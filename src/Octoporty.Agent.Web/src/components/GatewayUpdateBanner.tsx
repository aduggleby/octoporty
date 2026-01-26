// GatewayUpdateBanner.tsx
// Displays a notification banner when a Gateway update is available.
// Allows the user to trigger the update with a single click.
// Shows loading state during the update request.

import { useState } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { api } from '../api/client'
import { useToast } from '../hooks/useToast'

interface GatewayUpdateBannerProps {
  agentVersion: string
  gatewayVersion?: string | null
  visible: boolean
  onUpdateTriggered?: () => void
}

export function GatewayUpdateBanner({
  agentVersion,
  gatewayVersion,
  visible,
  onUpdateTriggered,
}: GatewayUpdateBannerProps) {
  const [isUpdating, setIsUpdating] = useState(false)
  const [dismissed, setDismissed] = useState(false)
  const { addToast } = useToast()

  const handleUpdateClick = async () => {
    setIsUpdating(true)
    try {
      const response = await api.triggerGatewayUpdate()

      if (response.success) {
        addToast('success', 'Update Queued', response.message)
        onUpdateTriggered?.()
        // Keep banner visible but disabled until reconnect
      } else {
        addToast('error', 'Update Failed', response.error ?? response.message)
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error'
      addToast('error', 'Update Failed', message)
    } finally {
      setIsUpdating(false)
    }
  }

  const handleDismiss = () => {
    setDismissed(true)
  }

  // Don't show if not visible, dismissed, or versions match
  if (!visible || dismissed) {
    return null
  }

  return (
    <AnimatePresence>
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        exit={{ opacity: 0, y: -20 }}
        className="bg-amber-glow border border-amber-dim rounded-lg p-4 mb-6"
      >
        <div className="flex items-start gap-4">
          {/* Icon */}
          <div className="text-amber-base flex-shrink-0">
            <svg
              className="w-6 h-6"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          </div>

          {/* Content */}
          <div className="flex-1 min-w-0">
            <h3 className="font-mono text-sm font-bold text-amber-base tracking-wider">
              GATEWAY UPDATE AVAILABLE
            </h3>
            <p className="text-sm text-text-secondary mt-1">
              Your Agent is running a newer version than the Gateway.
              Update the Gateway to ensure compatibility.
            </p>
            <p className="font-mono text-xs text-text-tertiary mt-2">
              Agent v{agentVersion} → Gateway v{gatewayVersion ?? 'unknown'}
            </p>
          </div>

          {/* Actions */}
          <div className="flex items-center gap-2 flex-shrink-0">
            <button
              onClick={handleUpdateClick}
              disabled={isUpdating}
              className="btn btn-warning btn-sm flex items-center gap-2"
            >
              {isUpdating ? (
                <>
                  <svg
                    className="w-4 h-4 animate-spin"
                    viewBox="0 0 24 24"
                    fill="none"
                  >
                    <circle
                      className="opacity-25"
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="currentColor"
                      strokeWidth="4"
                    />
                    <path
                      className="opacity-75"
                      fill="currentColor"
                      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
                    />
                  </svg>
                  Updating...
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
                    <path d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
                  </svg>
                  Update Gateway
                </>
              )}
            </button>

            <button
              onClick={handleDismiss}
              className="text-text-muted hover:text-text-secondary transition-colors p-1"
              title="Dismiss"
            >
              <svg className="w-5 h-5" viewBox="0 0 20 20" fill="currentColor">
                <path
                  fillRule="evenodd"
                  d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z"
                  clipRule="evenodd"
                />
              </svg>
            </button>
          </div>
        </div>
      </motion.div>
    </AnimatePresence>
  )
}
