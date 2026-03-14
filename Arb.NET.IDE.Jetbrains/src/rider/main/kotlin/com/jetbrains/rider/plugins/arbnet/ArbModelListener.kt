package com.jetbrains.rider.plugins.arbnet

import com.intellij.codeInsight.daemon.DaemonCodeAnalyzer
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity
import com.intellij.psi.PsiManager
import com.jetbrains.rd.ide.model.arbModel
import com.jetbrains.rd.util.lifetime.Lifetime
import com.jetbrains.rider.projectView.solution
import javax.swing.SwingUtilities

/**
 * Subscribes to backend → frontend signals on the ARB model at project startup.
 */
class ArbModelListener : ProjectActivity {

    override suspend fun execute(project: Project) {
        val model = project.solution.arbModel

        // Backend fires this after a "Generate ARB key" quick fix writes the key to .arb files.
        model.openArbEditor.advise(Lifetime.Eternal) { payload ->
            SwingUtilities.invokeLater {
                openArbEditorAtKey(project, payload.arbDir, payload.keyFilter)
            }
        }

        // Backend fires this after ARB keys are added, removed, or renamed.
        model.arbKeysChanged.advise(Lifetime.Eternal) {
            SwingUtilities.invokeLater {
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
            daemon.restart(psiFile)
        }
    }
}
