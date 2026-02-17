package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.testFramework.LightVirtualFile

class OpenArbEditorFromFolderAction : AnAction("Open Arb.NET Editor") {

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.EDT

    private fun AnActionEvent.resolveDir(): VirtualFile? {
        val vf = getData(CommonDataKeys.VIRTUAL_FILE)
            ?: getData(CommonDataKeys.VIRTUAL_FILE_ARRAY)?.firstOrNull()
            ?: return null
        return if (vf.isDirectory) vf else vf.parent
    }

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val dir = e.resolveDir() ?: return
        val lightFile = LightVirtualFile(ArbEditor.FILE_NAME).apply {
            putUserData(ArbEditor.INITIAL_DIR_KEY, dir.path)
        }
        FileEditorManager.getInstance(project).openFile(lightFile, true)
    }

    override fun update(e: AnActionEvent) {
        e.presentation.isEnabledAndVisible = false
        e.project ?: return
        val vf = e.getData(CommonDataKeys.VIRTUAL_FILE)
            ?: e.getData(CommonDataKeys.VIRTUAL_FILE_ARRAY)?.firstOrNull()
            ?: return
        val show = if (vf.isDirectory) {
            try { vf.children.any { it.extension == "arb" } } catch (_: Exception) { false }
        } else {
            vf.extension == "arb" || try {
                vf.parent?.children?.any { it.extension == "arb" } == true
            } catch (_: Exception) { false }
        }
        e.presentation.isEnabledAndVisible = show
    }
}
