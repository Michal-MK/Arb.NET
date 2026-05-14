package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import com.jetbrains.rd.ide.model.ArbKeyInfo
import com.jetbrains.rd.ide.model.arbModel
import com.jetbrains.rd.util.lifetime.Lifetime
import com.jetbrains.rider.projectView.solution
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference

/**
 * Project-level service for fetching ARB keys from the C# backend.
 *
 * Maintains a per-projectDir cache so that callers on the EDT (e.g. intention
 * `isAvailable`) never block. The cache is populated lazily on the first blocking
 * call (annotator background thread) and invalidated whenever the backend fires
 * `arbKeysChanged` for the affected directory.
 */
@Service(Service.Level.PROJECT)
class ArbKeyRetrievalService(private val project: Project) {
    companion object {
        fun getInstance(project: Project): ArbKeyRetrievalService = project.service()
    }

    private data class CacheEntry(
        val keys: List<ArbKeyInfo>,
        val refreshing: AtomicBoolean = AtomicBoolean(false)
    )

    private val cache = ConcurrentHashMap<String, CacheEntry>()

    /** Evicts the cached keys for [projectDir]. Called when `arbKeysChanged` fires. */
    fun invalidate(projectDir: String) {
        cache.remove(projectDir)
    }

    /**
     * Returns cached keys for [projectDir] immediately without any blocking.
     * Returns `null` when the cache is cold (no prior successful fetch).
     *
     * When the cache is cold, a background fetch is triggered so subsequent calls
     * will find a warm cache. Safe to call on the EDT.
     */
    fun getCached(projectDir: String): List<ArbKeyInfo>? {
        val entry = cache[projectDir]
        if (entry != null) return entry.keys

        // Cache is cold — kick off a background refresh so the next call is warm,
        // but return null immediately so the caller does not block.
        triggerBackgroundRefresh(projectDir)
        return null
    }

    /**
     * Fetches keys from the backend, blocking up to [timeoutMs] milliseconds.
     * Populates the cache on success. Must be called from a background thread
     * outside any read action.
     */
    fun getKeysBlocking(projectDir: String, lifetime: Lifetime, timeoutMs: Long = 2000): List<ArbKeyInfo> {
        val latch = CountDownLatch(1)
        val resultRef = AtomicReference<List<ArbKeyInfo>>(emptyList())

        project.solution.arbModel.getArbKeys.start(lifetime, projectDir)
            .result.advise(lifetime) { result ->
                try {
                    resultRef.set(result.unwrap())
                } catch (_: Throwable) { }
                latch.countDown()
            }

        latch.await(timeoutMs, TimeUnit.MILLISECONDS)
        val keys = resultRef.get()
        cache[projectDir] = CacheEntry(keys)
        return keys
    }

    private fun triggerBackgroundRefresh(projectDir: String) {
        // Use a sentinel entry to prevent multiple concurrent refreshes for the same dir.
        val sentinel = CacheEntry(emptyList())
        if (cache.putIfAbsent(projectDir, sentinel) != null) return

        Thread {
            try {
                val keys = getKeysBlocking(projectDir, Lifetime.Eternal)
                cache[projectDir] = CacheEntry(keys)
            } catch (_: Throwable) {
                cache.remove(projectDir)
            }
        }.also { it.isDaemon = true; it.name = "arb-key-refresh-$projectDir" }.start()
    }
}
