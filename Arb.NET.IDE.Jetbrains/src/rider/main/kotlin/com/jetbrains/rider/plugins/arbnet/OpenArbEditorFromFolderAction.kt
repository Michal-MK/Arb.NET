package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.vfs.VirtualFile

class OpenArbEditorFromFolderAction : AnAction("Open Arb.NET Editor") {

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.EDT

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val vf = e.getData(CommonDataKeys.VIRTUAL_FILE)
            ?: e.getData(CommonDataKeys.VIRTUAL_FILE_ARRAY)?.firstOrNull()
            ?: return

        val dirPath: String = if (vf.name == "l10n.yaml") {
            val parentPath = vf.parent?.path ?: return
            resolveArbDirectory(parentPath)
        } else {
            (if (vf.isDirectory) vf else vf.parent)?.path ?: return
        }

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
        val show = when {
            vf.name == "l10n.yaml" -> true
            vf.isDirectory -> try { vf.children.any { it.extension == "arb" } } catch (_: Exception) { false }
            vf.extension == "arb" -> true
            else -> try {
                vf.parent?.children?.any { it.extension == "arb" } == true
            } catch (_: Exception) { false }
        }
        e.presentation.isEnabledAndVisible = show
    }
}
