// ═══════════════════════════════════════════════════════════════════════════
// CONNECTION STATUS INDICATOR
// Real-time tunnel connection status with industrial aesthetic
// ═══════════════════════════════════════════════════════════════════════════

import { motion } from 'motion/react'
import type { ConnectionStatus as ConnectionStatusType } from '../types'
import clsx from 'clsx'

interface ConnectionStatusProps {
  status: ConnectionStatusType
  gatewayUrl?: string
  lastConnected?: string | null
  compact?: boolean
  onReconnect?: () => void
}

const statusConfig: Record<
  ConnectionStatusType,
  { label: string; ledClass: string; description: string }
> = {
  Connected: {
    label: 'ONLINE',
    ledClass: 'led-connected',
    description: 'Tunnel active',
  },
  Disconnected: {
    label: 'OFFLINE',
    ledClass: 'led-disconnected',
    description: 'Tunnel inactive',
  },
  Connecting: {
    label: 'CONNECTING',
    ledClass: 'led-connecting',
    description: 'Establishing tunnel...',
  },
  Reconnecting: {
    label: 'RECONNECTING',
    ledClass: 'led-connecting',
    description: 'Restoring connection...',
  },
}

export function ConnectionStatus({
  status,
  gatewayUrl,
  lastConnected,
  compact = false,
  onReconnect,
}: ConnectionStatusProps) {
  const config = statusConfig[status]

  if (compact) {
    return (
      <div className="flex items-center gap-2">
        <div className={clsx('led', config.ledClass)} />
        <span className="font-mono text-xs font-semibold tracking-wider text-text-secondary">
          {config.label}
        </span>
      </div>
    )
  }

  return (
    <motion.div
      initial={{ opacity: 0, y: -10 }}
      animate={{ opacity: 1, y: 0 }}
      className={clsx(
        'connection-panel',
        status === 'Connected' && 'connection-panel-connected',
        status === 'Disconnected' && 'connection-panel-disconnected'
      )}
    >
      {/* LED Indicator */}
      <div className="relative">
        <div className={clsx('led w-3 h-3', config.ledClass)} />
      </div>

      {/* Status Text */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-3">
          <span className="font-mono text-sm font-bold tracking-wider text-text-primary">
            {config.label}
          </span>
          <span
            className={clsx(
              'badge',
              status === 'Connected' && 'badge-success',
              status === 'Disconnected' && 'badge-error',
              (status === 'Connecting' || status === 'Reconnecting') &&
                'badge-warning'
            )}
          >
            {config.description}
          </span>
        </div>

        {gatewayUrl && (
          <p className="font-mono text-xs text-text-tertiary mt-1 truncate">
            {gatewayUrl}
          </p>
        )}
      </div>

      {/* Reconnect Button */}
      {status === 'Disconnected' && onReconnect && (
        <button onClick={onReconnect} className="btn btn-secondary btn-sm">
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
          Reconnect
        </button>
      )}

      {/* Last Connected Time */}
      {lastConnected && status === 'Disconnected' && (
        <div className="text-right">
          <p className="data-label">Last Connected</p>
          <p className="font-mono text-xs text-text-secondary">
            {new Date(lastConnected).toLocaleString()}
          </p>
        </div>
      )}

      {/* Data Flow Animation (when connected) */}
      {status === 'Connected' && (
        <div className="relative w-24 h-2 bg-surface-2 rounded-full overflow-hidden">
          <motion.div
            className="absolute inset-y-0 w-8 bg-gradient-to-r from-transparent via-cyan-base to-transparent rounded-full"
            animate={{ x: [-32, 96] }}
            transition={{
              duration: 1.5,
              repeat: Infinity,
              ease: 'linear',
            }}
          />
        </div>
      )}
    </motion.div>
  )
}

// Smaller inline status for sidebar/header
export function ConnectionStatusBadge({
  status,
}: {
  status: ConnectionStatusType
}) {
  const config = statusConfig[status]

  return (
    <div
      className={clsx(
        'inline-flex items-center gap-2 px-3 py-1.5 rounded-md',
        status === 'Connected' && 'bg-emerald-glow',
        status === 'Disconnected' && 'bg-rose-glow',
        (status === 'Connecting' || status === 'Reconnecting') && 'bg-amber-glow'
      )}
    >
      <div className={clsx('led w-2 h-2', config.ledClass)} />
      <span className="font-mono text-[10px] font-bold tracking-wider">
        {config.label}
      </span>
    </div>
  )
}
