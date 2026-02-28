package com.jetbrains.rider.plugins.arbnet

import com.intellij.codeInsight.completion.CompletionContributor
import com.intellij.codeInsight.completion.CompletionParameters
import com.intellij.codeInsight.completion.CompletionProvider
import com.intellij.codeInsight.completion.CompletionResultSet
import com.intellij.codeInsight.completion.CompletionType
import com.intellij.codeInsight.lookup.LookupElementBuilder
import com.intellij.patterns.PlatformPatterns.psiElement
import com.intellij.util.ProcessingContext
import com.jetbrains.rd.ide.model.ArbKeyInfo
import com.jetbrains.rd.util.lifetime.Lifetime

// Matches the text before the caret inside a {*:Arb ...} expression
// e.g. in `{ext:Arb AppTitle}` with caret after "App", this matches "{ext:Arb App"
// TODO This regex is duplicated across a lot of places and does not handle all the cases, e.g. implicit XAML namespaces.
private val ARB_PREFIX_PATTERN = Regex("""\{[^:}]+:Arb\s+(\w*)$""")

/**
 * Provides code completion for ARB key names inside XAML markup extensions of the form:
 *   Text="{ext:Arb <caret>}"
 *
 * Uses text-based detection (not PSI node types) because Rider represents .xaml files
 * as RiderFileImpl — its own stub, not a standard XmlFile.
 *
 * Keys are fetched from the .NET backend via the RD protocol (getArbKeys call).
 */
class ArbKeyCompletionContributor : CompletionContributor() {
    init {
        // No PSI-pattern filter — Rider's XAML PSI (RiderFileImpl) doesn't use standard
        // XmlAttributeValue nodes, so we catch all invocations and filter by text instead.
        extend(
            CompletionType.BASIC,
            psiElement(),
            ArbKeyCompletionProvider()
        )
    }
}

private class ArbKeyCompletionProvider : CompletionProvider<CompletionParameters>() {
    override fun addCompletions(
        parameters: CompletionParameters,
        context: ProcessingContext,
        result: CompletionResultSet
    ) {
        // Only .xaml files
        val virtualFile = parameters.originalFile.virtualFile ?: return
        if (virtualFile.extension?.lowercase() != "xaml") return

        // Use the text to the left of the caret to detect we're inside {*:Arb ...}
        val doc = parameters.editor.document
        val caretOffset = parameters.offset
        val lineStart = doc.getLineStartOffset(doc.getLineNumber(caretOffset))
        val textBeforeCaret = doc.getText(com.intellij.openapi.util.TextRange(lineStart, caretOffset))

        val matchResult = ARB_PREFIX_PATTERN.find(textBeforeCaret) ?: return

        // The partial key the user has typed so far (may be empty)
        val typedKey = matchResult.groupValues[1]

        // Walk up from the virtual file directly — avoids PSI virtualFile being null on Rider XAML nodes
        val projectDir = findProjectDirFromVFile(virtualFile) ?: return

        val cache = ArbKeyCache.getInstance(parameters.position.project)
        // Use blocking fetch so completions appear on the first Ctrl+Space.
        // CompletionProviders run on a background thread, so blocking is safe here.
        val keys = cache.getKeysBlocking(projectDir, Lifetime.Eternal, timeoutMs = 500)
        if (keys.isEmpty()) return

        // We have ARB results — suppress backend XAML suggestions to keep popup clean.
        result.stopHere()

        // Use the typed partial key as the prefix for filtering
        val filtered = result.withPrefixMatcher(typedKey)

        for (keyInfo in keys) {
            filtered.addElement(keyInfo.toLookupElement())
        }
    }
}

private fun ArbKeyInfo.toLookupElement(): LookupElementBuilder {
    val tailParts = buildList {
        if (isParametric) add("\u26a0 parametric")
        if (!description.isNullOrBlank()) add(description)
    }
    val tailText = if (tailParts.isNotEmpty()) "  ${tailParts.joinToString("  ")}" else null

    return LookupElementBuilder.create(key)
        .withBoldness(true)
        .withTypeText("ARB key", true)
        .withCaseSensitivity(false)
        .let { b -> if (tailText != null) b.withTailText(tailText, true) else b }
}