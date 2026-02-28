package com.jetbrains.rider.plugins.arbnet

import com.intellij.lang.annotation.AnnotationHolder
import com.intellij.lang.annotation.ExternalAnnotator
import com.intellij.lang.annotation.HighlightSeverity
import com.intellij.openapi.editor.DefaultLanguageHighlighterColors
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiFile
import com.jetbrains.rd.util.lifetime.Lifetime

/**
 * Colors ARB key names inside XAML markup extensions like `{ext:Arb AppTitle}`.
 * The key name is highlighted using the "instance field" text attribute so it
 * visually resembles a binding path property rather than plain string content.
 *
 * Uses ExternalAnnotator instead of Annotator because Rider XAML files (RiderFileImpl)
 * only expose LeafPsiElement and RiderFileImpl to the Annotator pipeline — never
 * XmlAttribute. ExternalAnnotator operates on the file text directly, bypassing
 * the PSI element type restriction.
 *
 * collectionInfo: VirtualFile? (the XAML file, or null to skip)
 * doAnnotate:     List<TextRange> of key name ranges to highlight
 */
class ArbKeyAnnotator : ExternalAnnotator<ArbKeyAnnotator.FileInfo?, List<TextRange>>() {

    data class FileInfo(
        val text: String,
        val projectDir: String,
        val project: com.intellij.openapi.project.Project
    )

    /** Called on the EDT — collect lightweight info only, no blocking. */
    override fun collectInformation(file: PsiFile, editor: Editor, hasErrors: Boolean): FileInfo? {
        val vFile = file.virtualFile ?: return null
        if (vFile.extension?.lowercase() != "xaml") return null
        val projectDir = findProjectDirFromVFile(vFile) ?: return null
        return FileInfo(editor.document.text, projectDir, file.project)
    }

    /** Called on a background thread — may block to fetch keys. */
    override fun doAnnotate(collectedInfo: FileInfo?): List<TextRange> {
        collectedInfo ?: return emptyList()

        val cache = ArbKeyCache.getInstance(collectedInfo.project)
        val keys = cache.getKeysBlocking(collectedInfo.projectDir, Lifetime.Eternal, timeoutMs = 2000)
        if (keys.isEmpty()) return emptyList()

        val keyNames = keys.map { it.key }.toSet()
        val ranges = mutableListOf<TextRange>()

        // Scan every line of the file for {*:Arb KeyName} patterns
        var lineStart = 0
        for (line in collectedInfo.text.lines()) {
            for (match in ARB_KEY_IN_LINE.findAll(line)) {
                val keyGroup = match.groups[1] ?: continue
                val key = keyGroup.value
                if (key in keyNames) {
                    val start = lineStart + keyGroup.range.first
                    val end = lineStart + keyGroup.range.last + 1
                    ranges.add(TextRange(start, end))
                }
            }
            lineStart += line.length + 1  // +1 for the newline character
        }

        return ranges
    }

    /** Called on the EDT — apply the highlighting annotations. */
    override fun apply(file: PsiFile, ranges: List<TextRange>, holder: AnnotationHolder) {
        for (range in ranges) {
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
    }
}