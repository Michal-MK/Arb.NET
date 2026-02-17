package com.jetbrains.rider.plugins.arbnet

import com.intellij.lang.Language
import com.intellij.openapi.fileTypes.LanguageFileType
import javax.swing.Icon

object ArbFileType : LanguageFileType(Language.findInstancesByMimeType("application/json").firstOrNull() ?: Language.ANY) {
    override fun getName(): String = "ARB"
    override fun getDescription(): String = "Application Resource Bundle format invented by Google for i18n."
    override fun getDefaultExtension(): String = "arb"
    override fun getIcon(): Icon? = null
}