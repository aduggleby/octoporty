import { AnimatePresence, motion } from 'motion/react'

interface GatewayUpdatePendingModalProps {
  isOpen: boolean
  onClose: () => void
  agentVersion: string
  gatewayVersion?: string | null
  isChecking: boolean
  isGatewayReachable: boolean | null
  lastCheckedAt: Date | null
  checkError: string | null
}

export function GatewayUpdatePendingModal({
  isOpen,
  onClose,
  agentVersion,
  gatewayVersion,
  isChecking,
  isGatewayReachable,
  lastCheckedAt,
  checkError,
}: GatewayUpdatePendingModalProps) {
  const statusLabel = isChecking
    ? 'Checking gateway status...'
    : isGatewayReachable === true
      ? 'Gateway reachable. Waiting for version update.'
      : isGatewayReachable === false
        ? 'Gateway is not reachable. Waiting to retry.'
        : 'Waiting for first gateway status check.'

  return (
    <AnimatePresence>
      {isOpen && (
        <div className="fixed inset-0 z-[220] bg-surface-0/95 backdrop-blur-sm overflow-y-auto">
          <motion.div
            initial={{ opacity: 0, scale: 0.98 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.98 }}
            transition={{ duration: 0.15 }}
            className="min-h-screen flex items-center justify-center p-6"
          >
            <div className="w-full max-w-3xl panel">
              <div className="panel-header">
                <div className="flex-1">
                  <h2 className="font-display text-2xl text-text-primary">Gateway Update Pending</h2>
                  <p className="font-mono text-xs text-text-tertiary mt-1">
                    Waiting for the Gateway to restart and report the new version.
                  </p>
                </div>
                <button
                  onClick={onClose}
                  className="btn btn-secondary btn-sm"
                  type="button"
                >
                  Close
                </button>
              </div>

              <div className="panel-body space-y-5">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="bg-surface-2 border border-border-default rounded-lg p-4">
                    <p className="data-label">Agent Version</p>
                    <p className="font-mono text-xl text-cyan-base mt-2">v{agentVersion}</p>
                  </div>
                  <div className="bg-surface-2 border border-border-default rounded-lg p-4">
                    <p className="data-label">Gateway Version</p>
                    <p className="font-mono text-xl text-amber-base mt-2">
                      {gatewayVersion ? `v${gatewayVersion}` : 'Unknown'}
                    </p>
                  </div>
                </div>

                <div className="bg-surface-2 border border-border-default rounded-lg p-4 space-y-3">
                  <div className="flex items-center gap-2">
                    <div
                      className={`led ${
                        isGatewayReachable === false
                          ? 'led-disconnected'
                          : isGatewayReachable === true
                            ? 'led-connected'
                            : 'led-connecting'
                      }`}
                    />
                    <p className="font-mono text-sm text-text-secondary">{statusLabel}</p>
                  </div>

                  <p className="font-mono text-xs text-text-tertiary">
                    Last checked:{' '}
                    {lastCheckedAt
                      ? lastCheckedAt.toLocaleTimeString('en-US', {
                          hour12: false,
                          hour: '2-digit',
                          minute: '2-digit',
                          second: '2-digit',
                        })
                      : 'Never'}
                  </p>

                  {checkError && (
                    <p className="text-xs text-rose-base font-mono">
                      Gateway check failed: {checkError}
                    </p>
                  )}
                </div>
              </div>
            </div>
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  )
}
