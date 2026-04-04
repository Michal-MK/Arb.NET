package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.DialogWrapper
import com.intellij.openapi.ui.ValidationInfo
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBTextField
import java.awt.Dimension
import java.awt.GridBagConstraints
import java.awt.GridBagLayout
import java.awt.Insets
import javax.swing.JComponent
import javax.swing.JPanel
import javax.swing.JScrollPane

class ArbPlaceholderRenameDialog(
    project: Project,
    private val keyName: String,
    placeholderNames: List<String>,
) : DialogWrapper(project, true) {

    private data class RenameRow(val originalName: String, val field: JBTextField)

    private val rows = placeholderNames.map { name ->
        RenameRow(name, JBTextField(name))
    }

    val resultRenames: List<Pair<String, String>>
        get() = rows
            .map { it.originalName to it.field.text.trim() }
            .filter { (oldName, newName) -> newName.isNotEmpty() && newName != oldName }

    init {
        title = "Rename Placeholders"
        isResizable = true
        init()
        setSize(760, minOf(340, 150 + placeholderNames.size * 38))
        setOKButtonText("OK")
        setTitle("Rename Placeholders")
    }

    override fun createCenterPanel(): JComponent {
        val content = JPanel(GridBagLayout())
        val gbc = GridBagConstraints().apply {
            fill = GridBagConstraints.HORIZONTAL
            insets = Insets(4, 4, 4, 4)
            weightx = 0.0
            gridx = 0
            gridy = 0
        }

        gbc.gridwidth = 2
        content.add(JBLabel("Update placeholder names for key '$keyName'. Edit only the rows you want to rename."), gbc)

        gbc.gridwidth = 1
        for ((index, row) in rows.withIndex()) {
            gbc.gridy = index + 1
            gbc.gridx = 0
            gbc.weightx = 0.0
            content.add(JBLabel(row.originalName), gbc)

            gbc.gridx = 1
            gbc.weightx = 1.0
            content.add(row.field, gbc)
        }

        return JScrollPane(content).apply {
            preferredSize = Dimension(700, minOf(220, 40 + rows.size * 36))
            minimumSize = Dimension(520, 160)
            border = null
        }
    }

    override fun doValidate(): ValidationInfo? {
        val finalNames = rows.map { it.originalName }.toMutableSet()

        for (row in rows) {
            val newName = row.field.text.trim()
            if (newName.isEmpty() || newName == row.originalName) {
                continue
            }

            if (!isValidPlaceholderName(newName)) {
                return ValidationInfo(
                    "Placeholder names must be identifier-like (e.g. name, param0, _value) or numeric-only.",
                    row.field)
            }

            finalNames.remove(row.originalName)
            if (!finalNames.add(newName)) {
                return ValidationInfo("Placeholder '$newName' would collide with another placeholder.", row.field)
            }
        }

        return null
    }

    private fun isValidPlaceholderName(name: String): Boolean {
        val first = name.firstOrNull() ?: return false
        if (first.isDigit()) return name.all { it.isDigit() }
        if (!(first.isLetter() || first == '_')) return false
        return name.all { it.isLetterOrDigit() || it == '_' }
    }
}