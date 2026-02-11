// ═══════════════════════════════════════════════════════════════════════════
// GATEWAY LOG DETAIL PAGE
// Detailed view for inspecting gateway log events and message payloads.
// ═══════════════════════════════════════════════════════════════════════════

import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { api } from '../api/client'
import { useError } from '../components/ErrorBoundary'
import type { GatewayLogItem, LogLevel } from '../types'

function getLevelColor(level: LogLevel): string {
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

function formatTimestamp(timestamp: string): string {
  return new Date(timestamp).toLocaleString('en-US', {
    hour12: false,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })
}

export function GatewayLogDetailPage() {
  const { showError } = useError()
  const [logs, setLogs] = useState<GatewayLogItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [levelFilter, setLevelFilter] = useState<'All' | LogLevel>('All')
  const [selectedLogId, setSelectedLogId] = useState<number | null>(null)

  useEffect(() => {
    setIsLoading(true)
    api.getGatewayLogs(0, 2000)
      .then((response) => {
        if (response.success) {
          setLogs(response.logs)
          if (response.logs.length > 0) {
            setSelectedLogId(response.logs[0].id)
          }
        }
      })
      .catch((err) => {
        const message = err?.message || 'Unknown error'
        const details = err?.errors?.serializerErrors?.join('\n') || JSON.stringify(err, null, 2)
        showError('Failed to Load Detailed Logs', message, details)
      })
      .finally(() => setIsLoading(false))
  }, [showError])

  const filteredLogs = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase()
    return logs.filter((log) => {
      if (levelFilter !== 'All' && log.level !== levelFilter) {
        return false
      }
      if (!normalizedSearch) {
        return true
      }
      return (
        log.message.toLowerCase().includes(normalizedSearch) ||
        log.level.toLowerCase().includes(normalizedSearch) ||
        log.id.toString().includes(normalizedSearch)
      )
    })
  }, [logs, search, levelFilter])

  const selectedLog = filteredLogs.find((log) => log.id === selectedLogId) ?? filteredLogs[0] ?? null

  useEffect(() => {
    if (!selectedLog && filteredLogs.length > 0) {
      setSelectedLogId(filteredLogs[0].id)
    }
  }, [selectedLog, filteredLogs])

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Gateway Log Detail</h1>
        <p className="page-subtitle">Inspect full log entries and request context</p>
      </div>

      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="panel mb-6"
      >
        <div className="panel-body">
          <div className="flex flex-wrap items-center gap-3">
            <Link to="/gateway" className="btn btn-ghost btn-sm">
              Back to Gateway
            </Link>
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="input max-w-md"
              placeholder="Search message, level, or log ID..."
            />
            <select
              value={levelFilter}
              onChange={(e) => setLevelFilter(e.target.value as 'All' | LogLevel)}
              className="input w-40"
            >
              <option value="All">All levels</option>
              <option value="Debug">Debug</option>
              <option value="Info">Info</option>
              <option value="Warning">Warning</option>
              <option value="Error">Error</option>
            </select>
            <span className="text-xs font-mono text-text-muted">
              {filteredLogs.length} / {logs.length} entries
            </span>
          </div>
        </div>
      </motion.div>

      <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.05 }}
          className="panel"
        >
          <div className="panel-header">
            <span className="panel-title">Log Entries</span>
          </div>
          <div className="panel-body p-0">
            <div className="terminal max-h-[700px] overflow-y-auto rounded-none border-0">
              {isLoading && (
                <div className="text-center py-8 font-mono text-xs text-text-muted">Loading logs...</div>
              )}
              {!isLoading && filteredLogs.length === 0 && (
                <div className="text-center py-8 font-mono text-xs text-text-muted">No logs match current filter.</div>
              )}
              {!isLoading && filteredLogs.length > 0 && (
                <div>
                  {filteredLogs.map((log) => {
                    const isSelected = selectedLog?.id === log.id
                    return (
                      <button
                        key={log.id}
                        type="button"
                        onClick={() => setSelectedLogId(log.id)}
                        className={`w-full text-left px-4 py-2 border-b border-border-subtle hover:bg-surface-2 transition-colors ${
                          isSelected ? 'bg-surface-2' : ''
                        }`}
                      >
                        <div className="flex items-center justify-between gap-3">
                          <span className={`font-mono text-xs ${getLevelColor(log.level)}`}>[{log.level}]</span>
                          <span className="font-mono text-[11px] text-text-muted">#{log.id}</span>
                        </div>
                        <p className="font-mono text-[11px] text-text-muted mt-1">{formatTimestamp(log.timestamp)}</p>
                        <p className="font-mono text-xs text-text-secondary mt-1 truncate">{log.message}</p>
                      </button>
                    )
                  })}
                </div>
              )}
            </div>
          </div>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
          className="panel"
        >
          <div className="panel-header">
            <span className="panel-title">Selected Entry</span>
          </div>
          <div className="panel-body">
            {!selectedLog && (
              <p className="font-mono text-xs text-text-muted">Select a log entry to view details.</p>
            )}
            {selectedLog && (
              <div className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div>
                    <p className="data-label">Log ID</p>
                    <p className="data-value mt-1 font-mono">#{selectedLog.id}</p>
                  </div>
                  <div>
                    <p className="data-label">Level</p>
                    <p className={`data-value mt-1 font-mono ${getLevelColor(selectedLog.level)}`}>
                      {selectedLog.level}
                    </p>
                  </div>
                  <div>
                    <p className="data-label">Timestamp</p>
                    <p className="data-value mt-1 font-mono text-sm">{formatTimestamp(selectedLog.timestamp)}</p>
                  </div>
                </div>

                <div>
                  <p className="data-label mb-2">Message</p>
                  <pre className="terminal p-4 text-xs whitespace-pre-wrap break-words">
                    {selectedLog.message}
                  </pre>
                </div>
              </div>
            )}
          </div>
        </motion.div>
      </div>
    </div>
  )
}

