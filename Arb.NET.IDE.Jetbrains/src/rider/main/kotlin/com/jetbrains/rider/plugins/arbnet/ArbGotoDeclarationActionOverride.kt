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
 * If caret is inside `{Arb KeyName}` or `{*:Arb KeyName}` in XAML, opens the Arb.NET editor.
 * Otherwise delegates to the original platform goto declaration action.
 */
class ArbGotoDeclarationActionOverride : AnAction() {

    private val delegate = GotoDeclarationAction()

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT

    override fun update(e: AnActionEvent) {
        delegate.update(e)
        val editor = e.project?.let { FileEditorManager.getInstance(it).selectedTextEditor }
        if (editor != null && (findArbContextInEditor(editor) != null || findRelayCommandTargetInEditor(editor) != null)) {
            e.presentation.isEnabledAndVisible = true
        }
    }

    override fun actionPerformed(e: AnActionEvent) {
        val editor = e.project?.let { FileEditorManager.getInstance(it).selectedTextEditor }
        val relayTarget = editor?.let { findRelayCommandTargetInEditor(it) }
        if (relayTarget != null) {
            openRelayCommandTarget(e.project ?: return, relayTarget)
            return
        }

        val context = editor?.let { findArbContextInEditor(it) }
        if (context != null) {
            val editorManager = FileEditorManager.getInstance(context.project)
            val singletonFile = ArbEditor.getOrCreateFile(context.project)
            val existingEditor = editorManager.getEditors(singletonFile).filterIsInstance<ArbEditor>().firstOrNull()
            if (existingEditor != null) {
                existingEditor.navigateTo(context.arbDir, context.keyName)
                editorManager.openFile(singletonFile, true)
            } else {
                singletonFile.putUserData(ArbEditor.INITIAL_DIR_KEY, context.arbDir)
                singletonFile.putUserData(ArbEditor.INITIAL_FILTER_KEY, context.keyName)
                editorManager.openFile(singletonFile, true)
            }
            return
        }

        delegate.actionPerformed(e)
    }
}
