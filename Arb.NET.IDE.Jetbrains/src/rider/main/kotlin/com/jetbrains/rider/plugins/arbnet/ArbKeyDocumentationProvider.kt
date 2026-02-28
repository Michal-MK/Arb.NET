package com.jetbrains.rider.plugins.arbnet

import com.intellij.lang.documentation.DocumentationMarkup
import com.intellij.lang.documentation.DocumentationProvider
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.impl.FakePsiElement
import com.jetbrains.rd.util.lifetime.Lifetime

/**
 * Provides on-hover documentation for ARB key names inside XAML markup extensions.
 *
 * When the user hovers over a key like `AppTitle` in `{ext:Arb AppTitle}`, the
 * quick-doc popup shows the key's description from the .arb metadata.
 *
 * [getCustomDocumentationElement] is the entry point Rider XAML calls for hover —
 * it returns a synthetic [ArbKeyElement] that carries the key name and its resolved
 * [com.jetbrains.rd.ide.model.ArbKeyInfo]-derived documentation string. [generateDoc] then reads the pre-built
 * HTML back out of that element, avoiding a second cache lookup.
 */
class ArbKeyDocumentationProvider : DocumentationProvider {

    override fun getCustomDocumentationElement(
        editor: Editor,
        file: PsiFile,
        contextElement: PsiElement?,
        targetOffset: Int
    ): PsiElement? {
        val vFile = file.virtualFile ?: return null
        if (vFile.extension?.lowercase() != "xaml") return null

        val doc = editor.document
        val line = doc.getLineNumber(targetOffset)
        val lineStart = doc.getLineStartOffset(line)
        val lineEnd = doc.getLineEndOffset(line)
        val lineText = doc.getText(TextRange(lineStart, lineEnd))
        val col = targetOffset - lineStart

        val match = ARB_KEY_IN_LINE.findAll(lineText)
            .firstOrNull { m -> col in m.range }
            ?: return null

        val key = match.groupValues[1]

        val projectDir = findProjectDirFromVFile(vFile) ?: return null
        val cache = ArbKeyCache.getInstance(file.project)
        val keys = cache.getKeysBlocking(projectDir, Lifetime.Eternal, timeoutMs = 1000)
        val keyInfo = keys.find { it.key == key }

        // Build the doc HTML here so generateDoc just returns it without another cache hit.
        val html = buildDocHtml(key, keyInfo?.description, keyInfo?.isParametric ?: false)
        return ArbKeyElement(file, key, html)
    }

    override fun generateDoc(element: PsiElement?, originalElement: PsiElement?): String? {
        val arbElement = element as? ArbKeyElement ?: return null
        return arbElement.docHtml
    }

    override fun generateHoverDoc(element: PsiElement, originalElement: PsiElement?): String? =
        generateDoc(element, originalElement)

    private fun buildDocHtml(key: String, description: String?, isParametric: Boolean): String {
        val sb = StringBuilder()
        // DEFINITION_ELEMENT gives the bold header section matching IntelliJ's quick-doc style
        sb.append(DocumentationMarkup.DEFINITION_START)
        sb.append("<b>$key</b>")
        if (isParametric) sb.append("&nbsp;<i>(parametric)</i>")
        sb.append(DocumentationMarkup.DEFINITION_END)
        if (!description.isNullOrBlank()) {
            sb.append(DocumentationMarkup.CONTENT_START)
            sb.append(description)
            sb.append(DocumentationMarkup.CONTENT_END)
        }
        return sb.toString()
    }
}

/**
 * Synthetic PSI element carrying a resolved ARB key name and its pre-built documentation HTML.
 * Created by [ArbKeyDocumentationProvider.getCustomDocumentationElement], consumed by [ArbKeyDocumentationProvider.generateDoc].
 *
 * [canNavigate] returns false to suppress the "pencil / go to source" button in the
 * hover popup — there is no single source location to navigate to for an ARB key.
 */
internal class ArbKeyElement(
    private val file: PsiFile,
    val arbKey: String,
    val docHtml: String
) : FakePsiElement() {
    override fun getParent(): PsiElement = file
    override fun getName(): String = arbKey
    override fun canNavigate(): Boolean = false
    override fun canNavigateToSource(): Boolean = false
}