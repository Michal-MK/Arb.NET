package com.jetbrains.rider.plugins.arbnet

import com.intellij.ide.fileTemplates.FileTemplateManager
import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.LangDataKeys
import com.intellij.openapi.command.WriteCommandAction
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.ui.Messages
import com.intellij.openapi.vfs.VfsUtil
import com.intellij.psi.PsiDirectory
import java.util.Properties

class CreateL10nYamlAction : AnAction(
    "Arb.NET l10n.yaml",
    "Create a commented l10n.yaml configuration file for Arb.NET",
    null
) {

    companion object {
        private const val FILE_NAME = "l10n.yaml"
        private const val TEMPLATE_NAME = "Arb.NET l10n.yaml"
    }

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.EDT

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val directory = getTargetDirectory(e) ?: return

        if (directory.findFile(FILE_NAME) != null) {
            Messages.showInfoMessage(project, "The selected folder already contains l10n.yaml.", "Arb.NET")
            return
        }

        val template = FileTemplateManager.getInstance(project).getInternalTemplate(TEMPLATE_NAME)
        val content = template.getText(Properties())

        var createdPath: com.intellij.openapi.vfs.VirtualFile? = null
        WriteCommandAction.writeCommandAction(project)
            .withName("Create l10n.yaml")
            .run<Throwable> {
                val virtualFile = directory.virtualFile.createChildData(this, FILE_NAME)
                VfsUtil.saveText(virtualFile, content)
                createdPath = virtualFile
            }

        createdPath?.let { FileEditorManager.getInstance(project).openFile(it, true) }
    }

    override fun update(e: AnActionEvent) {
        val directory = getTargetDirectory(e)
        e.presentation.isEnabledAndVisible = directory != null && directory.findFile(FILE_NAME) == null
    }

    private fun getTargetDirectory(e: AnActionEvent): PsiDirectory? =
        e.getData(LangDataKeys.IDE_VIEW)?.directories?.firstOrNull()
}