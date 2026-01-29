// ═══════════════════════════════════════════════════════════════════════════
// GATEWAY PAGE
// Shows Gateway information and real-time log output
// Loads historical logs on mount and supports infinite scroll for older logs
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useEffect, useRef, useCallback } from 'react'
import { motion } from 'motion/react'
import { useToast } from '../hooks/useToast'
import { useSignalR } from '../hooks/useSignalR'
import { api } from '../api/client'
import type { AgentStatus, GatewayLog, GatewayLogItem } from '../types'

// Extended log type that includes the ID for pagination
interface LogEntry {
  id: number
  timestamp: string
  level: GatewayLog['level']
  message: string
}

export function GatewayPage() {
  const { addToast } = useToast()
  const [status, setStatus] = useState<AgentStatus | null>(null)
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [hasMore, setHasMore] = useState(false)
  const [autoScroll, setAutoScroll] = useState(true)
  const logsEndRef = useRef<HTMLDivElement>(null)
  const logsContainerRef = useRef<HTMLDivElement>(null)
  const nextRealTimeId = useRef(Number.MAX_SAFE_INTEGER) // Real-time logs get high IDs

  // Fetch initial status
  useEffect(() => {
    api.getStatus()
      .then(setStatus)
      .catch((err) => {
        addToast('error', 'Failed to load status', err.message)
      })
      .finally(() => {
        setIsLoading(false)
      })
  }, [addToast])

  // Fetch initial logs on mount
  useEffect(() => {
    api.getGatewayLogs(0, 1000)
      .then((response) => {
        if (response.success) {
          // Logs come newest-first, reverse for display (oldest at top)
          const entries: LogEntry[] = response.logs.map((l: GatewayLogItem) => ({
            id: l.id,
            timestamp: l.timestamp,
            level: l.level,
            message: l.message,
          })).reverse()
          setLogs(entries)
          setHasMore(response.hasMore)
        }
      })
      .catch((err) => {
        console.error('Failed to load initial logs:', err)
      })
  }, [])

  // Handle SignalR gateway log updates (real-time)
  const handleGatewayLog = useCallback((log: GatewayLog) => {
    setLogs((prev) => {
      // Assign a unique high ID to real-time logs
      const newLog: LogEntry = {
        id: nextRealTimeId.current--,
        timestamp: log.timestamp,
        level: log.level,
        message: log.message,
      }
      // Keep only the last 2000 logs to avoid memory issues
      const newLogs = [...prev, newLog]
      if (newLogs.length > 2000) {
        return newLogs.slice(-2000)
      }
      return newLogs
    })
  }, [])

  useSignalR({
    onGatewayLog: handleGatewayLog,
  })

  // Auto-scroll to bottom when new logs arrive
  useEffect(() => {
    if (autoScroll && logsEndRef.current) {
      logsEndRef.current.scrollIntoView({ behavior: 'smooth' })
    }
  }, [logs, autoScroll])

  // Load more logs when scrolling to top
  const loadMoreLogs = useCallback(async () => {
    if (isLoadingMore || !hasMore || logs.length === 0) return

    // Find the oldest log (lowest ID) from historical logs
    const oldestId = Math.min(...logs.filter(l => l.id < Number.MAX_SAFE_INTEGER - 100000).map(l => l.id))
    if (oldestId <= 0 || oldestId === Infinity) return

    setIsLoadingMore(true)
    try {
      const response = await api.getGatewayLogs(oldestId, 1000)
      if (response.success && response.logs.length > 0) {
        const entries: LogEntry[] = response.logs.map((l: GatewayLogItem) => ({
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
    } catch (err) {
      console.error('Failed to load more logs:', err)
    } finally {
      setIsLoadingMore(false)
    }
  }, [isLoadingMore, hasMore, logs])

  // Handle scroll to detect when at top
  const handleScroll = useCallback(() => {
    const container = logsContainerRef.current
    if (!container) return

    // Check if scrolled to top (within 50px)
    if (container.scrollTop < 50 && hasMore && !isLoadingMore) {
      loadMoreLogs()
    }

    // Check if scrolled to bottom for auto-scroll
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

  const getLevelColor = (level: GatewayLog['level']): string => {
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
      {/* Page Header */}
      <div className="page-header">
        <h1 className="page-title">Gateway</h1>
        <p className="page-subtitle">
          Gateway information and real-time logs
        </p>
      </div>

      {/* Gateway Info Panel */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="panel mb-6"
      >
        <div className="panel-header">
          <svg
            className="w-4 h-4 text-cyan-base"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          >
            <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
            <line x1="8" y1="21" x2="16" y2="21" />
            <line x1="12" y1="17" x2="12" y2="21" />
          </svg>
          <span className="panel-title">Gateway Information</span>
        </div>
        <div className="panel-body">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div>
              <p className="data-label">Gateway Version</p>
              <p className="data-value mt-1">
                {status?.gatewayVersion ?? 'Unknown'}
              </p>
            </div>
            <div>
              <p className="data-label">Connection Status</p>
              <p className="data-value mt-1">
                <span
                  className={
                    status?.connectionStatus === 'Connected'
                      ? 'text-emerald-base'
                      : 'text-rose-base'
                  }
                >
                  {status?.connectionStatus ?? 'Unknown'}
                </span>
              </p>
            </div>
            <div>
              <p className="data-label">Gateway Uptime</p>
              <p className="data-value mt-1">
                {status?.gatewayUptime
                  ? formatUptime(status.gatewayUptime)
                  : 'Unknown'}
              </p>
            </div>
          </div>
        </div>
      </motion.div>

      {/* Logs Panel */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.1 }}
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
            <span className="panel-title">Gateway Logs</span>
            <span className="text-xs text-text-muted font-mono">
              ({logs.length} entries)
            </span>
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
            <button
              onClick={clearLogs}
              className="btn btn-ghost btn-sm"
            >
              Clear
            </button>
          </div>
        </div>
        <div className="panel-body p-0">
          <div
            ref={logsContainerRef}
            onScroll={handleScroll}
            className="terminal max-h-[500px] overflow-y-auto rounded-none border-0"
          >
            {/* Loading more indicator */}
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
            {/* Has more indicator */}
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
                <svg
                  className="w-12 h-12 mx-auto mb-4 opacity-50"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.5"
                >
                  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                  <polyline points="14 2 14 8 20 8" />
                </svg>
                <p className="font-mono text-sm">Waiting for gateway logs...</p>
                <p className="font-mono text-xs mt-2 text-text-muted">
                  Logs will appear here when the gateway is connected
                </p>
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
                    <span
                      className={`shrink-0 w-16 font-semibold ${getLevelColor(log.level)}`}
                    >
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

/**
 * Formats uptime in seconds to a human-readable string.
 */
function formatUptime(seconds: number): string {
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)
  const days = Math.floor(hours / 24)

  if (days > 0) {
    const remainingHours = hours % 24
    return `${days}d ${remainingHours}h`
  }
  if (hours > 0) {
    const remainingMinutes = minutes % 60
    return `${hours}h ${remainingMinutes}m`
  }
  return `${minutes}m`
}
