// ═══════════════════════════════════════════════════════════════════════════
// DASHBOARD PAGE
// Main control panel with status overview and quick actions
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useEffect, useCallback } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { ConnectionStatus } from '../components/ConnectionStatus'
import { useSignalR } from '../hooks/useSignalR'
import { useToast } from '../hooks/useToast'
import { api } from '../api/client'
import type { AgentStatus, PortMapping, StatusUpdate } from '../types'

export function DashboardPage() {
  const { addToast } = useToast()
  const [status, setStatus] = useState<AgentStatus | null>(null)
  const [mappings, setMappings] = useState<PortMapping[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isReconnecting, setIsReconnecting] = useState(false)

  // Fetch initial data
  useEffect(() => {
    Promise.all([api.getStatus(), api.getMappings()])
      .then(([statusData, mappingsData]) => {
        setStatus(statusData)
        setMappings(mappingsData)
      })
      .catch((err) => {
        addToast('error', 'Failed to load dashboard', err.message)
      })
      .finally(() => {
        setIsLoading(false)
      })
  }, [addToast])

  // Handle SignalR status updates
  const handleStatusUpdate = useCallback(
    (update: StatusUpdate) => {
      setStatus((prev) =>
        prev ? { ...prev, connectionStatus: update.connectionStatus } : null
      )

      // Show toast for connection changes
      if (update.connectionStatus === 'Connected') {
        addToast('success', 'Connected', 'Tunnel is now active')
      } else if (update.connectionStatus === 'Disconnected') {
        addToast('error', 'Disconnected', update.message || 'Tunnel connection lost')
      }
    },
    [addToast]
  )

  useSignalR({
    onStatusUpdate: handleStatusUpdate,
  })

  const handleReconnect = async () => {
    setIsReconnecting(true)
    try {
      await api.reconnect()
      addToast('info', 'Reconnecting', 'Attempting to establish tunnel connection')
    } catch (err) {
      const error = err as Error
      addToast('error', 'Reconnect failed', error.message)
    } finally {
      setIsReconnecting(false)
    }
  }

  const formatUptime = (seconds: number | null): string => {
    if (!seconds) return '--:--:--'
    const h = Math.floor(seconds / 3600)
    const m = Math.floor((seconds % 3600) / 60)
    const s = Math.floor(seconds % 60)
    return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`
  }

  const activeMappings = mappings.filter((m) => m.enabled)
  const inactiveMappings = mappings.filter((m) => !m.enabled)

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
        <h1 className="page-title">Control Panel</h1>
        <p className="page-subtitle">
          System status and tunnel overview
        </p>
      </div>

      {/* Connection Status */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="mb-8"
      >
        {status && (
          <ConnectionStatus
            status={status.connectionStatus}
            gatewayUrl={status.gatewayUrl}
            lastConnected={status.lastConnected}
            onReconnect={isReconnecting ? undefined : handleReconnect}
          />
        )}
      </motion.div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
        {/* Uptime */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
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
              <circle cx="12" cy="12" r="10" />
              <polyline points="12 6 12 12 16 14" />
            </svg>
            <span className="panel-title">Uptime</span>
          </div>
          <div className="panel-body">
            <p className="data-value data-value-lg font-mono text-cyan-base">
              {formatUptime(status?.uptime ?? null)}
            </p>
            <p className="data-label mt-2">Current session</p>
          </div>
        </motion.div>

        {/* Active Mappings */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.15 }}
          className="panel"
        >
          <div className="panel-header">
            <svg
              className="w-4 h-4 text-emerald-base"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
              <polyline points="15 3 21 3 21 9" />
              <line x1="10" y1="14" x2="21" y2="3" />
            </svg>
            <span className="panel-title">Active Mappings</span>
          </div>
          <div className="panel-body">
            <p className="data-value data-value-lg text-emerald-base">
              {activeMappings.length}
            </p>
            <p className="data-label mt-2">
              {inactiveMappings.length} disabled
            </p>
          </div>
        </motion.div>

        {/* Gateway */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
          className="panel"
        >
          <div className="panel-header">
            <svg
              className="w-4 h-4 text-amber-base"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
              <line x1="8" y1="21" x2="16" y2="21" />
              <line x1="12" y1="17" x2="12" y2="21" />
            </svg>
            <span className="panel-title">Gateway</span>
          </div>
          <div className="panel-body">
            <p className="data-value text-amber-base truncate text-sm">
              {status?.gatewayUrl?.replace('wss://', '').replace('/tunnel', '') ?? '--'}
            </p>
            <p className="data-label mt-2">Target endpoint</p>
          </div>
        </motion.div>

        {/* Version */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.25 }}
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
            <span className="panel-title">Agent Version</span>
          </div>
          <div className="panel-body">
            <p className="data-value data-value-lg">
              v{status?.version ?? '0.0.0'}
            </p>
            <p className="data-label mt-2">Current build</p>
          </div>
        </motion.div>
      </div>

      {/* Quick Actions & Recent Mappings */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Quick Actions */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.3 }}
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
              <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
            </svg>
            <span className="panel-title">Quick Actions</span>
          </div>
          <div className="panel-body space-y-3">
            <Link to="/mappings/new" className="btn btn-primary w-full">
              <svg
                className="w-4 h-4"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
                  clipRule="evenodd"
                />
              </svg>
              Create New Mapping
            </Link>

            <Link to="/mappings" className="btn btn-secondary w-full">
              <svg
                className="w-4 h-4"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path d="M9 2a1 1 0 000 2h2a1 1 0 100-2H9z" />
                <path
                  fillRule="evenodd"
                  d="M4 5a2 2 0 012-2 3 3 0 003 3h2a3 3 0 003-3 2 2 0 012 2v11a2 2 0 01-2 2H6a2 2 0 01-2-2V5zm3 4a1 1 0 000 2h.01a1 1 0 100-2H7zm3 0a1 1 0 000 2h3a1 1 0 100-2h-3zm-3 4a1 1 0 100 2h.01a1 1 0 100-2H7zm3 0a1 1 0 100 2h3a1 1 0 100-2h-3z"
                  clipRule="evenodd"
                />
              </svg>
              View All Mappings
            </Link>

            <button
              onClick={handleReconnect}
              disabled={
                isReconnecting || status?.connectionStatus === 'Connected'
              }
              className="btn btn-ghost w-full"
            >
              <svg
                className="w-4 h-4"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z"
                  clipRule="evenodd"
                />
              </svg>
              Force Reconnect
            </button>
          </div>
        </motion.div>

        {/* Recent Mappings */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.35 }}
          className="panel"
        >
          <div className="panel-header">
            <svg
              className="w-4 h-4 text-emerald-base"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
              <polyline points="15 3 21 3 21 9" />
              <line x1="10" y1="14" x2="21" y2="3" />
            </svg>
            <span className="panel-title">Active Mappings</span>
          </div>
          <div className="panel-body">
            {activeMappings.length === 0 ? (
              <div className="empty-state py-8">
                <div className="empty-state-icon">
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="1.5"
                  >
                    <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
                    <polyline points="15 3 21 3 21 9" />
                    <line x1="10" y1="14" x2="21" y2="3" />
                  </svg>
                </div>
                <p className="empty-state-title">No active mappings</p>
                <p className="empty-state-description">
                  Create your first port mapping to start tunneling traffic.
                </p>
              </div>
            ) : (
              <div className="space-y-3">
                {activeMappings.slice(0, 4).map((mapping) => (
                  <Link
                    key={mapping.id}
                    to={`/mappings/${mapping.id}`}
                    className="flex items-center justify-between p-3 bg-surface-2 rounded-lg border border-border-subtle hover:border-border-emphasis transition-colors group"
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      <div className="led w-2 h-2 led-connected" />
                      <div className="min-w-0">
                        <p className="font-mono text-sm text-text-primary truncate">
                          {mapping.name}
                        </p>
                        <p className="font-mono text-xs text-text-tertiary truncate">
                          {mapping.externalDomain}
                        </p>
                      </div>
                    </div>
                    <svg
                      className="w-4 h-4 text-text-muted group-hover:text-text-secondary transition-colors shrink-0"
                      viewBox="0 0 20 20"
                      fill="currentColor"
                    >
                      <path
                        fillRule="evenodd"
                        d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z"
                        clipRule="evenodd"
                      />
                    </svg>
                  </Link>
                ))}

                {activeMappings.length > 4 && (
                  <Link
                    to="/mappings"
                    className="block text-center text-sm text-cyan-base hover:text-cyan-bright transition-colors py-2"
                  >
                    View all {activeMappings.length} mappings
                  </Link>
                )}
              </div>
            )}
          </div>
        </motion.div>
      </div>

      {/* System Info Footer */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ delay: 0.5 }}
        className="mt-8 p-4 bg-surface-1 rounded-lg border border-border-subtle"
      >
        <div className="flex flex-wrap items-center justify-between gap-4 text-xs font-mono text-text-muted">
          <div className="flex items-center gap-6">
            <span>
              Reconnect Attempts:{' '}
              <span className="text-text-secondary">
                {status?.reconnectAttempts ?? 0}
              </span>
            </span>
            {status?.lastDisconnected && (
              <span>
                Last Disconnected:{' '}
                <span className="text-text-secondary">
                  {new Date(status.lastDisconnected).toLocaleString()}
                </span>
              </span>
            )}
          </div>
          <span className="text-[10px] tracking-wider">
            OCTOPORTY AGENT CONTROL INTERFACE
          </span>
        </div>
      </motion.div>
    </div>
  )
}
