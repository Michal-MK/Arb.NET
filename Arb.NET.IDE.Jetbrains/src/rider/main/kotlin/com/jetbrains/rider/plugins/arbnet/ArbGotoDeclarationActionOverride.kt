package com.jetbrains.rider.plugins.arbnet

import com.intellij.codeInsight.navigation.actions.GotoDeclarationAction
import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.testFramework.LightVirtualFile

/**
 * Overrides the platform Go To Declaration action by id, so custom keymaps are preserved.
 *
 * If caret is inside `{*:Arb KeyName}` in XAML, opens the Arb.NET editor.
 * Otherwise delegates to the original platform goto declaration action.
 */
class ArbGotoDeclarationActionOverride : AnAction() {

    private val delegate = GotoDeclarationAction()

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT

    override fun update(e: AnActionEvent) {
        delegate.update(e)
        val editor = e.project?.let { FileEditorManager.getInstance(it).selectedTextEditor }
        if (editor != null && findArbContextInEditor(editor) != null) {
            e.presentation.isEnabledAndVisible = true
        }
    }

    override fun actionPerformed(e: AnActionEvent) {
        val editor = e.project?.let { FileEditorManager.getInstance(it).selectedTextEditor }
        val context = editor?.let { findArbContextInEditor(it) }
        if (context != null) {
            val lightFile = LightVirtualFile(ArbEditor.FILE_NAME).apply {
                putUserData(ArbEditor.INITIAL_DIR_KEY, context.arbDir)
            }
            FileEditorManager.getInstance(context.project).openFile(lightFile, true)
            return
        }

        delegate.actionPerformed(e)
    }
}
