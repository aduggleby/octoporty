// ═══════════════════════════════════════════════════════════════════════════
// CADDY CONFIG PAGE
// Displays the current Caddy reverse proxy configuration from the Gateway
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useEffect, useCallback } from 'react'
import { motion } from 'motion/react'
import { useToast } from '../hooks/useToast'
import { api, type CaddyConfigResponse } from '../api/client'

export function CaddyConfigPage() {
  const { addToast } = useToast()
  const [config, setConfig] = useState<CaddyConfigResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isRefreshing, setIsRefreshing] = useState(false)

  const fetchConfig = useCallback(async (showToast: boolean = false) => {
    try {
      const response = await api.getCaddyConfig()
      setConfig(response)
      if (showToast && response.success) {
        addToast('success', 'Config refreshed', 'Caddy configuration updated')
      } else if (!response.success) {
        addToast('error', 'Failed to load config', response.error || 'Unknown error')
      }
    } catch (err) {
      const error = err as Error
      addToast('error', 'Failed to load config', error.message)
      setConfig({ success: false, error: error.message })
    }
  }, [addToast])

  useEffect(() => {
    setIsLoading(true)
    fetchConfig().finally(() => setIsLoading(false))
  }, [fetchConfig])

  const handleRefresh = async () => {
    setIsRefreshing(true)
    await fetchConfig(true)
    setIsRefreshing(false)
  }

  // Format JSON for display
  const formattedJson = config?.configJson
    ? JSON.stringify(JSON.parse(config.configJson), null, 2)
    : null

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
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 mb-8">
        <div>
          <h1 className="page-title">Caddy Configuration</h1>
          <p className="page-subtitle">
            Current reverse proxy configuration from the Gateway
          </p>
        </div>

        <button
          onClick={handleRefresh}
          disabled={isRefreshing}
          className="btn btn-secondary"
        >
          {isRefreshing ? (
            <>
              <motion.svg
                className="w-4 h-4"
                viewBox="0 0 24 24"
                animate={{ rotate: 360 }}
                transition={{ duration: 1, repeat: Infinity, ease: 'linear' }}
              >
                <circle
                  className="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  strokeWidth="4"
                  fill="none"
                />
                <path
                  className="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                />
              </motion.svg>
              Refreshing...
            </>
          ) : (
            <>
              <svg
                className="w-4 h-4"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
              >
                <path d="M21 12a9 9 0 1 1-6.219-8.56" />
                <polyline points="21 3 21 9 15 9" />
              </svg>
              Refresh
            </>
          )}
        </button>
      </div>

      {/* Config Display */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
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
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
            <polyline points="14 2 14 8 20 8" />
            <line x1="16" y1="13" x2="8" y2="13" />
            <line x1="16" y1="17" x2="8" y2="17" />
            <polyline points="10 9 9 9 8 9" />
          </svg>
          <span className="panel-title">JSON Configuration</span>
        </div>
        <div className="panel-body p-0">
          {config?.success && formattedJson ? (
            <pre className="p-4 overflow-x-auto text-sm font-mono text-text-secondary bg-surface-1 rounded-b-lg max-h-[70vh] overflow-y-auto">
              <code>{formattedJson}</code>
            </pre>
          ) : (
            <div className="p-8 text-center">
              <div className="text-rose-base mb-2">
                <svg
                  className="w-12 h-12 mx-auto opacity-50"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.5"
                >
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="8" x2="12" y2="12" />
                  <line x1="12" y1="16" x2="12.01" y2="16" />
                </svg>
              </div>
              <p className="text-text-tertiary">
                {config?.error || 'Failed to load Caddy configuration'}
              </p>
            </div>
          )}
        </div>
      </motion.div>

      {/* Info Panel */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.1 }}
        className="panel mt-6"
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
          <span className="panel-title">About Caddy Configuration</span>
        </div>
        <div className="panel-body">
          <p className="text-sm text-text-secondary">
            This shows the live configuration of the Caddy reverse proxy running on the Gateway.
            Routes are automatically created and removed when you add or delete port mappings.
            Each route forwards traffic from an external domain through the tunnel to your internal service.
          </p>
          <div className="mt-4 p-3 bg-surface-2 rounded-md">
            <p className="text-xs font-mono text-text-muted">
              Routes are identified by <code className="text-cyan-base">octoporty-[mapping-id]</code> in the configuration.
            </p>
          </div>
        </div>
      </motion.div>
    </div>
  )
}
