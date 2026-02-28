package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFileManager
import com.intellij.openapi.vfs.newvfs.BulkFileListener
import com.intellij.openapi.vfs.newvfs.events.VFileEvent
import com.jetbrains.rd.ide.model.ArbKeyInfo
import com.jetbrains.rd.ide.model.arbModel
import com.jetbrains.rd.util.lifetime.Lifetime
import com.jetbrains.rider.projectView.solution
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference

/**
 * Project-level service that caches ARB key lists per project directory.
 * The cache is invalidated whenever any .arb file changes.
 * Provides both async (fire-and-forget) and blocking access patterns.
 */
@Service(Service.Level.PROJECT)
class ArbKeyCache(private val project: Project) {

    private val cache = ConcurrentHashMap<String, List<ArbKeyInfo>>()

    init {
        // Invalidate cache whenever any .arb file changes on disk
        project.messageBus.connect().subscribe(VirtualFileManager.VFS_CHANGES, object : BulkFileListener {
            override fun after(events: List<VFileEvent>) {
                if (events.any { it.file?.extension == "arb" }) {
                    cache.clear()
                }
            }
        })
    }

    /**
     * Returns the cached key list for [projectDir], or null if not yet fetched.
     * Triggers a background fetch so the next call may return results.
     */
    fun getKeys(projectDir: String, lifetime: Lifetime): List<ArbKeyInfo>? {
        val cached = cache[projectDir]
        if (cached != null) return cached

        requestKeysAsync(projectDir, lifetime)

        return null
    }

    /**
     * Blocking fetch with a short timeout.
     * Returns the cached list if available, otherwise starts an async fetch and waits up to
     * [timeoutMs] milliseconds for the result. Returns an empty list if the backend doesn't
     * respond in time — the next call will return the cached result.
     *
     */
    fun getKeysBlocking(projectDir: String, lifetime: Lifetime, timeoutMs: Long = 2000): List<ArbKeyInfo> {
        cache[projectDir]?.let { return it }

        // Never block EDT. Prime async fetch and return quickly.
        val app = ApplicationManager.getApplication()
        if (app.isDispatchThread) {
            requestKeysAsync(projectDir, lifetime)
            return cache[projectDir] ?: emptyList()
        }

        val latch = CountDownLatch(1)
        val resultRef = AtomicReference<List<ArbKeyInfo>>(emptyList())

        app.invokeAndWait {
            project.solution.arbModel.getArbKeys.start(lifetime, projectDir)
                .result.advise(lifetime) { result ->
                    try {
                        val keys = result.unwrap()
                        resultRef.set(keys)
                        if (keys.isNotEmpty()) {
                            cache[projectDir] = keys
                        }
                    } catch (_: Throwable) {
                        // Keep empty list
                    }
                    latch.countDown()
                }
        }

        latch.await(timeoutMs, TimeUnit.MILLISECONDS)
        return resultRef.get()
    }

    private fun requestKeysAsync(projectDir: String, lifetime: Lifetime) {
        ApplicationManager.getApplication().invokeLater {
            project.solution.arbModel.getArbKeys.start(lifetime, projectDir)
                .result.advise(lifetime) { result ->
                    try {
                        val keys = result.unwrap()
                        if (keys.isNotEmpty()) {
                            cache[projectDir] = keys
                        }
                    } catch (_: Throwable) {
                        // Ignore failures — cache remains empty, will retry next call
                    }
                }
        }
    }

    companion object {
        fun getInstance(project: Project): ArbKeyCache = project.service()
    }
}
