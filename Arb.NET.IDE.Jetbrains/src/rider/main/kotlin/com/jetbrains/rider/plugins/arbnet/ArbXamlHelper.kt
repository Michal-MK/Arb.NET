package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.editor.Editor
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.TextRange
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.psi.PsiElement
import java.io.File

// TODO fragile: assumes explicit namespace prefix, does not handle implicit/global XAML namespaces

/**
 * Matches a complete or partial `{*:Arb KeyName}` expression anywhere in a line of text
 * and captures the key name in group 1. Used for caret-position detection in goto and annotator.
 */
val ARB_KEY_IN_LINE = Regex("""\{[^:}]+:Arb\s+(\w+)}?""")

// Requires a closing `}` so it returns null while the user is still typing the key name.
private val ARB_KEY_CAPTURE = Regex("""^\{[^:}]+:Arb\s+(\w+)}$""")

/**
 * Extracts the key name from a complete markup extension value like `{ext:Arb AppTitle}`.
 * Returns null if the value is incomplete (no closing `}`) or is not an ARB expression.
 */
fun extractArbKey(value: String): String? =
    ARB_KEY_CAPTURE.find(value.trim())?.groupValues?.get(1)

/**
 * Walks the VFS ancestor chain upward from [element]'s file to find the nearest directory
 * that contains an `l10n.yaml` file. Returns the absolute path of that directory, or null.
 */
fun findProjectDir(element: PsiElement): String? =
    findProjectDirFromVFile(element.containingFile?.virtualFile)

/**
 * Walks the VFS ancestor chain upward from [vFile] to find the nearest directory
 * that contains an `l10n.yaml` file. Use this when the PSI element's virtualFile may be null
 * (e.g. Rider XAML nodes), passing the file's VirtualFile directly instead.
 */
fun findProjectDirFromVFile(vFile: VirtualFile?): String? {
    var dir = vFile?.parent ?: return null
    // TODO magic constant
    repeat(10) {
        // TODO magic constant
        if (dir.children.any { it.name == "l10n.yaml" }) return dir.path
        dir = dir.parent ?: return null
    }
    return null
}

/**
 * Resolves the ARB directory from l10n.yaml in [projectDir].
 * Falls back to [projectDir] if l10n.yaml is missing or arb-dir is not specified.
 */
fun resolveArbDirectory(projectDir: String): String {
    return try {
        // TODO magic constant
        val l10n = File(projectDir, "l10n.yaml")
        if (!l10n.exists()) return projectDir

        val arbDirLine = l10n.readLines()
            .map { it.trim().trimStart('\uFEFF') }
            .firstOrNull { it.startsWith("arb-dir:") }
            ?: return projectDir

        val configured = arbDirLine.substringAfter(':').trim().trim('"', '\'')
        if (configured.isEmpty()) return projectDir

        val arbDir = File(projectDir, configured)
        if (arbDir.exists()) arbDir.absolutePath.replace('\\', '/') else projectDir
    } catch (_: Throwable) {
        projectDir
    }
}

/** Resolved context for a caret position inside a `{*:Arb KeyName}` expression in XAML. */
data class ArbContext(
    val project: Project,
    val arbDir: String,
    val keyName: String
)

/**
 * Returns [ArbContext] when the caret in [editor] is inside a `{*:Arb KeyName}` expression
 * in a XAML file, or null otherwise.
 */
fun findArbContextInEditor(editor: Editor): ArbContext? {
    val vFile = FileDocumentManager.getInstance().getFile(editor.document) ?: return null
    if (!vFile.name.endsWith(".xaml", ignoreCase = true)) return null

    val offset = editor.caretModel.offset
    val doc = editor.document
    val line = doc.getLineNumber(offset)
    val lineStart = doc.getLineStartOffset(line)
    val lineEnd = doc.getLineEndOffset(line)
    val lineText = doc.getText(TextRange(lineStart, lineEnd))
    val col = offset - lineStart

    val match = ARB_KEY_IN_LINE.findAll(lineText).firstOrNull { m -> col in m.range } ?: return null

    val projectDir = findProjectDirFromVFile(vFile) ?: return null
    val project = editor.project ?: return null
    return ArbContext(project, resolveArbDirectory(projectDir), match.groupValues[1])
}