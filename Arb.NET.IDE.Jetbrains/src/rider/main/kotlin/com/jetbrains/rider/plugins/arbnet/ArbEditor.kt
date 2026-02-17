package com.jetbrains.rider.plugins.arbnet

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
import com.jetbrains.rd.ide.model.arbModel
import com.jetbrains.rd.util.lifetime.Lifetime
import com.jetbrains.rider.projectView.solution
import java.awt.BorderLayout
import java.awt.event.KeyEvent
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import java.beans.PropertyChangeListener
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
    }

    private val lifetime = Lifetime.Eternal.createNested()

    private val panel: JPanel = JPanel(BorderLayout()).also { root ->
        val loadingLabel = JLabel("Loading ARB data…", JLabel.CENTER)
        root.add(loadingLabel, BorderLayout.CENTER)

        project.solution.arbModel.getArbData.start(lifetime, Unit).result.advise(lifetime) { result ->
            val allLocaleData = result.unwrap()

            // Group by directory; sort alphabetically for a stable order.
            val byDirectory: Map<String, List<ArbLocaleData>> = allLocaleData
                .groupBy { it.directory }
                .toSortedMap()

            val directories = byDirectory.keys.toList()

            val hintDir = file.getUserData(INITIAL_DIR_KEY) ?: file.parent?.path ?: ""
            val initialDir = directories.firstOrNull { it == hintDir } ?: directories.firstOrNull() ?: return@advise

            SwingUtilities.invokeLater {
                root.remove(loadingLabel)

                val dirCombo = JComboBox(directories.toTypedArray())
                dirCombo.selectedItem = initialDir
                root.add(dirCombo, BorderLayout.NORTH)

                val tableHolder = JPanel(BorderLayout())
                root.add(tableHolder, BorderLayout.CENTER)

                // Resolve rename shortcut(s) from the user's active keymap.
                val renameShortcuts: Set<KeyStroke> = ActionManager.getInstance()
                    .getAction(IdeActions.ACTION_RENAME)
                    ?.shortcutSet
                    ?.shortcuts
                    ?.mapNotNull { s ->
                        (s as? com.intellij.openapi.actionSystem.KeyboardShortcut)?.firstKeyStroke
                    }
                    ?.toSet()
                    ?: emptySet()

                fun buildTable(directory: String) {
                    tableHolder.removeAll()

                    val localeDataList = byDirectory[directory] ?: return
                    val localeToFilePath = localeDataList.associate { it.locale to it.filePath }
                    val locales = localeDataList.map { it.locale }.sorted()
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
                        columnModel.getColumn(0).preferredWidth = 240
                        for (i in 1 until columnModel.columnCount) {
                            columnModel.getColumn(i).preferredWidth = 220
                        }

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
                                // Select the row that was right-clicked.
                                setRowSelectionInterval(row, row)
                                if (col != 0) return
                                val menu = JPopupMenu()
                                val renameItem = JMenuItem("Rename")
                                renameItem.addActionListener { doRenameRow(this@apply, row) }
                                menu.add(renameItem)
                                menu.show(this@apply, e.x, e.y)
                            }
                        })
                    }

                    // Double-click on a locale column header → open the raw .arb file in the IDE text editor.
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
                    })

                    tableHolder.add(JBScrollPane(table), BorderLayout.CENTER)
                    tableHolder.revalidate()
                    tableHolder.repaint()
                }

                buildTable(initialDir)

                dirCombo.addActionListener {
                    val selected = dirCombo.selectedItem as? String ?: return@addActionListener
                    buildTable(selected)
                }

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
