package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.fileEditor.FileEditor
import com.intellij.openapi.fileEditor.FileEditorState
import com.intellij.openapi.util.UserDataHolderBase
import com.intellij.openapi.vfs.VirtualFile
import java.awt.Color
import java.beans.PropertyChangeListener
import javax.swing.JComponent
import javax.swing.JPanel

class ArbEditor(private val file: VirtualFile) : UserDataHolderBase(), FileEditor {

    companion object {
        const val FILE_NAME = "Arb.NET Editor"
    }

    private val panel = JPanel().apply {
        background = Color.BLACK
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
    override fun dispose() {}
}
