package com.jetbrains.rider.plugins.arbnet

import com.intellij.codeInsight.daemon.DaemonCodeAnalyzer
import com.intellij.codeInsight.intention.IntentionAction
import com.intellij.codeInsight.intention.PriorityAction
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.project.Project
import com.intellij.psi.PsiFile
import com.jetbrains.rd.ide.model.ArbNewKey
import com.jetbrains.rd.ide.model.arbModel
import com.jetbrains.rd.util.lifetime.Lifetime
import com.jetbrains.rider.projectView.solution
import javax.swing.SwingUtilities

/**
 * Intention action for C# files: when the caret is on a PascalCase identifier that is
 * not a known ARB key in the nearest project directory, offers
 * "Generate ARB key 'X' and open editor".
 */
class ArbCSharpGenerateKeyIntention : IntentionAction, PriorityAction {

    override fun getText(): String = _displayText ?: "Generate ARB key and open editor"
    override fun getFamilyName(): String = "Arb.NET"
    override fun getPriority(): PriorityAction.Priority = PriorityAction.Priority.TOP
    override fun startInWriteAction(): Boolean = false

    private var _displayText: String? = null
    private var _keyName: String? = null
    private var _projectDir: String? = null

    override fun isAvailable(project: Project, editor: Editor?, file: PsiFile?): Boolean {
        editor ?: return false
        file ?: return false
        if (file.virtualFile?.extension?.lowercase() != "cs") return false

        val projectDir = findProjectDirFromVFile(file.virtualFile) ?: return false

        val identifier = identifierAtCaret(editor) ?: return false

        if (identifier.isEmpty() || !identifier[0].isUpperCase()) return false

        val keys = ArbKeyRetrievalService.getInstance(project).getKeysBlocking(projectDir, Lifetime.Eternal)

        if (keys.isEmpty()) return false

        if (keys.any { it.key == identifier }) return false

        _keyName = identifier
        _projectDir = projectDir
        _displayText = "Generate ARB key '$identifier' and open editor"
        return true
    }

    override fun invoke(project: Project, editor: Editor?, file: PsiFile?) {
        val key = _keyName ?: return
        val projectDir = _projectDir ?: return

        val arbKey = key[0].lowercaseChar() + key.substring(1)
        val arbDir = resolveArbDirectory(projectDir)

        val model = project.solution.arbModel

        model.addArbKey
            .start(Lifetime.Eternal, ArbNewKey(arbDir, arbKey))
            .result.advise(Lifetime.Eternal) { result ->
                try {
                    if (result.unwrap()) {
                        SwingUtilities.invokeLater {
                            if (file != null) {
                                DaemonCodeAnalyzer.getInstance(project).restart(file, "New ARB key added")
                            }
                        }
                    }
                } catch (_: Throwable) { }
            }

        SwingUtilities.invokeLater {
            openArbEditorAtKey(project, arbDir, key)
        }
    }

    /**
     * Returns the identifier token under the caret, or null if the caret is not
     * on a word character.
     */
    private fun identifierAtCaret(editor: Editor): String? {
        val offset = editor.caretModel.offset
        val doc = editor.document
        val text = doc.immutableCharSequence

        if (offset >= text.length) return null

        // Expand left and right to word boundaries
        var start = offset
        while (start > 0 && (text[start - 1].isLetterOrDigit() || text[start - 1] == '_')) start--
        var end = offset
        while (end < text.length && (text[end].isLetterOrDigit() || text[end] == '_')) end++

        if (start == end) return null
        return text.subSequence(start, end).toString()
    }
}