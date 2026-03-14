package com.jetbrains.rider.plugins.arbnet

import com.intellij.codeInsight.daemon.DaemonCodeAnalyzer
import com.intellij.codeInsight.intention.IntentionAction
import com.intellij.lang.annotation.AnnotationHolder
import com.intellij.lang.annotation.ExternalAnnotator
import com.intellij.lang.annotation.HighlightSeverity
import com.intellij.openapi.editor.DefaultLanguageHighlighterColors
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiFile
import com.jetbrains.rd.ide.model.ArbNewKey
import com.jetbrains.rd.ide.model.arbModel
import com.jetbrains.rd.util.lifetime.Lifetime
import com.jetbrains.rider.projectView.solution
import javax.swing.SwingUtilities

/**
 * Quick fix + intention action that creates a new ARB key from a XAML markup extension
 * that references a non-existent key. Adds an empty entry to all locale .arb files in
 * the directory and opens the ARB editor pre-filtered to the new key.
 */
class GenerateArbKeyFix(private val key: String, private val projectDir: String) : IntentionAction {

    override fun getText(): String = "Generate ARB key '$key'"
    override fun getFamilyName(): String = "Arb.NET"
    override fun isAvailable(project: Project, editor: Editor?, file: PsiFile?): Boolean = true
    override fun startInWriteAction(): Boolean = false

    override fun invoke(project: Project, editor: Editor?, file: PsiFile?) {
        val arbDir = resolveArbDirectory(projectDir)
        val arbKey = key[0].lowercaseChar() + key.substring(1)  // PascalCase → camelCase

        project.solution.arbModel.addArbKey
            .start(Lifetime.Eternal, ArbNewKey(arbDir, arbKey))
            .result.advise(Lifetime.Eternal) { result ->
                try {
                    if (result.unwrap()) {
                        SwingUtilities.invokeLater {
                            // Re-run annotations on the current file so the error squiggle
                            // disappears immediately without requiring a file edit.
                            if (file != null) {
                                DaemonCodeAnalyzer.getInstance(project).restart(file, "New ARB key added")
                            }
                            openArbEditorAtKey(project, arbDir, key)
                        }
                    }
                } catch (_: Throwable) {
                    // Backend error — silently ignore; user can add manually
                }
            }
    }
}

/** Holds the result of [ArbKeyAnnotator.doAnnotate]. */
data class AnnotationData(
    val knownKeyRanges: List<TextRange>,
    val unknownKeys: List<UnknownKeyInfo>
)

/** A key reference that was not found in any .arb file. */
data class UnknownKeyInfo(val range: TextRange, val key: String, val projectDir: String)

/**
 * Colors known ARB key names inside XAML markup extensions like `{ext:Arb AppTitle}`, and
 * produces ERROR annotations for unknown keys with a "Generate ARB key" quick fix.
 *
 * Uses ExternalAnnotator instead of Annotator because Rider XAML files (RiderFileImpl)
 * only expose LeafPsiElement and RiderFileImpl to the Annotator pipeline — never
 * XmlAttribute. ExternalAnnotator operates on the file text directly, bypassing
 * the PSI element type restriction.
 *
 * LocalInspectionTool with XmlElementVisitor is also not called for Rider XAML files
 * (same root cause), so the unknown-key error reporting is done here instead.
 */
class ArbKeyAnnotator : ExternalAnnotator<ArbKeyAnnotator.FileInfo?, AnnotationData>() {

    data class FileInfo(
        val text: String,
        val projectDir: String,
        val project: com.intellij.openapi.project.Project
    )

    /** Called on the EDT | non blocking. */
    override fun collectInformation(file: PsiFile, editor: Editor, hasErrors: Boolean): FileInfo? {
        val vFile = file.virtualFile ?: return null
        if (vFile.extension?.lowercase() != "xaml") return null
        val projectDir = findProjectDirFromVFile(vFile) ?: return null
        return FileInfo(editor.document.text, projectDir, file.project)
    }

    /** Called on a background thread | blocking. */
    override fun doAnnotate(collectedInfo: FileInfo?): AnnotationData {
        collectedInfo ?: return AnnotationData(emptyList(), emptyList())

        val keyRetrievalService = ArbKeyRetrievalService.getInstance(collectedInfo.project)
        val keys = keyRetrievalService.getKeysBlocking(collectedInfo.projectDir, Lifetime.Eternal, timeoutMs = 2000)

        val keyNames = keys.map { it.key }.toSet()
        val knownRanges = mutableListOf<TextRange>()
        val unknownKeys = mutableListOf<UnknownKeyInfo>()

        var lineStart = 0
        for (line in collectedInfo.text.lines()) {
            for (match in ARB_KEY_IN_LINE.findAll(line)) {
                val keyGroup = match.groups[1] ?: continue
                val key = keyGroup.value
                val start = lineStart + keyGroup.range.first
                val end = lineStart + keyGroup.range.last + 1
                val range = TextRange(start, end)

                if (key in keyNames) {
                    knownRanges.add(range)
                } else {
                    unknownKeys.add(UnknownKeyInfo(range, key, collectedInfo.projectDir))
                }
            }
            lineStart += line.length + 1  // +1 for the newline character
        }

        return AnnotationData(knownRanges, unknownKeys)
    }

    /** Called on the EDT. */
    override fun apply(file: PsiFile, data: AnnotationData, holder: AnnotationHolder) {
        for (range in data.knownKeyRanges) {
            holder.newSilentAnnotation(HighlightSeverity.INFORMATION)
                .range(range)
                .textAttributes(
                    TextAttributesKey.createTextAttributesKey(
                        "ARB_KEY_REF",
                        DefaultLanguageHighlighterColors.INSTANCE_FIELD
                    )
                )
                .create()
        }

        for (info in data.unknownKeys) {
            holder.newAnnotation(HighlightSeverity.ERROR, "Unknown ARB key '${info.key}'")
                .range(info.range)
                .withFix(GenerateArbKeyFix(info.key, info.projectDir))
                .create()
        }
    }
}