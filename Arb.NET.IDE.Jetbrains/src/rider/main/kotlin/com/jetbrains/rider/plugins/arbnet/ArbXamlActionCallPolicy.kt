package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.psi.PsiElement
import com.jetbrains.rdclient.hyperlinks.FrontendCtrlClickHost
import com.jetbrains.rider.actions.RiderActionCallStrategy
import com.jetbrains.rider.actions.RiderActionSupportPolicy
import com.jetbrains.rider.actions.RiderActions

/**
 * Routes (Go To Declaration) for XAML files:
 * - When the caret is inside an ARB markup extension (`{*:Arb KeyName}`), handle
 *   the action on the frontend so [ArbGotoDeclarationActionOverride] intercepts it.
 * - Otherwise delegate to the backend (normal Rider XAML goto behaviour).
 */
class ArbXamlActionCallPolicy : RiderActionSupportPolicy() {

    override fun isAvailable(psiElement: PsiElement, backendActionId: String): Boolean {
        val containingFile = psiElement.containingFile ?: return false
        val vFile = containingFile.virtualFile
        return vFile?.extension?.lowercase() == "xaml" ||
                containingFile.name.endsWith(".xaml", ignoreCase = true)
    }

    override fun getCallStrategy(psiElement: PsiElement, backendActionId: String): RiderActionCallStrategy {
        val isGotoAction = backendActionId == RiderActions.GOTO_DECLARATION ||
                backendActionId == FrontendCtrlClickHost.backendActionId

        val editor = FileEditorManager.getInstance(psiElement.project).selectedTextEditor
        if (isGotoAction && editor != null && findArbContextInEditor(editor) != null) {
            return RiderActionCallStrategy.FRONTEND_ONLY
        }

        return RiderActionCallStrategy.BACKEND_FIRST
    }
}