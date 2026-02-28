package com.jetbrains.rider.plugins.arbnet

import com.intellij.codeInspection.LocalInspectionTool
import com.intellij.codeInspection.ProblemHighlightType
import com.intellij.codeInspection.ProblemsHolder
import com.intellij.psi.PsiElementVisitor
import com.intellij.psi.xml.XmlAttribute
import com.intellij.psi.XmlElementVisitor
import com.jetbrains.rd.util.lifetime.Lifetime

/**
 * Highlights ARB key names inside XAML markup extensions that are either:
 *  - Unknown (not present in the template .arb file / generated AppLocale class) — reported as an ERROR.
 *  - Parametric (correspond to a method on AppLocale, not a property) — reported as a WARNING.
 *
 * Example:
 *   Text="{ext:Arb AppTitle}"    → OK
 *   Text="{ext:Arb Greeting}"   → WARNING: parametric key
 *   Text="{ext:Arb Bogus}"      → ERROR: unknown key
 */
@Suppress("InspectionDescriptionNotFoundInspection") // TODO
class ArbKeyInspection : LocalInspectionTool() {

    override fun getGroupDisplayName(): String = "Arb.NET"
    override fun getDisplayName(): String = "Invalid ARB key in markup extension"
    override fun getShortName(): String = "ArbKeyInspection"

    override fun buildVisitor(holder: ProblemsHolder, isOnTheFly: Boolean): PsiElementVisitor {
        return object : XmlElementVisitor() {
            override fun visitXmlAttribute(attribute: XmlAttribute) {
                // Only .xaml files
                val vFile = attribute.containingFile?.virtualFile ?: return
                if (vFile.extension?.lowercase() != "xaml") return

                val rawValue = attribute.value ?: return
                val key = extractArbKey(rawValue) ?: return  // not an ARB expression, or still typing
                val projectDir = findProjectDir(attribute) ?: return
                val project = attribute.project

                val cache = ArbKeyCache.getInstance(project)
                val lifetime = Lifetime.Eternal

                // Use blocking fetch — inspections run on a background thread
                val keys = cache.getKeysBlocking(projectDir, lifetime)
                if (keys.isEmpty()) return  // backend not ready yet; skip to avoid false positives

                val valueElement = attribute.valueElement ?: return
                val match = keys.find { it.key == key }

                when {
                    match == null -> holder.registerProblem(
                        valueElement,
                        "Unknown ARB key '$key'",
                        ProblemHighlightType.GENERIC_ERROR_OR_WARNING
                    )
                    match.isParametric -> holder.registerProblem(
                        valueElement,
                        "ARB key '$key' is parametric and cannot be used as a plain string in XAML",
                        ProblemHighlightType.WARNING
                    )
                }
            }
        }
    }
}