package com.jetbrains.rider.plugins.arbnet

import com.intellij.lang.documentation.DocumentationMarkup
import com.intellij.lang.documentation.DocumentationProvider
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.impl.FakePsiElement
import com.jetbrains.rd.util.lifetime.Lifetime
import org.w3c.dom.Element
import org.w3c.dom.Node
import org.w3c.dom.NodeList
import javax.xml.parsers.DocumentBuilderFactory

/**
 * Provides on-hover documentation for ARB key names inside XAML markup extensions.
 *
 * When the user hovers over a key like `AppTitle` in `{ext:Arb AppTitle}`, the
 * quick-doc popup shows the same rich documentation as hovering over the generated
 * C# dispatcher property — including description and a locale-value table.
 *
 * If the generated dispatcher is unavailable (e.g. not yet built), falls back to
 * showing just the description from the .arb metadata.
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
        val html = if (keyInfo?.xmlDoc != null) {
            renderXmlDocToHtml(keyInfo.xmlDoc, key, keyInfo.isParametric)
        } else {
            buildFallbackDocHtml(key, keyInfo?.description, keyInfo?.isParametric ?: false)
        }
        return ArbKeyElement(file, key, html)
    }

    override fun generateDoc(element: PsiElement?, originalElement: PsiElement?): String? {
        val arbElement = element as? ArbKeyElement ?: return null
        return arbElement.docHtml
    }

    override fun generateHoverDoc(element: PsiElement, originalElement: PsiElement?): String? =
        generateDoc(element, originalElement)

    /**
     * Renders the raw inner content of the `<summary>` XML doc tag from the generated
     * dispatcher into IntelliJ quick-doc HTML. Produces the same visual output as
     * hovering over the corresponding C# property in Rider.
     *
     * The xmlDoc string is the content between `<summary>` and `</summary>`, which contains
     * an optional description text followed by a `<list type="table">` with locale rows.
     */
    private fun renderXmlDocToHtml(xmlDoc: String, key: String, isParametric: Boolean): String {
        val sb = StringBuilder()
        sb.append(DocumentationMarkup.DEFINITION_START)
        sb.append("<b>$key</b>")
        if (isParametric) sb.append("&nbsp;<i>(parametric)</i>")
        sb.append(DocumentationMarkup.DEFINITION_END)

        val rendered = tryRenderXmlDoc(xmlDoc)
        if (rendered != null) {
            sb.append(DocumentationMarkup.CONTENT_START)
            sb.append(rendered)
            sb.append(DocumentationMarkup.CONTENT_END)
        }
        return sb.toString()
    }

    /**
     * Parses the xmlDoc inner content and converts it to HTML.
     * Returns null if parsing fails (caller will show nothing in the content section).
     */
    private fun tryRenderXmlDoc(xmlDoc: String): String? {
        return try {
            val factory = DocumentBuilderFactory.newInstance()
            factory.isNamespaceAware = false
            val builder = factory.newDocumentBuilder()
            // Wrap in a root element so the parser handles bare text + child elements
            val doc = builder.parse(
                java.io.ByteArrayInputStream("<root>$xmlDoc</root>".toByteArray(Charsets.UTF_8))
            )
            val root = doc.documentElement

            val html = StringBuilder()

            // Collect direct text content (description) before any <list> element
            val descriptionParts = mutableListOf<String>()
            val children = root.childNodes
            for (i in 0 until children.length) {
                val node = children.item(i)
                when {
                    node.nodeType == Node.TEXT_NODE -> {
                        val text = node.textContent.trim()
                        if (text.isNotEmpty()) descriptionParts.add(text)
                    }
                    node.nodeType == Node.ELEMENT_NODE && node.nodeName == "list" -> break
                }
            }
            val description = descriptionParts.joinToString(" ").trim()
            if (description.isNotEmpty()) {
                html.append("<p>$description</p>")
            }

            // Find the <list type="table"> element and render it as an HTML table
            val lists = root.getElementsByTagName("list")
            for (i in 0 until lists.length) {
                val list = lists.item(i) as? Element ?: continue
                if (list.getAttribute("type") != "table") continue

                html.append("<table>")

                // <listheader>
                val headers = list.getElementsByTagName("listheader")
                if (headers.length > 0) {
                    val header = headers.item(0) as? Element
                    if (header != null) {
                        val term = header.getElementsByTagName("term").item(0)?.textContent ?: ""
                        val desc = header.getElementsByTagName("description").item(0)?.textContent ?: ""
                        html.append("<tr><th>$term</th><th>$desc</th></tr>")
                    }
                }

                // <item> rows
                val items = list.getElementsByTagName("item")
                for (j in 0 until items.length) {
                    val item = items.item(j) as? Element ?: continue
                    val term = item.getElementsByTagName("term").item(0)?.textContent ?: ""
                    val desc = item.getElementsByTagName("description").item(0)?.textContent ?: ""
                    html.append("<tr><td><b>$term</b></td><td>$desc</td></tr>")
                }

                html.append("</table>")
                break // only one list expected
            }

            html.toString().takeIf { it.isNotEmpty() }
        } catch (_: Exception) {
            null
        }
    }

    /** Fallback used when the generated dispatcher is not yet available. */
    private fun buildFallbackDocHtml(key: String, description: String?, isParametric: Boolean): String {
        val sb = StringBuilder()
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
