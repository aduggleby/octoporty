// ═══════════════════════════════════════════════════════════════════════════
// REQUEST INSPECTOR PAGE
// Compares Gateway vs Agent responses for a given URL
// ═══════════════════════════════════════════════════════════════════════════

import { useState } from 'react'
import { motion } from 'motion/react'
import { api } from '../api/client'
import { useToast } from '../hooks/useToast'
import type { DiagnoseResponse, ProbeResult, GatewayLogItem } from '../types'

export function RequestInspectorPage() {
  const { addToast } = useToast()
  const [url, setUrl] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [result, setResult] = useState<DiagnoseResponse | null>(null)

  const runProbe = async () => {
    if (!url.trim()) {
      addToast('warning', 'URL required', 'Enter a full URL to test')
      return
    }

    setIsLoading(true)
    try {
      const response = await api.diagnoseUrl(url.trim())
      setResult(response)
      if (!response.success) {
        addToast('error', 'Probe failed', response.error || 'Unknown error')
      }
    } catch (err) {
      const error = err as { message?: string }
      addToast('error', 'Probe failed', error?.message || 'Unknown error')
    } finally {
      setIsLoading(false)
    }
  }

  const formatHeaders = (headers?: Record<string, string[]>): string => {
    if (!headers) return 'No headers'
    return JSON.stringify(headers, null, 2)
  }

  const formatBody = (probe?: ProbeResult): string => {
    if (!probe?.success) return probe?.error || 'No response'
    if (probe.bodyText) return probe.bodyText
    if (probe.bodyBase64) return `base64:\n${probe.bodyBase64}`
    return '(empty body)'
  }

  const getLevelColor = (level: GatewayLogItem['level']): string => {
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

  const renderProbe = (title: string, probe?: ProbeResult) => {
    return (
      <motion.div
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        className="panel"
      >
        <div className="panel-header">
          <svg
            className="w-4 h-4 text-cyan-base"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          >
            <path d="M12 2v20M2 12h20" />
          </svg>
          <span className="panel-title">{title}</span>
        </div>
        <div className="panel-body">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
            <div>
              <p className="data-label">Status</p>
              <p className="data-value mt-1">
                {probe?.success ? probe.statusCode ?? 'OK' : 'Failed'}
              </p>
            </div>
            <div>
              <p className="data-label">Duration</p>
              <p className="data-value mt-1">
                {probe?.durationMs != null ? `${probe.durationMs}ms` : 'n/a'}
              </p>
            </div>
            <div>
              <p className="data-label">Content-Type</p>
              <p className="data-value mt-1">
                {probe?.contentType ?? 'unknown'}
              </p>
            </div>
            <div>
              <p className="data-label">Body Size</p>
              <p className="data-value mt-1">
                {probe?.bodySize != null ? `${probe.bodySize} bytes` : 'n/a'}
                {probe?.bodyTruncated ? ' (truncated)' : ''}
              </p>
            </div>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mt-6">
            <div>
              <p className="data-label mb-2">Headers</p>
              <pre className="p-3 bg-surface-2 rounded-md text-xs font-mono text-text-secondary max-h-64 overflow-auto">
                <code>{formatHeaders(probe?.headers)}</code>
              </pre>
            </div>
            <div>
              <p className="data-label mb-2">Body</p>
              <pre className="p-3 bg-surface-2 rounded-md text-xs font-mono text-text-secondary max-h-64 overflow-auto whitespace-pre-wrap">
                <code>{formatBody(probe)}</code>
              </pre>
            </div>
          </div>
        </div>
      </motion.div>
    )
  }

  return (
    <div>
      {/* Page Header */}
      <div className="page-header">
        <h1 className="page-title">Request Inspector</h1>
        <p className="page-subtitle">
          Compare Gateway vs Agent responses for a given URL
        </p>
      </div>

      {/* Input Panel */}
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
            <path d="M21 12a9 9 0 1 1-6.219-8.56" />
            <polyline points="21 3 21 9 15 9" />
          </svg>
          <span className="panel-title">Probe URL</span>
        </div>
        <div className="panel-body">
          <div className="flex flex-col md:flex-row gap-3">
            <input
              type="text"
              className="input flex-1"
              placeholder="https://example.com/app.js"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
            />
            <button
              className="btn btn-primary"
              onClick={runProbe}
              disabled={isLoading}
            >
              {isLoading ? 'Probing...' : 'Run Probe'}
            </button>
          </div>
          {result?.requestId && (
            <div className="mt-4 text-xs font-mono text-text-muted">
              request id: {result.requestId}
            </div>
          )}
          {result?.mapping && (
            <div className="mt-2 text-xs text-text-secondary">
              mapping: {result.mapping.externalDomain} → {result.mapping.internalTarget}
            </div>
          )}
        </div>
      </motion.div>

      {result && !result.success && (
        <div className="panel mb-6">
          <div className="panel-body text-rose-base">
            {result.error || 'Probe failed'}
          </div>
        </div>
      )}

      {result && (
        <div className="grid grid-cols-1 gap-6">
          {renderProbe('Gateway Response', result.gateway)}
          {renderProbe('Agent Direct Response', result.agent)}

          <motion.div
            initial={{ opacity: 0, y: 16 }}
            animate={{ opacity: 1, y: 0 }}
            className="panel"
          >
            <div className="panel-header">
              <svg
                className="w-4 h-4 text-text-tertiary"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
              >
                <circle cx="12" cy="12" r="10" />
                <line x1="12" y1="16" x2="12" y2="12" />
                <line x1="12" y1="8" x2="12.01" y2="8" />
              </svg>
              <span className="panel-title">Gateway Logs</span>
            </div>
            <div className="panel-body">
              {result.gatewayLogs.length === 0 ? (
                <p className="text-text-muted text-sm">No gateway logs matched this request.</p>
              ) : (
                <div className="space-y-2 max-h-80 overflow-auto">
                  {result.gatewayLogs.map((log) => (
                    <div key={log.id} className="text-xs font-mono">
                      <span className="text-text-muted mr-2">
                        {new Date(log.timestamp).toLocaleTimeString('en-US', { hour12: false })}
                      </span>
                      <span className={`${getLevelColor(log.level)} mr-2`}>
                        {log.level.toUpperCase()}
                      </span>
                      <span className="text-text-secondary">{log.message}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </motion.div>
        </div>
      )}
    </div>
  )
}
