package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.fileEditor.FileEditor
import com.intellij.openapi.fileEditor.FileEditorState
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.UserDataHolderBase
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.table.JBTable
import com.jetbrains.rd.ide.model.arbModel
import com.jetbrains.rd.util.lifetime.Lifetime
import com.jetbrains.rider.projectView.solution
import java.beans.PropertyChangeListener
import java.awt.BorderLayout
import javax.swing.JComponent
import javax.swing.JLabel
import javax.swing.JPanel
import javax.swing.SwingUtilities
import javax.swing.table.DefaultTableModel

class ArbEditor(private val project: Project, private val file: VirtualFile) : UserDataHolderBase(), FileEditor {

    companion object {
        const val FILE_NAME = "Arb.NET Editor"
    }

    private val lifetime = Lifetime.Eternal.createNested()

    private val panel: JPanel = JPanel(BorderLayout()).also { root ->
        val loadingLabel = JLabel("Loading ARB data…", JLabel.CENTER)
        root.add(loadingLabel, BorderLayout.CENTER)

        // Ask the backend for all ARB data asynchronously.
        project.solution.arbModel.getArbData.start(lifetime, Unit).result.advise(lifetime) { result ->
            val localeDataList = result.unwrap()

            // Build table model on whatever thread the result arrives on,
            // then push the UI update onto the EDT.
            val locales = localeDataList.map { it.locale }.sorted()
            val allKeys = localeDataList.flatMap { ld -> ld.entries.map { it.key } }.toSortedSet()

            // locale -> (key -> value)
            val byLocale = localeDataList.associate { ld ->
                ld.locale to ld.entries.associate { it.key to it.value }
            }

            val columnNames = (listOf("Key") + locales).toTypedArray()
            val tableModel = DefaultTableModel(columnNames, 0).apply {
                for (key in allKeys) {
                    val row = listOf(key) + locales.map { locale -> byLocale[locale]?.get(key) ?: "" }
                    addRow(row.toTypedArray())
                }
            }

            SwingUtilities.invokeLater {
                root.remove(loadingLabel)
                val table = JBTable(tableModel).apply {
                    isStriped = true
                    setShowGrid(true)
                    autoResizeMode = JBTable.AUTO_RESIZE_OFF
                    columnModel.getColumn(0).preferredWidth = 240
                    for (i in 1 until columnModel.columnCount) {
                        columnModel.getColumn(i).preferredWidth = 220
                    }
                }
                root.add(JBScrollPane(table), BorderLayout.CENTER)
                root.revalidate()
                root.repaint()
            }
        }
    }

    override fun getComponent(): JComponent = panel
    override fun getPreferredFocusedComponent(): JComponent = panel
    override fun getName(): String = FILE_NAME
    override fun getFile(): VirtualFile = file
    override fun setState(state: FileEditorState) {}
    override fun isModified(): Boolean = false
    override fun isValid(): Boolean = true
    override fun addPropertyChangeListener(listener: PropertyChangeListener) {}
    override fun removePropertyChangeListener(listener: PropertyChangeListener) {}
    override fun dispose() { lifetime.terminate() }
}
