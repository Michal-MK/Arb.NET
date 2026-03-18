package com.jetbrains.rider.plugins.arbnet

import com.intellij.codeInsight.daemon.DaemonCodeAnalyzer
import com.intellij.openapi.application.EDT
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity
import com.intellij.psi.PsiManager
import com.jetbrains.rd.ide.model.arbModel
import com.jetbrains.rd.util.lifetime.Lifetime
import com.jetbrains.rider.projectView.solution
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * Subscribes to backend → frontend signals on the ARB model at project startup.
 */
class ArbModelListener : ProjectActivity {

    override suspend fun execute(project: Project) {
        // advise must be called on the protocol dispatcher thread (EDT in Rider)
        withContext(Dispatchers.EDT) {
            val model = project.solution.arbModel

            model.openArbEditor.advise(Lifetime.Eternal) { payload ->
                openArbEditorAtKey(project, payload.arbDir, payload.keyFilter)
            }

            model.arbKeysChanged.advise(Lifetime.Eternal) {
                restartXamlAnnotations(project)
            }
        }
    }

    private fun restartXamlAnnotations(project: Project) {
        val daemon = DaemonCodeAnalyzer.getInstance(project)
        val psiManager = PsiManager.getInstance(project)
        for (file in FileEditorManager.getInstance(project).openFiles) {
            if (file.extension?.lowercase() != "xaml") continue
            val psiFile = psiManager.findFile(file) ?: continue
            daemon.restart(psiFile, "ARB keys changed")
        }
    }
}