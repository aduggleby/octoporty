// ═══════════════════════════════════════════════════════════════════════════
// AGENT LOGS PAGE
// Shows Agent process logs with historical load + real-time SignalR updates.
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useEffect, useRef, useCallback } from 'react'
import { motion } from 'motion/react'
import { useError } from '../components/ErrorBoundary'
import { useToast } from '../hooks/useToast'
import { useSignalR } from '../hooks/useSignalR'
import { api } from '../api/client'
import type { AgentLog, AgentLogItem } from '../types'

interface LogEntry {
  id: number
  timestamp: string
  level: AgentLog['level']
  message: string
}

export function AgentLogsPage() {
  const { addToast } = useToast()
  const { showError } = useError()

  const [logs, setLogs] = useState<LogEntry[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [hasMore, setHasMore] = useState(false)
  const [autoScroll, setAutoScroll] = useState(true)

  const logsEndRef = useRef<HTMLDivElement>(null)
  const logsContainerRef = useRef<HTMLDivElement>(null)
  const nextRealTimeId = useRef(Number.MAX_SAFE_INTEGER)

  // Initial history
  useEffect(() => {
    api.getAgentLogs(0, 1000)
      .then((response) => {
        if (response.success) {
          const entries: LogEntry[] = response.logs.map((l: AgentLogItem) => ({
            id: l.id,
            timestamp: l.timestamp,
            level: l.level,
            message: l.message,
          })).reverse()
          setLogs(entries)
          setHasMore(response.hasMore)
        } else {
          addToast('warning', 'Agent Logs Unavailable', response.error ?? 'Unknown error')
        }
      })
      .catch((err) => {
        const message = err?.message || 'Unknown error'
        const details = err?.errors?.serializerErrors?.join('\n') || JSON.stringify(err, null, 2)
        showError('Failed to Load Agent Logs', message, details)
      })
      .finally(() => setIsLoading(false))
  }, [addToast, showError])

  const handleAgentLog = useCallback((log: AgentLog) => {
    setLogs((prev) => {
      const newLog: LogEntry = {
        id: nextRealTimeId.current--,
        timestamp: log.timestamp,
        level: log.level,
        message: log.message,
      }
      const newLogs = [...prev, newLog]
      if (newLogs.length > 2000) return newLogs.slice(-2000)
      return newLogs
    })
  }, [])

  useSignalR({
    onAgentLog: handleAgentLog,
  })

  useEffect(() => {
    if (autoScroll && logsEndRef.current) {
      logsEndRef.current.scrollIntoView({ behavior: 'smooth' })
    }
  }, [logs, autoScroll])

  const loadMoreLogs = useCallback(async () => {
    if (isLoadingMore || !hasMore || logs.length === 0) return

    const oldestId = Math.min(...logs.filter(l => l.id < Number.MAX_SAFE_INTEGER - 100000).map(l => l.id))
    if (oldestId <= 0 || oldestId === Infinity) return

    setIsLoadingMore(true)
    try {
      const response = await api.getAgentLogs(oldestId, 1000)
      if (response.success && response.logs.length > 0) {
        const entries: LogEntry[] = response.logs.map((l: AgentLogItem) => ({
          id: l.id,
          timestamp: l.timestamp,
          level: l.level,
          message: l.message,
        })).reverse()
        setLogs(prev => [...entries, ...prev])
        setHasMore(response.hasMore)
      } else {
        setHasMore(false)
      }
    } catch (err: unknown) {
      const error = err as { message?: string; errors?: { serializerErrors?: string[] } }
      const message = error?.message || 'Unknown error'
      const details = error?.errors?.serializerErrors?.join('\n') || JSON.stringify(err, null, 2)
      showError('Failed to Load More Agent Logs', message, details)
    } finally {
      setIsLoadingMore(false)
    }
  }, [isLoadingMore, hasMore, logs, showError])

  const handleScroll = useCallback(() => {
    const container = logsContainerRef.current
    if (!container) return

    if (container.scrollTop < 50 && hasMore && !isLoadingMore) {
      loadMoreLogs()
    }

    const isAtBottom = container.scrollHeight - container.scrollTop - container.clientHeight < 50
    setAutoScroll(isAtBottom)
  }, [hasMore, isLoadingMore, loadMoreLogs])

  const formatTimestamp = (timestamp: string): string => {
    return new Date(timestamp).toLocaleTimeString('en-US', {
      hour12: false,
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    })
  }

  const getLevelColor = (level: AgentLog['level']): string => {
    switch (level) {
      case 'Debug':
        return 'text-text-muted'
      case 'Info':
        return 'text-cyan-base'
      case 'Warning':
        return 'text-amber-base'
      case 'Error':
        return 'text-rose-base'
      default:
        return 'text-text-secondary'
    }
  }

  const clearLogs = () => {
    setLogs([])
    addToast('info', 'Logs cleared', 'Log buffer has been cleared')
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <motion.div
          className="w-12 h-12 border-2 border-cyan-base border-t-transparent rounded-full"
          animate={{ rotate: 360 }}
          transition={{ duration: 1, repeat: Infinity, ease: 'linear' }}
        />
      </div>
    )
  }

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Agent Logs</h1>
        <p className="page-subtitle">Agent process logs (file-tailed)</p>
      </div>

      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="panel"
      >
        <div className="panel-header flex items-center justify-between">
          <div className="flex items-center gap-2">
            <svg
              className="w-4 h-4 text-amber-base"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
              <polyline points="14 2 14 8 20 8" />
              <line x1="16" y1="13" x2="8" y2="13" />
              <line x1="16" y1="17" x2="8" y2="17" />
              <polyline points="10 9 9 9 8 9" />
            </svg>
            <span className="panel-title">Agent Logs</span>
            <span className="text-xs text-text-muted font-mono">({logs.length} entries)</span>
          </div>
          <div className="flex items-center gap-3">
            <label className="flex items-center gap-2 text-xs text-text-secondary cursor-pointer">
              <input
                type="checkbox"
                checked={autoScroll}
                onChange={(e) => setAutoScroll(e.target.checked)}
                className="rounded border-border-default bg-surface-2"
              />
              Auto-scroll
            </label>
            <button onClick={clearLogs} className="btn btn-ghost btn-sm">Clear</button>
          </div>
        </div>
        <div className="panel-body p-0">
          <div
            ref={logsContainerRef}
            onScroll={handleScroll}
            className="terminal max-h-[500px] overflow-y-auto rounded-none border-0"
          >
            {isLoadingMore && (
              <div className="flex items-center justify-center py-2 text-text-muted">
                <motion.div
                  className="w-4 h-4 border-2 border-cyan-base border-t-transparent rounded-full mr-2"
                  animate={{ rotate: 360 }}
                  transition={{ duration: 1, repeat: Infinity, ease: 'linear' }}
                />
                <span className="font-mono text-xs">Loading more logs...</span>
              </div>
            )}
            {hasMore && !isLoadingMore && logs.length > 0 && (
              <div className="text-center py-2">
                <button
                  onClick={loadMoreLogs}
                  className="font-mono text-xs text-cyan-base hover:text-cyan-light transition-colors"
                >
                  ↑ Load more logs
                </button>
              </div>
            )}
            {logs.length === 0 && !isLoadingMore && (
              <div className="text-center py-12 text-text-muted">
                <p className="font-mono text-sm">Waiting for agent logs...</p>
              </div>
            )}
            {logs.length > 0 && (
              <div className="space-y-0.5">
                {logs.map((log, index) => (
                  <div
                    key={index}
                    className="flex gap-3 px-4 py-1 hover:bg-surface-2 transition-colors"
                  >
                    <span className="text-text-muted shrink-0">
                      {formatTimestamp(log.timestamp)}
                    </span>
                    <span className={`shrink-0 w-16 font-semibold ${getLevelColor(log.level)}`}>
                      [{log.level}]
                    </span>
                    <span className="text-text-secondary break-all">
                      {log.message}
                    </span>
                  </div>
                ))}
                <div ref={logsEndRef} />
              </div>
            )}
          </div>
        </div>
      </motion.div>
    </div>
  )
}

