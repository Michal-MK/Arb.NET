package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.fileEditor.FileEditor
import com.intellij.openapi.fileEditor.FileEditorPolicy
import com.intellij.openapi.fileEditor.FileEditorProvider
import com.intellij.openapi.project.DumbAware
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.testFramework.LightVirtualFile

class ArbEditorProvider : FileEditorProvider, DumbAware {

    override fun accept(project: Project, file: VirtualFile): Boolean =
        file is LightVirtualFile && file.name == ArbEditor.FILE_NAME

    override fun createEditor(project: Project, file: VirtualFile): FileEditor =
        ArbEditor(project, file)

    override fun getEditorTypeId(): String = "arb-net-editor"

    override fun getPolicy(): FileEditorPolicy = FileEditorPolicy.HIDE_DEFAULT_EDITOR
}
