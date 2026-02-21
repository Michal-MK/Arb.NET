package com.jetbrains.rider.plugins.arbnet

import com.intellij.ide.util.PropertiesComponent
import com.intellij.openapi.actionSystem.ActionManager
import com.intellij.openapi.actionSystem.IdeActions
import com.intellij.openapi.fileEditor.FileEditor
import com.intellij.openapi.fileEditor.FileEditorState
import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.Messages
import com.intellij.openapi.util.Key
import com.intellij.openapi.util.UserDataHolderBase
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.table.JBTable
import com.jetbrains.rd.ide.model.ArbEntryUpdate
import com.jetbrains.rd.ide.model.ArbKeyRename
import com.jetbrains.rd.ide.model.ArbLocaleData
import com.jetbrains.rd.ide.model.ArbNewKey
import com.jetbrains.rd.ide.model.ArbNewLocale
import com.jetbrains.rd.ide.model.ArbRemoveKey
import com.jetbrains.rd.ide.model.arbModel
import com.jetbrains.rd.util.lifetime.Lifetime
import com.jetbrains.rider.projectView.solution
import java.awt.BorderLayout
import java.awt.event.KeyEvent
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import java.beans.PropertyChangeListener
import javax.swing.JButton
import javax.swing.JComboBox
import javax.swing.JComponent
import javax.swing.JLabel
import javax.swing.JMenuItem
import javax.swing.JPanel
import javax.swing.JPopupMenu
import javax.swing.KeyStroke
import javax.swing.SwingUtilities
import javax.swing.event.TableModelEvent
import javax.swing.table.DefaultTableModel

class ArbEditor(private val project: Project, private val file: VirtualFile) : UserDataHolderBase(), FileEditor {

    companion object {
        const val FILE_NAME = "Arb.NET Editor"
        val INITIAL_DIR_KEY: Key<String> = Key.create("arb.initialDir")

        private const val COL_WIDTH_PREFIX = "arb.net.colWidth."
        private const val COL_ORDER_PREFIX = "arb.net.colOrder."
        private const val DEFAULT_KEY_COL_WIDTH = 240
        private const val DEFAULT_LOCALE_COL_WIDTH = 220
        internal const val SETTINGS_ENDPOINT = "arb.net.ai.endpoint"
        internal const val SETTINGS_DEPLOYMENT = "arb.net.ai.deployment"
        internal const val SETTINGS_API_KEY = "arb.net.ai.key"
        internal const val SETTINGS_PROMPT = "arb.net.ai.prompt"
        internal const val SETTINGS_TEMPERATURE = "arb.net.ai.temperature"

        // Keys are scoped by directory so different directories don't share state.
        private fun widthKey(directory: String, header: String) = "$COL_WIDTH_PREFIX$directory:$header"
        private fun orderKey(directory: String) = "$COL_ORDER_PREFIX$directory"
    }

    private fun loadColumnWidth(directory: String, header: String): Int {
        val raw = PropertiesComponent.getInstance().getValue(widthKey(directory, header)) ?: return -1
        return raw.toIntOrNull()?.takeIf { it > 0 } ?: -1
    }

    private fun saveColumnWidths(directory: String, table: JBTable, columnNames: Array<String>) {
        val props = PropertiesComponent.getInstance()
        for (i in columnNames.indices) {
            props.setValue(widthKey(directory, columnNames[i]), table.columnModel.getColumn(i).width.toString())
        }
    }

    // Locale order persisted as comma-separated names in display-index order (Key column excluded).
    private fun loadLocaleOrder(directory: String): List<String>? {
        val raw = PropertiesComponent.getInstance().getValue(orderKey(directory)) ?: return null
        return raw.split(",").filter { it.isNotEmpty() }
    }

    private fun saveLocaleOrder(directory: String, table: JBTable) {
        // Walk columns in current display order, skip the Key column.
        val ordered = (0 until table.columnModel.columnCount)
            .map { table.columnModel.getColumn(it).headerValue as? String ?: "" }
            .filter { it.isNotEmpty() && it != "Key" }
        PropertiesComponent.getInstance().setValue(orderKey(directory), ordered.joinToString(","))
    }

    private val lifetime = Lifetime.Eternal.createNested()

    // Resolve rename shortcut(s) once; they don't change during a session.
    private val renameShortcuts: Set<KeyStroke> = ActionManager.getInstance()
        .getAction(IdeActions.ACTION_RENAME)
        ?.shortcutSet
        ?.shortcuts
        ?.mapNotNull { s -> (s as? com.intellij.openapi.actionSystem.KeyboardShortcut)?.firstKeyStroke }
        ?.toSet()
        ?: emptySet()

    private val hintDir = file.getUserData(INITIAL_DIR_KEY) ?: file.parent?.path ?: ""

    // Mutable state owned by the editor; written on the EDT only.
    private var dirCombo: JComboBox<String>? = null
    private var tableHolder: JPanel? = null
    private var initialised = false
    // The translate action is updated each time buildTable() runs to capture the current directory data.
    private var openTranslateDialog: (() -> Unit)? = null
    // The remove-key action is updated each time buildTable() runs so it can access the live table.
    private var removeSelectedKey: (() -> Unit)? = null

    private val panel: JPanel = JPanel(BorderLayout()).also { root ->
        root.add(JLabel("Loading ARB data…", JLabel.CENTER), BorderLayout.CENTER)
    }

    /** Fetch fresh ARB data from the backend and rebuild the whole UI. */
    fun refresh() {
        project.solution.arbModel.getArbData.start(lifetime, Unit).result.advise(lifetime) { result ->
            val allLocaleData = try {
                result.unwrap()
            } catch (t: Throwable) {
                SwingUtilities.invokeLater {
                    panel.removeAll()
                    panel.add(JLabel("Failed to load ARB data: ${t.message ?: "unknown error"}", JLabel.CENTER), BorderLayout.CENTER)
                    panel.revalidate()
                    panel.repaint()
                }
                return@advise
            }

            val byDirectory: Map<String, List<ArbLocaleData>> = allLocaleData
                .groupBy { it.directory }
                .toSortedMap()

            val directories = byDirectory.keys.toList()

            SwingUtilities.invokeLater {
                if (!initialised) {
                    // First call: build chrome (combo + button + table holder).
                    panel.removeAll()

                    val combo = JComboBox(directories.toTypedArray())
                    dirCombo = combo

                    val addKeyButton = JButton("Add Key")
                    val addLocaleButton = JButton("Add Locale")
                    val removeKeyButton = JButton("Remove Key")
                    val translateButton = JButton("Translate...")
                    val aiSettingsButton = JButton("AI Settings...")

                    val topPanel = JPanel(java.awt.FlowLayout(java.awt.FlowLayout.LEFT, 4, 4))
                    topPanel.add(combo)
                    topPanel.add(addLocaleButton)
                    topPanel.add(addKeyButton)
                    topPanel.add(removeKeyButton)
                    topPanel.add(translateButton)
                    topPanel.add(aiSettingsButton)

                    val holder = JPanel(BorderLayout())
                    tableHolder = holder

                    panel.add(topPanel, BorderLayout.NORTH)
                    panel.add(holder, BorderLayout.CENTER)

                    val initialDir = directories.firstOrNull { it == hintDir }
                        ?: directories.firstOrNull()

                    if (initialDir != null) {
                        combo.selectedItem = initialDir
                        buildTable(initialDir, byDirectory)
                    }

                    addKeyButton.addActionListener {
                        val directory = combo.selectedItem as? String ?: return@addActionListener
                        val newKey = Messages.showInputDialog(
                            project,
                            "Enter the new key name:",
                            "Add ARB Key",
                            null,
                            "",
                            null
                        ) ?: return@addActionListener
                        if (newKey.isBlank()) return@addActionListener
                        project.solution.arbModel.addArbKey.start(
                            lifetime, ArbNewKey(directory, newKey)
                        ).result.advise(lifetime) { addResult ->
                            if (addResult.unwrap()) refresh()
                        }
                    }

                    addLocaleButton.addActionListener {
                        val directory = combo.selectedItem as? String ?: return@addActionListener
                        val locale = Messages.showInputDialog(
                            project,
                            "Enter locale code (e.g. de, fr):",
                            "Add ARB Locale",
                            null,
                            "",
                            null
                        ) ?: return@addActionListener
                        if (locale.isBlank()) return@addActionListener
                        project.solution.arbModel.addArbLocale.start(
                            lifetime, ArbNewLocale(directory, locale.trim())
                        ).result.advise(lifetime) { r -> if (r.unwrap()) refresh() }
                    }

                    removeKeyButton.addActionListener { removeSelectedKey?.invoke() }

                    translateButton.addActionListener {
                        openTranslateDialog?.invoke()
                    }

                    aiSettingsButton.addActionListener {
                        ArbTranslationSettingsDialog(project).showAndGet()
                    }

                    combo.addActionListener {
                        val selected = combo.selectedItem as? String ?: return@addActionListener
                        refresh()
                    }

                    initialised = true
                } else {
                    // Subsequent calls (refresh): update combo items and rebuild the current table.
                    val combo = dirCombo ?: return@invokeLater
                    val previousSelection = combo.selectedItem as? String

                    // Temporarily remove the action listener to avoid re-triggering refresh while
                    // we repopulate the combo.
                    val listeners = combo.actionListeners.toList()
                    listeners.forEach { combo.removeActionListener(it) }

                    combo.removeAllItems()
                    directories.forEach { combo.addItem(it) }

                    val selected = if (previousSelection != null && directories.contains(previousSelection))
                        previousSelection
                    else
                        directories.firstOrNull()

                    if (selected != null) combo.selectedItem = selected

                    listeners.forEach { combo.addActionListener(it) }

                    if (selected != null) buildTable(selected, byDirectory)
                }

                panel.revalidate()
                panel.repaint()
            }
        }
    }

    private fun buildTable(directory: String, byDirectory: Map<String, List<ArbLocaleData>>) {
        val holder = tableHolder ?: return
        holder.removeAll()

        val localeDataList = byDirectory[directory] ?: return
        val localeToFilePath = localeDataList.associate { it.locale to it.filePath }
        val alphabetical = localeDataList.map { it.locale }.sorted()
        val savedOrder = loadLocaleOrder(directory)
        val locales = if (savedOrder != null) {
            // Known locales in saved order, then any new locales (alphabetically) at the end.
            savedOrder.filter { it in alphabetical } + alphabetical.filter { it !in savedOrder }
        } else {
            alphabetical
        }
        val allKeys = localeDataList.flatMap { ld -> ld.entries.map { it.key } }.toSortedSet()
        val byLocale = localeDataList.associate { ld ->
            ld.locale to ld.entries.associate { it.key to it.value }
        }

        val columnNames = (listOf("Key") + locales).toTypedArray()

        // Col 0 (Key) is intentionally non-editable; rename via rename shortcut only.
        val tableModel = object : DefaultTableModel(columnNames, 0) {
            override fun isCellEditable(row: Int, column: Int) = column != 0
        }.apply {
            for (key in allKeys) {
                val row = listOf(key) + locales.map { locale -> byLocale[locale]?.get(key) ?: "" }
                addRow(row.toTypedArray())
            }
        }

        fun openTranslateDialogForRows(selectedRowIndices: List<Int>?) {
            ArbTranslateDialog(
                project, directory, locales, byLocale, selectedRowIndices,
                onApplied = { refresh() }
            ).show()
        }

        tableModel.addTableModelListener { e ->
            if (e.type != TableModelEvent.UPDATE) return@addTableModelListener
            val row = e.firstRow
            val col = e.column
            if (col <= 0) return@addTableModelListener

            val key = tableModel.getValueAt(row, 0) as? String ?: return@addTableModelListener
            val locale = locales[col - 1]
            val newValue = tableModel.getValueAt(row, col) as? String ?: return@addTableModelListener
            project.solution.arbModel.saveArbEntry.start(
                lifetime, ArbEntryUpdate(directory, locale, key, newValue)
            )
        }

        // Shared rename action reused by both the keyboard shortcut and the context menu.
        fun doRenameRow(table: JBTable, row: Int) {
            val oldKey = tableModel.getValueAt(row, 0) as? String ?: return
            val newKey = Messages.showInputDialog(
                project,
                "Rename key '$oldKey' in all locale files:",
                "Rename ARB Key",
                null,
                oldKey,
                null
            ) ?: return
            if (newKey == oldKey || newKey.isBlank()) return
            project.solution.arbModel.renameArbKey.start(
                lifetime, ArbKeyRename(directory, oldKey, newKey)
            )
            // Update the table model so the UI reflects the rename immediately.
            tableModel.setValueAt(newKey, row, 0)
        }

        val table = JBTable(tableModel).apply {
            isStriped = true
            setShowGrid(true)
            autoResizeMode = JBTable.AUTO_RESIZE_OFF
            columnModel.getColumn(0).preferredWidth =
                loadColumnWidth(directory, "Key").takeIf { it > 0 } ?: DEFAULT_KEY_COL_WIDTH
            for (i in 1 until columnModel.columnCount) {
                columnModel.getColumn(i).preferredWidth =
                    loadColumnWidth(directory, locales[i - 1]).takeIf { it > 0 } ?: DEFAULT_LOCALE_COL_WIDTH
            }

            // Column-move persistence is handled in header mouseReleased (below) alongside widths,
            // because columnMoved fires on every intermediate drag step — the fromIndex/toIndex
            // values during intermediate events are unreliable for detecting completion.

            addKeyListener(object : java.awt.event.KeyAdapter() {
                override fun keyPressed(e: KeyEvent) {
                    val pressed = KeyStroke.getKeyStrokeForEvent(e)
                    if (pressed !in renameShortcuts) return
                    val row = selectedRow.takeIf { it >= 0 } ?: return
                    doRenameRow(this@apply, row)
                }
            })

            addMouseListener(object : MouseAdapter() {
                override fun mousePressed(e: MouseEvent) {
                    if (!SwingUtilities.isRightMouseButton(e)) return
                    val row = rowAtPoint(e.point)
                    val col = columnAtPoint(e.point)
                    if (row < 0) return
                    // Keep current multi-selection if right-clicked row is already selected.
                    if (!isRowSelected(row)) {
                        setRowSelectionInterval(row, row)
                    }
                    val menu = JPopupMenu()
                    val translateItem = JMenuItem("Translate Selected")
                    translateItem.addActionListener {
                        val selectedIndices = this@apply.selectedRows.toList()
                        openTranslateDialogForRows(selectedIndices.ifEmpty { null })
                    }
                    menu.add(translateItem)

                    if (col != 0) {
                        menu.show(this@apply, e.x, e.y)
                        return
                    }

                    val renameItem = JMenuItem("Rename")
                    renameItem.addActionListener { doRenameRow(this@apply, row) }
                    menu.add(renameItem)
                    menu.show(this@apply, e.x, e.y)
                }
            })
        }

        // Wire the top-toolbar "Translate..." button to open the dialog for all rows.
        openTranslateDialog = { openTranslateDialogForRows(null) }

        // Wire the top-toolbar "Remove Key" button to remove the currently selected row.
        removeSelectedKey = remove@{
            val row = table.selectedRow.takeIf { it >= 0 } ?: return@remove
            val key = tableModel.getValueAt(row, 0) as? String ?: return@remove
            val confirm = Messages.showYesNoDialog(
                project,
                "Remove key '$key' from all locale files in this directory?",
                "Remove ARB Key",
                Messages.getQuestionIcon()
            )
            if (confirm != Messages.YES) return@remove
            project.solution.arbModel.removeArbKey.start(
                lifetime, ArbRemoveKey(directory, key)
            ).result.advise(lifetime) { r -> if (r.unwrap()) refresh() }
        }

        // Double-click on a locale column header → open the raw .arb file in the IDE text editor.
        // mouseReleased → save widths once the user finishes dragging a column divider.
        table.tableHeader.addMouseListener(object : MouseAdapter() {
            override fun mouseClicked(e: MouseEvent) {
                if (e.clickCount != 2) return
                val col = table.columnAtPoint(e.point)
                if (col <= 0) return
                val locale = locales[col - 1]
                val path = localeToFilePath[locale] ?: return
                val vf = LocalFileSystem.getInstance().findFileByPath(path) ?: return
                OpenFileDescriptor(project, vf).navigate(true)
            }

            override fun mouseReleased(e: MouseEvent) {
                saveColumnWidths(directory, table, columnNames)
                saveLocaleOrder(directory, table)
            }
        })

        holder.add(JBScrollPane(table), BorderLayout.CENTER)
        holder.revalidate()
        holder.repaint()
    }

    init {
        refresh()
    }

    override fun getComponent(): JComponent = panel
    override fun getPreferredFocusedComponent(): JComponent = panel
    override fun getName(): String = FILE_NAME
    override fun getFile(): VirtualFile = file
    override fun setState(state: FileEditorState) {}
    override fun isModified(): Boolean = false
    override fun isValid(): Boolean = true
    override fun selectNotify() { refresh() }
    override fun addPropertyChangeListener(listener: PropertyChangeListener) {}
    override fun removePropertyChangeListener(listener: PropertyChangeListener) {}
    override fun dispose() { lifetime.terminate() }
}
