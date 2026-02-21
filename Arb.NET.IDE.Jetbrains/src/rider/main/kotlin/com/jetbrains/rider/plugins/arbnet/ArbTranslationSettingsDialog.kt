package com.jetbrains.rider.plugins.arbnet

import com.intellij.ide.util.PropertiesComponent
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.DialogWrapper
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBPasswordField
import com.intellij.ui.components.JBTextArea
import com.intellij.ui.components.JBTextField
import java.awt.GridBagConstraints
import java.awt.GridBagLayout
import java.awt.Insets
import javax.swing.JComponent
import javax.swing.JPanel
import javax.swing.JScrollPane

class ArbTranslationSettingsDialog(project: Project) : DialogWrapper(project, true) {

    private val endpointField = JBTextField()
    private val deploymentField = JBTextField()
    private val apiKeyField = JBPasswordField()
    private val customPromptArea = JBTextArea(3, 40)
    private val temperatureField = JBTextField(6)

    init {
        title = "AI Translation Settings"
        isResizable = false
        init()

        val props = PropertiesComponent.getInstance()
        endpointField.text = props.getValue(ArbEditor.SETTINGS_ENDPOINT, "")
        deploymentField.text = props.getValue(ArbEditor.SETTINGS_DEPLOYMENT, "")
        apiKeyField.text = props.getValue(ArbEditor.SETTINGS_API_KEY, "")
        customPromptArea.text = props.getValue(ArbEditor.SETTINGS_PROMPT, "")
        temperatureField.text = props.getValue(ArbEditor.SETTINGS_TEMPERATURE, "0.2")
    }

    override fun createCenterPanel(): JComponent {
        val panel = JPanel(GridBagLayout())
        val gbc = GridBagConstraints().apply {
            fill = GridBagConstraints.HORIZONTAL
            insets = Insets(4, 4, 4, 4)
        }

        fun addRow(label: String, field: JComponent, row: Int, isScrolled: Boolean = false) {
            gbc.gridx = 0; gbc.gridy = row; gbc.weightx = 0.0; gbc.gridwidth = 1
            panel.add(JBLabel(label), gbc)
            gbc.gridx = 1; gbc.weightx = 1.0
            val comp: JComponent = if (isScrolled) JScrollPane(field).also { it.preferredSize = java.awt.Dimension(380, 68) } else field
            panel.add(comp, gbc)
        }

        addRow("Endpoint URL:", endpointField, 0)
        addRow("Deployment name:", deploymentField, 1)
        addRow("API key:", apiKeyField, 2)
        addRow("Custom prompt (optional):", customPromptArea, 3, isScrolled = true)
        addRow("Temperature:", temperatureField, 4)

        return panel
    }

    override fun doOKAction() {
        val props = PropertiesComponent.getInstance()
        props.setValue(ArbEditor.SETTINGS_ENDPOINT, endpointField.text.trim().trimEnd('/'))
        props.setValue(ArbEditor.SETTINGS_DEPLOYMENT, deploymentField.text.trim())
        props.setValue(ArbEditor.SETTINGS_API_KEY, String(apiKeyField.password))
        props.setValue(ArbEditor.SETTINGS_PROMPT, customPromptArea.text.trim())
        val temp = temperatureField.text.trim().toFloatOrNull() ?: 0.2f
        props.setValue(ArbEditor.SETTINGS_TEMPERATURE, temp.toString())
        super.doOKAction()
    }
}
