package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.DialogWrapper
import com.intellij.openapi.ui.Messages
import com.intellij.ui.JBColor
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.table.JBTable
import com.jetbrains.rd.ide.model.ArbCsvPreviewResponse
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
import javax.swing.JComboBox
import javax.swing.JComponent
import javax.swing.JPanel
import javax.swing.JRadioButton
import javax.swing.table.AbstractTableModel

class ArbCsvImportDialog(
    project: Project,
    private val sourceName: String,
    private val preview: ArbCsvPreviewResponse,
) : DialogWrapper(project, true) {

    private val mappingCombos = mutableListOf<JComboBox<CsvImportMappingOption>>()
    private val mergeModeRadio = JRadioButton("Add or update keys", true)
    private val replaceModeRadio = JRadioButton("Replace all keys")

    var selectedMappings: List<String> = emptyList()
        private set

    var selectedImportMode: String = "Merge"
        private set

    init {
        title = "Import CSV"
        setOKButtonText("Import")
        init()
    }

    override fun createCenterPanel(): JComponent {
        val root = JPanel(BorderLayout(0, 8))
        root.border = BorderFactory.createEmptyBorder(8, 8, 4, 8)
        root.preferredSize = Dimension(1040, 700)

        val topPanel = JPanel().apply {
            layout = BoxLayout(this, BoxLayout.Y_AXIS)
        }

        topPanel.add(JBLabel("Review the header mapping for '$sourceName'. The CSV header is shown first, the editable mapping row is below it, and the table shows the remaining values."))
        topPanel.add(Box.createVerticalStrut(8))

        ButtonGroup().apply {
            add(mergeModeRadio)
            add(replaceModeRadio)
        }

        val modePanel = JPanel(FlowLayout(FlowLayout.LEFT, 4, 0))
        modePanel.add(JBLabel("Import mode:"))
        modePanel.add(mergeModeRadio)
        modePanel.add(Box.createHorizontalStrut(12))
        modePanel.add(replaceModeRadio)
        topPanel.add(modePanel)
        topPanel.add(Box.createVerticalStrut(8))

        val mappingPanel = JPanel(GridBagLayout())
        val mappingOptions = buildMappingOptions(preview.availableLocaleMappings)
        val gbc = GridBagConstraints().apply {
            fill = GridBagConstraints.HORIZONTAL
            insets = Insets(0, 0, 0, 0)
        }

        preview.headers.forEachIndexed { index, header ->
            gbc.gridx = index
            gbc.gridy = 0
            gbc.weightx = 1.0
            mappingPanel.add(createHeaderCell(if (header.isBlank()) "(empty)" else header), gbc)

            gbc.gridy = 1
            val combo = JComboBox(mappingOptions.toTypedArray()).apply {
                preferredSize = Dimension(170, preferredSize.height)
            }
            val suggested = preview.suggestedMappings.getOrNull(index).orEmpty()
            combo.selectedItem = mappingOptions.firstOrNull { it.value.equals(suggested, ignoreCase = true) } ?: mappingOptions.first()
            mappingCombos.add(combo)
            mappingPanel.add(createComboCell(combo), gbc)
        }

        topPanel.add(JBScrollPane(mappingPanel).apply {
            horizontalScrollBarPolicy = JBScrollPane.HORIZONTAL_SCROLLBAR_AS_NEEDED
            verticalScrollBarPolicy = JBScrollPane.VERTICAL_SCROLLBAR_NEVER
            border = BorderFactory.createLineBorder(com.intellij.util.ui.JBUI.CurrentTheme.CustomFrameDecorations.separatorForeground())
            preferredSize = Dimension(1000, 110)
        })

        root.add(topPanel, BorderLayout.NORTH)
        root.add(JBScrollPane(createPreviewTable()), BorderLayout.CENTER)
        return root
    }

    override fun doOKAction() {
        val mappings = mappingCombos.map { (it.selectedItem as? CsvImportMappingOption)?.value.orEmpty() }
        if (mappings.count { it.equals("key", ignoreCase = true) } != 1) {
            Messages.showWarningDialog("Select exactly one column mapped to Key.", "Arb.NET")
            return
        }

        val duplicates = mappings
            .filter { it.isNotBlank() && !it.equals("key", ignoreCase = true) }
            .groupingBy { it.lowercase() }
            .eachCount()
            .filterValues { it > 1 }
            .keys
            .toList()
        if (duplicates.isNotEmpty()) {
            Messages.showWarningDialog("Each locale can be mapped only once. Duplicate mappings: ${duplicates.joinToString(", ")}.", "Arb.NET")
            return
        }

        selectedMappings = mappings
        selectedImportMode = if (replaceModeRadio.isSelected) "ReplaceAll" else "Merge"
        super.doOKAction()
    }

    private fun createPreviewTable(): JBTable {
        val model = object : AbstractTableModel() {
            override fun getRowCount(): Int = preview.rows.size
            override fun getColumnCount(): Int = preview.headers.size
            override fun getColumnName(column: Int): String = preview.headers.getOrNull(column).takeUnless { it.isNullOrBlank() } ?: "(empty)"
            override fun getValueAt(rowIndex: Int, columnIndex: Int): Any = preview.rows[rowIndex].cells.getOrNull(columnIndex).orEmpty()
            override fun isCellEditable(rowIndex: Int, columnIndex: Int): Boolean = false
        }

        return JBTable(model).apply {
            isStriped = true
            setShowGrid(true)
            autoResizeMode = JBTable.AUTO_RESIZE_OFF
            for (column in 0 until columnModel.columnCount) {
                columnModel.getColumn(column).preferredWidth = 180
            }
        }
    }

    private fun createHeaderCell(text: String): JComponent {
        return JPanel(BorderLayout()).apply {
            border = BorderFactory.createCompoundBorder(
                BorderFactory.createMatteBorder(0, 0, 1, 1, JBColor.border()),
                BorderFactory.createEmptyBorder(6, 8, 6, 8)
            )
            add(JBLabel(text), BorderLayout.CENTER)
        }
    }

    private fun createComboCell(combo: JComboBox<CsvImportMappingOption>): JComponent {
        return JPanel(BorderLayout()).apply {
            border = BorderFactory.createCompoundBorder(
                BorderFactory.createMatteBorder(0, 0, 1, 1, JBColor.border()),
                BorderFactory.createEmptyBorder(4, 4, 4, 4)
            )
            add(combo, BorderLayout.CENTER)
        }
    }

    private fun buildMappingOptions(locales: List<String>): List<CsvImportMappingOption> {
        return buildList {
            add(CsvImportMappingOption("", "Ignore"))
            add(CsvImportMappingOption("key", "Key"))
            locales.forEach { locale -> add(CsvImportMappingOption(locale, locale)) }
        }
    }
}

private data class CsvImportMappingOption(val value: String, val label: String) {
    override fun toString(): String = label
}