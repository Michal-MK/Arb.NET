package com.jetbrains.rider.plugins.arbnet

import com.intellij.ide.util.PropertiesComponent
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.application.ModalityState
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.DialogWrapper
import com.intellij.openapi.ui.DialogWrapper.IdeModalityType
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.components.JBTextField
import com.intellij.ui.table.JBTable
import com.jetbrains.rd.ide.model.ArbEntryUpdate
import com.jetbrains.rd.ide.model.ArbTranslateRequest
import com.jetbrains.rd.ide.model.ArbTranslationItem
import com.jetbrains.rd.ide.model.AzureTranslationSettings
import com.jetbrains.rd.ide.model.arbModel
import com.jetbrains.rd.util.lifetime.Lifetime
import com.jetbrains.rd.util.lifetime.LifetimeDefinition
import com.jetbrains.rider.projectView.solution
import com.intellij.openapi.diagnostic.Logger
import java.awt.BorderLayout
import java.awt.Dimension
import java.awt.FlowLayout
import java.awt.GridBagConstraints
import java.awt.GridBagLayout
import java.awt.Insets
import javax.swing.BorderFactory
import javax.swing.Box
import javax.swing.BoxLayout
import javax.swing.ButtonGroup
import javax.swing.JButton
import javax.swing.JCheckBox
import javax.swing.JComboBox
import javax.swing.JComponent
import javax.swing.JPanel
import javax.swing.JProgressBar
import javax.swing.JRadioButton
import javax.swing.table.AbstractTableModel

/**
 * One row in the preview/results table.
 * Starts with proposedTranslation="" (queued); gets filled in after translation.
 */
data class TranslationResultRow(
    val key: String,
    val targetLocale: String,
    val sourceText: String,
    val existingTranslation: String,
    var proposedTranslation: String = "",
    var accepted: Boolean = true,
)

/**
 * Full-featured translate dialog matching the Visual Studio implementation.
 *
 * The table is pre-populated on open showing every key that will be sent to the AI,
 * along with the existing translation (if any). After clicking Translate the
 * Translation column fills in. The user can uncheck rows or edit translations
 * before clicking Apply Selected.
 *
 * Uses IdeModalityType.MODELESS so the dialog does not install an AWT secondary-loop
 * modal pump. This is required because RD protocol advise callbacks are delivered
 * through RD's internal event queue, which is only drained by the outer Rider event
 * loop. A modal pump would starve that queue and the translation result would never
 * arrive.
 */
private val log = Logger.getInstance(ArbTranslateDialog::class.java)

class ArbTranslateDialog(
    private val project: Project,
    private val directory: String,
    /** All locale codes available in this directory, in display order. */
    private val locales: List<String>,
    /** Map of locale → (key → value) for the current directory. */
    private val byLocale: Map<String, Map<String, String>>,
    /** Row indices (into the sorted-keys list) to limit translation to; null means all rows. */
    private val selectedRowIndices: List<Int>? = null,
    /** Called on the EDT immediately after the user clicks "Apply Selected" and changes are saved. */
    private val onApplied: () -> Unit = {},
) : DialogWrapper(project, true, IdeModalityType.MODELESS) {

    // ── Controls ───────────────────────────────────────────────────────────────

    private val sourceCombo = JComboBox(locales.toTypedArray())
    private val targetCheckboxes = mutableListOf<JCheckBox>()
    private val targetPanel = JPanel(FlowLayout(FlowLayout.LEFT, 8, 2))
    private val emptyOnlyRadio = JRadioButton("Empty cells only", true)
    private val allCellsRadio = JRadioButton("All cells")
    private val customPromptField = JBTextField()
    private val translateButton = JButton("Translate")
    private val stopButton = JButton("Stop").also { it.isVisible = false }
    private val statusLabel = JBLabel("")
    private val progressBar = JProgressBar(0, 100).also { it.isVisible = false }
    private val applyButton = JButton("Apply Selected").also { it.isEnabled = false }

    // ── Table model ────────────────────────────────────────────────────────────

    private val rows = mutableListOf<TranslationResultRow>()

    private val tableModel = object : AbstractTableModel() {
        val COLS = arrayOf("", "Key", "Target", "Source text", "Existing", "Translation")
        override fun getRowCount() = rows.size
        override fun getColumnCount() = COLS.size
        override fun getColumnName(col: Int) = COLS[col]
        override fun getColumnClass(col: Int) = if (col == 0) Boolean::class.javaObjectType else String::class.java
        override fun isCellEditable(row: Int, col: Int) = col == 0 || col == 5
        override fun getValueAt(row: Int, col: Int): Any = when (col) {
            0 -> rows[row].accepted
            1 -> rows[row].key
            2 -> rows[row].targetLocale
            3 -> rows[row].sourceText
            4 -> rows[row].existingTranslation
            5 -> rows[row].proposedTranslation
            else -> ""
        }
        override fun setValueAt(value: Any?, row: Int, col: Int) {
            when (col) {
                0 -> rows[row].accepted = value as Boolean
                5 -> rows[row].proposedTranslation = value as? String ?: ""
            }
            fireTableCellUpdated(row, col)
            applyButton.isEnabled = rows.any { it.accepted && it.proposedTranslation.isNotBlank() }
        }
    }

    private val table = JBTable(tableModel).apply {
        isStriped = true
        setShowGrid(true)
        autoResizeMode = JBTable.AUTO_RESIZE_LAST_COLUMN
        columnModel.getColumn(0).apply { preferredWidth = 30; maxWidth = 30 }
        columnModel.getColumn(1).preferredWidth = 130
        columnModel.getColumn(2).preferredWidth = 55
        columnModel.getColumn(3).preferredWidth = 180
        columnModel.getColumn(4).preferredWidth = 130
        columnModel.getColumn(5).preferredWidth = 200
    }

    // ── State ──────────────────────────────────────────────────────────────────

    private val dialogLifetime = LifetimeDefinition()
    @Volatile private var cancelled = false
    var appliedChanges = false
        private set

    // Captured in startTranslation() after the dialog is visible so it reflects
    // the correct modality context for invokeLater calls.
    private var dialogModality: ModalityState = ModalityState.any()

    // ── All non-metadata keys, sorted ─────────────────────────────────────────

    private val allKeys: List<String> = byLocale.values
        .flatMap { it.keys }
        .filter { !it.startsWith("@") }
        .toSortedSet()
        .toList()

    private val limitedKeys: List<String> = if (selectedRowIndices != null)
        selectedRowIndices.mapNotNull { allKeys.getOrNull(it) }
    else
        allKeys

    // ── Init ───────────────────────────────────────────────────────────────────

    init {
        title = "AI Translate"

        // Default source to "en*" locale, otherwise the locale with the most non-empty values.
        val defaultSource = locales.firstOrNull { it == "en" }
            ?: locales.firstOrNull { it.startsWith("en", ignoreCase = true) }
            ?: locales.maxByOrNull { locale ->
                byLocale[locale]?.count { (k, v) -> !k.startsWith("@") && v.isNotBlank() } ?: 0
            }
            ?: locales.firstOrNull()
        if (defaultSource != null) sourceCombo.selectedItem = defaultSource

        customPromptField.text = PropertiesComponent.getInstance().getValue(ArbEditor.SETTINGS_PROMPT, "")

        ButtonGroup().apply {
            add(emptyOnlyRadio)
            add(allCellsRadio)
        }

        sourceCombo.addActionListener {
            rebuildTargetCheckboxes()
            rebuildPreview()
        }
        emptyOnlyRadio.addActionListener { rebuildPreview() }
        allCellsRadio.addActionListener { rebuildPreview() }

        translateButton.addActionListener { startTranslation() }
        stopButton.addActionListener {
            cancelled = true
            stopButton.isEnabled = false
            statusLabel.text = "Stopping after current locale finishes…"
            contentPane?.repaint()
        }

        init()

        rebuildTargetCheckboxes()
        rebuildPreview()
    }

    // ── Dialog layout ──────────────────────────────────────────────────────────

    override fun createCenterPanel(): JComponent {
        val root = JPanel(BorderLayout(0, 8))
        root.border = BorderFactory.createEmptyBorder(8, 8, 4, 8)
        root.preferredSize = Dimension(860, 580)

        val topSection = JPanel(GridBagLayout())
        val gbc = GridBagConstraints().apply {
            fill = GridBagConstraints.HORIZONTAL
            insets = Insets(2, 4, 2, 4)
        }

        gbc.gridx = 0; gbc.gridy = 0; gbc.weightx = 0.0
        topSection.add(JBLabel("Source locale:"), gbc)
        gbc.gridx = 1; gbc.weightx = 1.0
        topSection.add(sourceCombo, gbc)

        gbc.gridx = 0; gbc.gridy = 1; gbc.weightx = 0.0; gbc.anchor = GridBagConstraints.NORTHWEST
        topSection.add(JBLabel("Target locales:"), gbc)
        gbc.gridx = 1; gbc.weightx = 1.0; gbc.anchor = GridBagConstraints.WEST
        topSection.add(targetPanel, gbc)

        gbc.gridx = 0; gbc.gridy = 2; gbc.weightx = 0.0
        topSection.add(JBLabel("Mode:"), gbc)
        gbc.gridx = 1; gbc.weightx = 1.0
        val modePanel = JPanel(FlowLayout(FlowLayout.LEFT, 0, 0))
        modePanel.add(emptyOnlyRadio)
        modePanel.add(Box.createHorizontalStrut(16))
        modePanel.add(allCellsRadio)
        topSection.add(modePanel, gbc)

        gbc.gridx = 0; gbc.gridy = 3; gbc.weightx = 0.0
        topSection.add(JBLabel("Custom prompt:"), gbc)
        gbc.gridx = 1; gbc.weightx = 1.0
        topSection.add(customPromptField, gbc)

        gbc.gridx = 0; gbc.gridy = 4; gbc.weightx = 0.0; gbc.gridwidth = 2
        val translateRow = JPanel(FlowLayout(FlowLayout.LEFT, 4, 0))
        translateRow.add(translateButton)
        translateRow.add(stopButton)
        topSection.add(translateRow, gbc)

        root.add(topSection, BorderLayout.NORTH)
        root.add(JBScrollPane(table), BorderLayout.CENTER)

        val statusSection = JPanel()
        statusSection.layout = BoxLayout(statusSection, BoxLayout.Y_AXIS)
        statusSection.border = BorderFactory.createEmptyBorder(4, 0, 0, 0)
        statusLabel.alignmentX = java.awt.Component.LEFT_ALIGNMENT
        progressBar.alignmentX = java.awt.Component.LEFT_ALIGNMENT
        statusSection.add(statusLabel)
        statusSection.add(Box.createVerticalStrut(4))
        statusSection.add(progressBar)
        root.add(statusSection, BorderLayout.SOUTH)

        return root
    }

    override fun createSouthPanel(): JComponent {
        val closeButton = JButton("Close").also { it.addActionListener { close(CANCEL_EXIT_CODE) } }
        applyButton.addActionListener { applySelected() }
        val panel = JPanel(FlowLayout(FlowLayout.RIGHT, 4, 0))
        panel.add(applyButton)
        panel.add(closeButton)
        return panel
    }

    // ── Rebuild target checkboxes ──────────────────────────────────────────────

    private fun rebuildTargetCheckboxes() {
        val source = sourceCombo.selectedItem as? String ?: return
        targetPanel.removeAll()
        targetCheckboxes.clear()
        for (locale in locales) {
            if (locale == source) continue
            val cb = JCheckBox(locale, true)
            cb.addActionListener { rebuildPreview() }
            targetCheckboxes.add(cb)
            targetPanel.add(cb)
        }
        targetPanel.revalidate()
        targetPanel.repaint()
    }

    // ── Rebuild preview table ──────────────────────────────────────────────────

    private fun rebuildPreview() {
        val sourceLocale = sourceCombo.selectedItem as? String ?: return
        val targetLocales = targetCheckboxes.filter { it.isSelected }.map { it.text }
        val sourceMap = byLocale[sourceLocale] ?: emptyMap()
        val emptyOnly = emptyOnlyRadio.isSelected

        rows.clear()
        for (targetLocale in targetLocales) {
            val targetMap = byLocale[targetLocale] ?: emptyMap()
            for (key in limitedKeys) {
                val sourceText = sourceMap[key] ?: ""
                if (sourceText.isBlank()) continue
                val existing = targetMap[key] ?: ""
                if (emptyOnly && existing.isNotBlank()) continue
                rows.add(TranslationResultRow(
                    key = key,
                    targetLocale = targetLocale,
                    sourceText = sourceText,
                    existingTranslation = existing,
                ))
            }
        }

        tableModel.fireTableDataChanged()
        applyButton.isEnabled = false

        statusLabel.text = when {
            rows.isEmpty() && emptyOnly -> "${rows.size} items queued — all targets already translated. Switch to \"All cells\" to re-translate."
            rows.isEmpty() -> "No translatable items found for the selected source locale."
            else -> "${rows.size} item(s) queued for translation."
        }
    }

    // ── Translation ────────────────────────────────────────────────────────────

    private fun startTranslation() {
        if (rows.isEmpty()) {
            statusLabel.text = "Nothing queued — adjust source locale, targets, or mode."
            return
        }

        val sourceLocale = sourceCombo.selectedItem as? String ?: return

        val props = PropertiesComponent.getInstance()
        val endpoint = props.getValue(ArbEditor.SETTINGS_ENDPOINT, "")
        val deployment = props.getValue(ArbEditor.SETTINGS_DEPLOYMENT, "")
        val apiKey = props.getValue(ArbEditor.SETTINGS_API_KEY, "")
        if (endpoint.isBlank() || deployment.isBlank() || apiKey.isBlank()) {
            statusLabel.text = "AI settings are not configured. Use the \"AI Settings...\" button."
            return
        }

        val customPrompt = customPromptField.text.trim()
            .ifEmpty { props.getValue(ArbEditor.SETTINGS_PROMPT, "") }
        val temperature = props.getValue(ArbEditor.SETTINGS_TEMPERATURE, "0.2")?.toFloatOrNull() ?: 0.2f
        val settings = AzureTranslationSettings(endpoint, deployment, apiKey, customPrompt, temperature)

        // Group the current rows by target locale, preserving order.
        data class LocaleBatch(val targetLocale: String, val items: List<ArbTranslationItem>, val rowIndices: List<Int>)

        val batches: List<LocaleBatch> = rows
            .mapIndexed { i, row -> i to row }
            .groupBy { (_, row) -> row.targetLocale }
            .map { (locale, pairs) ->
                LocaleBatch(
                    targetLocale = locale,
                    items = pairs.map { (_, row) -> ArbTranslationItem(row.key, row.sourceText, null) },
                    rowIndices = pairs.map { (i, _) -> i },
                )
            }

        val totalItems = batches.sumOf { it.items.size }
        var completedItems = 0

        // Capture modality after the dialog is visible so invokeLater is dispatched
        // by the correct pump context.
        dialogModality = ModalityState.current()
        log.warn("[AI-DEBUG] startTranslation: batches=${batches.size} totalItems=$totalItems modality=$dialogModality thread=${Thread.currentThread().name}")

        cancelled = false
        translateButton.isEnabled = false
        stopButton.isVisible = true
        stopButton.isEnabled = true
        applyButton.isEnabled = false
        progressBar.value = 0
        progressBar.isVisible = true
        statusLabel.text = "Starting translation…"
        contentPane?.revalidate()
        contentPane?.repaint()

        val app = ApplicationManager.getApplication()

        fun translateNext(remaining: List<LocaleBatch>) {
            if (remaining.isEmpty() || cancelled) {
                app.invokeLater({
                    translateButton.isEnabled = true
                    stopButton.isVisible = false
                    progressBar.value = 100
                    val filled = rows.count { it.proposedTranslation.isNotBlank() }
                    statusLabel.text = if (cancelled) "Translation cancelled ($filled translated)."
                    else "Done — $filled item(s) translated."
                    applyButton.isEnabled = filled > 0
                }, dialogModality)
                return
            }

            val current = remaining.first()
            val rest = remaining.drop(1)

            statusLabel.text = "Translating to ${current.targetLocale}…"
            contentPane?.revalidate()
            contentPane?.repaint()

            val request = ArbTranslateRequest(directory, settings, sourceLocale, current.targetLocale, current.items)

            log.warn("[AI-DEBUG] calling translateArbEntries.start() locale=${current.targetLocale} items=${current.items.size} thread=${Thread.currentThread().name}")
            project.solution.arbModel.translateArbEntries.start(Lifetime.Eternal, request)
                .result.advise(Lifetime.Eternal) { rdResult ->
                    log.warn("[AI-DEBUG] advise fired thread=${Thread.currentThread().name}")
                    val translatedByKey: Map<String, String>?
                    val errMsg: String?
                    try {
                        val response = rdResult.unwrap()
                        if (response.success) {
                            translatedByKey = response.items.associate { it.key to it.translatedText }
                            errMsg = null
                        } else {
                            translatedByKey = emptyMap()
                            errMsg = response.errorMessage ?: "unknown"
                        }
                    } catch (t: Throwable) {
                        app.invokeLater({
                            statusLabel.text = "Error for ${current.targetLocale}: ${t.message ?: "unknown error"}"
                            translateButton.isEnabled = true
                            stopButton.isVisible = false
                            applyButton.isEnabled = rows.any { it.proposedTranslation.isNotBlank() }
                        }, dialogModality)
                        return@advise
                    }

                    completedItems += current.items.size
                    val progress = (completedItems.toDouble() / totalItems * 100).toInt()

                    app.invokeLater({
                        if (errMsg != null) statusLabel.text = "Error for ${current.targetLocale}: $errMsg"
                        progressBar.value = progress
                        for (idx in current.rowIndices) {
                            val row = rows.getOrNull(idx) ?: continue
                            val translated = translatedByKey?.get(row.key) ?: continue
                            if (translated.isNotBlank()) {
                                row.proposedTranslation = translated
                                tableModel.fireTableRowsUpdated(idx, idx)
                            }
                        }
                        translateNext(rest)
                    }, dialogModality)
                }
        }

        translateNext(batches)
    }

    // ── Apply ──────────────────────────────────────────────────────────────────

    private fun applySelected() {
        val accepted = rows.filter { it.accepted && it.proposedTranslation.isNotBlank() }
        if (accepted.isEmpty()) return

        for (row in accepted) {
            project.solution.arbModel.saveArbEntry.start(
                dialogLifetime.lifetime,
                ArbEntryUpdate(directory, row.targetLocale, row.key, row.proposedTranslation)
            )
        }

        appliedChanges = true
        onApplied()
        applyButton.isEnabled = false
        statusLabel.text = "Applied ${accepted.size} translation(s)."
    }

    override fun dispose() {
        dialogLifetime.terminate()
        super.dispose()
    }
}