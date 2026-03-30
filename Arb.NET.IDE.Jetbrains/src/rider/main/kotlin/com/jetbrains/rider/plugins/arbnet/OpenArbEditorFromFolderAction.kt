package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.vfs.VirtualFile
import java.io.File

class OpenArbEditorFromFolderAction : AnAction("Open Arb.NET Editor") {

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.EDT

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val vf = e.getData(CommonDataKeys.VIRTUAL_FILE)
            ?: e.getData(CommonDataKeys.VIRTUAL_FILE_ARRAY)?.firstOrNull()
            ?: return

        val dirPath = resolveEditorDirectory(vf) ?: return

        val singletonFile = ArbEditor.getOrCreateFile(project)
        val editorManager = FileEditorManager.getInstance(project)
        val existingEditor = editorManager.getEditors(singletonFile).filterIsInstance<ArbEditor>().firstOrNull()

        if (existingEditor != null) {
            existingEditor.navigateTo(dirPath)
            editorManager.openFile(singletonFile, true)
        } else {
            singletonFile.putUserData(ArbEditor.INITIAL_DIR_KEY, dirPath)
            editorManager.openFile(singletonFile, true)
        }
    }

    override fun update(e: AnActionEvent) {
        e.presentation.isEnabledAndVisible = false
        e.project ?: return
        val vf = e.getData(CommonDataKeys.VIRTUAL_FILE)
            ?: e.getData(CommonDataKeys.VIRTUAL_FILE_ARRAY)?.firstOrNull()
            ?: return
        e.presentation.isEnabledAndVisible = resolveEditorDirectory(vf) != null
    }

    private fun resolveEditorDirectory(vf: VirtualFile): String? {
        if (vf.name == "l10n.yaml") {
            val parentPath = vf.parent?.path ?: return null
            return resolveArbDirectory(parentPath)
        }

        if (vf.extension == "arb") {
            return vf.parent?.path
        }

        val startDir = (if (vf.isDirectory) vf else vf.parent)?.path ?: return null
        if (vf.isDirectory) {
            val hasDirectArbFiles = try {
                vf.children.any { it.extension == "arb" }
            } catch (_: Exception) {
                false
            }
            if (hasDirectArbFiles) {
                return startDir
            }
        }

        var dir: File? = File(startDir)
        while (dir != null) {
            if (File(dir, "l10n.yaml").exists()) {
                return resolveArbDirectory(dir.path)
            }
            dir = dir.parentFile
        }

        return null
    }
}
