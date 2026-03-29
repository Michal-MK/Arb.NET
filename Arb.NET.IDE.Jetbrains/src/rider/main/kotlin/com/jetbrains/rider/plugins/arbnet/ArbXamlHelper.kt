package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.editor.Editor
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.TextRange
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.psi.PsiElement
import java.io.File

/**
 * Matches a complete or partial `{Arb KeyName}` or `{ext:Arb KeyName}` expression anywhere in
 * a line of text and captures the key name in group 1. Supports both explicit namespace prefixes
 * and implicit global XAML namespaces. Used for caret-position detection in goto and annotator.
 */
val ARB_KEY_IN_LINE = Regex("""\{(?:[^:}]+:)?Arb\s+(\w+)}?""")

// Requires a closing `}` so it returns null while the user is still typing the key name.
private val ARB_KEY_CAPTURE = Regex("""^\{(?:[^:}]+:)?Arb\s+(\w+)}$""")
private val COMMAND_BINDING = Regex("""Command\s*=\s*(['"])(\{Binding.*?})\1""", setOf(RegexOption.DOT_MATCHES_ALL))
private val BINDING_PATH = Regex("""(?:^|[\s,])Path\s*=\s*([\w.]+Command)\b""", RegexOption.IGNORE_CASE)
private val IMPLICIT_BINDING_PATH = Regex("""^\{Binding\s+([\w.]+Command)\b""", RegexOption.IGNORE_CASE)
private val XMLNS_DECLARATION = Regex("""xmlns:(\w+)\s*=\s*(['"])([^'"]+)\2""")
private val DATA_TYPE = Regex("""x:DataType\s*=\s*(['"])([^'"]+)\1""")
private val X_CLASS = Regex("""x:Class\s*=\s*(['"])([^'"]+)\1""")
private val RELAY_COMMAND_METHOD = Regex(
    """\[\s*RelayCommand(?:Attribute)?(?:\s*\([^\)]*\))?\s*](?:\s*\[[^\]]+])*\s*(?:(?:public|private|protected|internal|static|virtual|sealed|override|async|partial|new|extern)\s+)*[^\r\n\(]+?\s+(\w+)\s*\(""",
    setOf(RegexOption.DOT_MATCHES_ALL)
)
private val FILE_SCOPED_NAMESPACE = Regex("""^\s*namespace\s+([\w.]+)\s*;""", RegexOption.MULTILINE)
private val BLOCK_NAMESPACE = Regex("""^\s*namespace\s+([\w.]+)\s*\{""", RegexOption.MULTILINE)
private val CLASS_DECLARATION = Regex("""\b(?:partial\s+)?class\s+(\w+)\b""")

/**
 * Extracts the key name from a complete markup extension value like `{Arb AppTitle}`
 * or `{ext:Arb AppTitle}`.
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

/**
 * Opens (or focuses) the singleton ARB editor and navigates to [keyFilter].
 * Must be called on the EDT.
 */
fun openArbEditorAtKey(project: Project, arbDir: String, keyFilter: String) {
    val editorManager = FileEditorManager.getInstance(project)
    val singletonFile = ArbEditor.getOrCreateFile(project)
    val existing = editorManager.getEditors(singletonFile).filterIsInstance<ArbEditor>().firstOrNull()
    if (existing != null) {
        existing.navigateTo(arbDir, keyFilter)
        editorManager.openFile(singletonFile, true)
    } else {
        singletonFile.putUserData(ArbEditor.INITIAL_DIR_KEY, arbDir)
        singletonFile.putUserData(ArbEditor.INITIAL_FILTER_KEY, keyFilter)
        editorManager.openFile(singletonFile, true)
    }
}

/** Resolved context for a caret position inside a `{Arb KeyName}` or `{*:Arb KeyName}` expression in XAML. */
data class ArbContext(
    val project: Project,
    val arbDir: String,
    val keyName: String
)

data class RelayCommandTarget(
    val filePath: String,
    val lineNumber: Int,
    val column: Int
)

/**
 * Returns [ArbContext] when the caret in [editor] is inside a `{Arb KeyName}` or
 * `{*:Arb KeyName}` expression in a XAML file, or null otherwise.
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

fun findRelayCommandTargetInEditor(editor: Editor): RelayCommandTarget? {
    val vFile = FileDocumentManager.getInstance().getFile(editor.document) ?: return null
    if (!vFile.name.endsWith(".xaml", ignoreCase = true)) return null

    val text = editor.document.text
    val offset = editor.caretModel.offset
    val methodName = findRelayCommandMethodName(text, offset) ?: return null
    val xClass = lastAttributeValueBeforeOffset(X_CLASS, text, offset)
    val namespaces = xmlNamespaces(text)
    val preferredType = resolveTypeName(lastAttributeValueBeforeOffset(DATA_TYPE, text, offset), namespaces, xClass)
    val fallbackType = resolveTypeName(xClass, namespaces, xClass)
    val companionFile = vFile.path + ".cs"
    val projectDir = findNearestCsprojDir(vFile) ?: return null
    val candidates = findRelayCommandCandidates(projectDir, methodName)
    if (candidates.isEmpty()) return null

    val ranked = candidates
        .map { candidate -> candidate to scoreCandidate(candidate, preferredType, fallbackType, companionFile) }
        .sortedWith(compareByDescending<Pair<Candidate, Int>> { it.second }.thenBy { it.first.filePath })

    if (ranked.size > 1 && ranked[0].second == ranked[1].second) return null

    return RelayCommandTarget(ranked[0].first.filePath, ranked[0].first.lineNumber, ranked[0].first.column)
}

private fun findRelayCommandMethodName(text: String, offset: Int): String? {
    for (match in COMMAND_BINDING.findAll(text)) {
        val expression = match.groups[2] ?: continue
        if (offset < expression.range.first || offset > expression.range.last + 1) continue

        val path = extractBindingPath(expression.value) ?: return null
        val commandName = path.substringAfterLast('.')
        if (!commandName.endsWith("Command") || commandName.length <= "Command".length) return null
        return commandName.removeSuffix("Command")
    }

    return null
}

private fun extractBindingPath(expression: String): String? {
    val namedPath = BINDING_PATH.find(expression)
    if (namedPath != null) return namedPath.groupValues[1]

    val implicitPath = IMPLICIT_BINDING_PATH.find(expression.trim())
    return implicitPath?.groupValues?.get(1)
}

private fun lastAttributeValueBeforeOffset(regex: Regex, text: String, offset: Int): String? {
    var value: String? = null
    for (match in regex.findAll(text)) {
        if (match.range.first > offset) break
        value = match.groupValues[2]
    }

    return value
}

private fun xmlNamespaces(text: String): Map<String, String> {
    val result = mutableMapOf<String, String>()
    for (match in XMLNS_DECLARATION.findAll(text)) {
        result[match.groupValues[1]] = match.groupValues[3]
    }

    return result
}

private fun resolveTypeName(rawType: String?, namespaces: Map<String, String>, xClass: String?): String? {
    val trimmed = rawType?.trim()?.takeIf { it.isNotEmpty() && !it.startsWith('{') } ?: return null
    val separator = trimmed.indexOf(':')
    if (separator > 0) {
        val prefix = trimmed.substring(0, separator)
        val typeName = trimmed.substring(separator + 1)
        val nsValue = namespaces[prefix] ?: return null
        val resolvedNamespace = when {
            nsValue.startsWith("clr-namespace:") -> nsValue.removePrefix("clr-namespace:").substringBefore(';').trim()
            nsValue.startsWith("using:") -> nsValue.removePrefix("using:").trim()
            else -> return null
        }
        return "$resolvedNamespace.$typeName"
    }

    if (trimmed.contains('.')) return trimmed
    val fallbackNamespace = xClass?.substringBeforeLast('.', "")?.takeIf { it.isNotBlank() }
    return if (fallbackNamespace == null) trimmed else "$fallbackNamespace.$trimmed"
}

private fun findNearestCsprojDir(vFile: VirtualFile?): String? {
    var dir = vFile?.parent ?: return null
    repeat(10) {
        val hasProjectFile = dir.children.any { child -> child.extension.equals("csproj", ignoreCase = true) }
        if (hasProjectFile) return dir.path
        dir = dir.parent ?: return null
    }

    return null
}

private fun findRelayCommandCandidates(projectDir: String, methodName: String): List<Candidate> {
    val root = File(projectDir)
    if (!root.exists()) return emptyList()

    return root.walkTopDown()
        .onEnter { dir ->
            val name = dir.name.lowercase()
            name != "bin" && name != "obj"
        }
        .filter { file -> file.isFile && file.extension.equals("cs", ignoreCase = true) }
        .flatMap { file ->
            val content = try {
                file.readText()
            } catch (_: Throwable) {
                return@flatMap emptySequence<Candidate>()
            }

            RELAY_COMMAND_METHOD.findAll(content)
                .filter { match -> match.groupValues[1] == methodName }
                .map { match ->
                    val methodIndex = match.groups[1]?.range?.first ?: 0
                    Candidate(
                        filePath = file.absolutePath.replace('\\', '/'),
                        namespaceName = namespaceName(content),
                        className = classNameForPosition(content, methodIndex),
                        lineNumber = lineNumberForOffset(content, methodIndex),
                        column = columnForOffset(content, methodIndex)
                    )
                }
        }
        .toList()
}

private fun namespaceName(content: String): String? =
    FILE_SCOPED_NAMESPACE.find(content)?.groupValues?.get(1)
        ?: BLOCK_NAMESPACE.find(content)?.groupValues?.get(1)

private fun classNameForPosition(content: String, position: Int): String? {
    var className: String? = null
    for (match in CLASS_DECLARATION.findAll(content)) {
        if (match.range.first >= position) break
        className = match.groupValues[1]
    }
    return className
}

private fun lineNumberForOffset(content: String, position: Int): Int =
    content.take(position.coerceAtMost(content.length)).count { it == '\n' }

private fun columnForOffset(content: String, position: Int): Int {
    val pos = position.coerceAtMost(content.length)
    val lastNewline = content.lastIndexOf('\n', pos - 1)
    return pos - lastNewline - 1
}

private fun scoreCandidate(candidate: Candidate, preferredType: String?, fallbackType: String?, companionFile: String): Int {
    var score = 0
    val candidateType = candidate.fullyQualifiedTypeName()
    if (preferredType != null && candidateType == preferredType) score += 100
    if (fallbackType != null && candidateType == fallbackType) score += 50
    if (candidate.filePath.equals(companionFile.replace('\\', '/'), ignoreCase = true)) score += 25
    if (preferredType != null && candidate.className == preferredType.substringAfterLast('.')) score += 10
    return score
}

private data class Candidate(
    val filePath: String,
    val namespaceName: String?,
    val className: String?,
    val lineNumber: Int,
    val column: Int
) {
    fun fullyQualifiedTypeName(): String? = when {
        className.isNullOrBlank() -> null
        namespaceName.isNullOrBlank() -> className
        else -> "$namespaceName.$className"
    }
}

fun openRelayCommandTarget(project: Project, target: RelayCommandTarget) {
    val vFile = LocalFileSystem.getInstance().findFileByPath(target.filePath) ?: return
    OpenFileDescriptor(project, vFile, target.lineNumber, target.column).navigate(true)
}