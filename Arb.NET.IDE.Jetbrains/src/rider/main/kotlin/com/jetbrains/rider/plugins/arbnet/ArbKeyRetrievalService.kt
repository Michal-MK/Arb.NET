package com.jetbrains.rider.plugins.arbnet

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import com.jetbrains.rd.ide.model.ArbKeyInfo
import com.jetbrains.rd.ide.model.arbModel
import com.jetbrains.rd.util.lifetime.Lifetime
import com.jetbrains.rider.projectView.solution
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference

/**
 * Project-level service for fetching ARB keys from the C# backend.
 *
 * The RD call is started directly on the calling thread (RD allows off-EDT calls); the result
 * callback fires on the EDT and signals the latch.
 */
@Service(Service.Level.PROJECT)
class ArbKeyRetrievalService(private val project: Project) {
    companion object {
        fun getInstance(project: Project): ArbKeyRetrievalService = project.service()
    }
    /**
     * Fetches keys from the backend, blocking up to [timeoutMs] milliseconds.
     * Must be called from a background thread outside any read action.
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
        return resultRef.get()
    }
}