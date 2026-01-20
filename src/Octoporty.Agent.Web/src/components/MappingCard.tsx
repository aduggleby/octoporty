// ═══════════════════════════════════════════════════════════════════════════
// MAPPING CARD COMPONENT
// Industrial-style port mapping display card
// ═══════════════════════════════════════════════════════════════════════════

import { motion } from 'motion/react'
import clsx from 'clsx'
import type { PortMapping } from '../types'

interface MappingCardProps {
  mapping: PortMapping
  onEdit?: (mapping: PortMapping) => void
  onDelete?: (mapping: PortMapping) => void
  onToggle?: (mapping: PortMapping, enabled: boolean) => void
  index?: number
}

export function MappingCard({
  mapping,
  onEdit,
  onDelete,
  onToggle,
  index = 0,
}: MappingCardProps) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.05, duration: 0.3 }}
      className={clsx(
        'mapping-card group',
        mapping.enabled ? 'mapping-card-enabled' : 'mapping-card-disabled'
      )}
    >
      {/* Header Row */}
      <div className="flex items-start justify-between mb-4">
        <div className="flex items-center gap-3">
          {/* Mapping Icon */}
          <div
            className={clsx(
              'w-10 h-10 rounded-lg flex items-center justify-center',
              mapping.enabled
                ? 'bg-cyan-glow border border-cyan-dim'
                : 'bg-surface-3 border border-border-default'
            )}
          >
            <svg
              className={clsx(
                'w-5 h-5',
                mapping.enabled ? 'text-cyan-base' : 'text-text-muted'
              )}
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
              <polyline points="15 3 21 3 21 9" />
              <line x1="10" y1="14" x2="21" y2="3" />
            </svg>
          </div>

          {/* Name and Status */}
          <div>
            <h3 className="font-display text-base font-semibold text-text-primary">
              {mapping.name}
            </h3>
            <div className="flex items-center gap-2 mt-1">
              <span
                className={clsx(
                  'badge',
                  mapping.enabled ? 'badge-success' : 'badge-warning'
                )}
              >
                {mapping.enabled ? 'ACTIVE' : 'DISABLED'}
              </span>
              {mapping.internalProtocol === 'Https' &&
                mapping.allowInvalidCertificates && (
                  <span className="badge badge-warning">INSECURE SSL</span>
                )}
            </div>
          </div>
        </div>

        {/* Toggle Switch */}
        <button
          className="toggle"
          data-checked={mapping.enabled}
          onClick={() => onToggle?.(mapping, !mapping.enabled)}
          aria-label={mapping.enabled ? 'Disable mapping' : 'Enable mapping'}
        />
      </div>

      {/* Port Mapping Display */}
      <div className="port-display mb-4">
        <div className="port-external">
          <span className="block text-[10px] text-cyan-dim font-semibold mb-0.5">
            EXTERNAL
          </span>
          <span className="text-sm">
            {mapping.externalDomain}:{mapping.externalPort}
          </span>
        </div>

        <div className="port-arrow relative">
          <svg
            className="w-6 h-6"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          >
            <path d="M5 12h14M12 5l7 7-7 7" />
          </svg>
          {/* Data flow animation */}
          {mapping.enabled && (
            <motion.div
              className="absolute inset-0 flex items-center"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
            >
              <motion.div
                className="w-1.5 h-1.5 rounded-full bg-cyan-base"
                animate={{ x: [0, 24] }}
                transition={{
                  duration: 0.8,
                  repeat: Infinity,
                  ease: 'linear',
                }}
              />
            </motion.div>
          )}
        </div>

        <div className="port-internal">
          <span className="block text-[10px] text-text-muted font-semibold mb-0.5">
            INTERNAL
          </span>
          <span className="text-sm">
            {mapping.internalProtocol.toLowerCase()}://{mapping.internalHost}:
            {mapping.internalPort}
          </span>
        </div>
      </div>

      {/* Footer Actions */}
      <div className="flex items-center justify-between pt-4 border-t border-border-subtle">
        <div className="text-[10px] text-text-muted font-mono">
          Created {new Date(mapping.createdAt).toLocaleDateString()}
        </div>

        <div className="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
          <button
            onClick={() => onEdit?.(mapping)}
            className="btn btn-ghost btn-sm"
          >
            <svg
              className="w-4 h-4"
              viewBox="0 0 20 20"
              fill="currentColor"
            >
              <path d="M13.586 3.586a2 2 0 112.828 2.828l-.793.793-2.828-2.828.793-.793zM11.379 5.793L3 14.172V17h2.828l8.38-8.379-2.83-2.828z" />
            </svg>
            Edit
          </button>
          <button
            onClick={() => onDelete?.(mapping)}
            className="btn btn-ghost btn-sm text-rose-base hover:bg-rose-glow"
          >
            <svg
              className="w-4 h-4"
              viewBox="0 0 20 20"
              fill="currentColor"
            >
              <path
                fillRule="evenodd"
                d="M9 2a1 1 0 00-.894.553L7.382 4H4a1 1 0 000 2v10a2 2 0 002 2h8a2 2 0 002-2V6a1 1 0 100-2h-3.382l-.724-1.447A1 1 0 0011 2H9zM7 8a1 1 0 012 0v6a1 1 0 11-2 0V8zm5-1a1 1 0 00-1 1v6a1 1 0 102 0V8a1 1 0 00-1-1z"
                clipRule="evenodd"
              />
            </svg>
            Delete
          </button>
        </div>
      </div>
    </motion.div>
  )
}

// Compact list view variant
export function MappingRow({
  mapping,
  onEdit,
  onDelete,
  onToggle,
}: MappingCardProps) {
  return (
    <tr className="group">
      <td>
        <div className="flex items-center gap-3">
          <div
            className={clsx(
              'led w-2 h-2',
              mapping.enabled ? 'led-connected' : 'led-disconnected'
            )}
          />
          <span className="font-mono font-medium text-text-primary">
            {mapping.name}
          </span>
        </div>
      </td>
      <td className="font-mono text-cyan-base">
        {mapping.externalDomain}:{mapping.externalPort}
      </td>
      <td className="font-mono text-text-secondary">
        {mapping.internalProtocol.toLowerCase()}://{mapping.internalHost}:
        {mapping.internalPort}
      </td>
      <td>
        <span
          className={clsx(
            'badge',
            mapping.enabled ? 'badge-success' : 'badge-warning'
          )}
        >
          {mapping.enabled ? 'ACTIVE' : 'DISABLED'}
        </span>
      </td>
      <td>
        <div className="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
          <button
            onClick={() => onToggle?.(mapping, !mapping.enabled)}
            className="btn btn-ghost btn-sm"
          >
            {mapping.enabled ? 'Disable' : 'Enable'}
          </button>
          <button
            onClick={() => onEdit?.(mapping)}
            className="btn btn-ghost btn-sm"
          >
            Edit
          </button>
          <button
            onClick={() => onDelete?.(mapping)}
            className="btn btn-ghost btn-sm text-rose-base"
          >
            Delete
          </button>
        </div>
      </td>
    </tr>
  )
}
