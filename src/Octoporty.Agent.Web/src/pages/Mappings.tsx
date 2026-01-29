// ═══════════════════════════════════════════════════════════════════════════
// MAPPINGS LIST PAGE
// Port mappings management with grid/table views
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useEffect } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { motion, AnimatePresence } from 'motion/react'
import clsx from 'clsx'
import { MappingCard, MappingRow } from '../components/MappingCard'
import { ConfirmDialog } from '../components/Modal'
import { useToast } from '../hooks/useToast'
import { api } from '../api/client'
import type { PortMapping } from '../types'

type ViewMode = 'grid' | 'table'
type FilterMode = 'all' | 'active' | 'disabled'

export function MappingsPage() {
  const navigate = useNavigate()
  const { addToast } = useToast()
  const [mappings, setMappings] = useState<PortMapping[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [viewMode, setViewMode] = useState<ViewMode>('grid')
  const [filter, setFilter] = useState<FilterMode>('all')
  const [searchQuery, setSearchQuery] = useState('')
  const [deleteTarget, setDeleteTarget] = useState<PortMapping | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  // Fetch mappings
  useEffect(() => {
    api
      .getMappings()
      .then(setMappings)
      .catch((err) => {
        addToast('error', 'Failed to load mappings', err.message)
      })
      .finally(() => {
        setIsLoading(false)
      })
  }, [addToast])

  // Filter and search mappings
  const filteredMappings = mappings.filter((mapping) => {
    // Filter by status
    if (filter === 'active' && !mapping.enabled) return false
    if (filter === 'disabled' && mapping.enabled) return false

    // Filter by search query
    if (searchQuery) {
      const query = searchQuery.toLowerCase()
      return (
        mapping.name.toLowerCase().includes(query) ||
        mapping.externalDomain.toLowerCase().includes(query) ||
        mapping.internalHost.toLowerCase().includes(query)
      )
    }

    return true
  })

  const handleEdit = (mapping: PortMapping) => {
    navigate(`/mappings/${mapping.id}`)
  }

  const handleDelete = async () => {
    if (!deleteTarget) return

    setIsDeleting(true)
    try {
      await api.deleteMapping(deleteTarget.id)
      setMappings((prev) => prev.filter((m) => m.id !== deleteTarget.id))
      addToast('success', 'Mapping deleted', `"${deleteTarget.name}" has been removed`)
      setDeleteTarget(null)
    } catch (err) {
      const error = err as Error
      addToast('error', 'Delete failed', error.message)
    } finally {
      setIsDeleting(false)
    }
  }

  const handleToggle = async (mapping: PortMapping, enabled: boolean) => {
    try {
      const updated = await api.toggleMapping(mapping.id, enabled)
      setMappings((prev) =>
        prev.map((m) => (m.id === mapping.id ? updated : m))
      )
      addToast(
        'success',
        enabled ? 'Mapping enabled' : 'Mapping disabled',
        `"${mapping.name}" is now ${enabled ? 'active' : 'inactive'}`
      )
    } catch (err) {
      const error = err as Error
      addToast('error', 'Update failed', error.message)
    }
  }

  const activeCount = mappings.filter((m) => m.enabled).length
  const disabledCount = mappings.filter((m) => !m.enabled).length

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
          <h1 className="page-title">Port Mappings</h1>
          <p className="page-subtitle">
            Configure and manage your tunnel endpoints
          </p>
        </div>

        <Link to="/mappings/new" className="btn btn-primary">
          <svg className="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
            <path
              fillRule="evenodd"
              d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
              clipRule="evenodd"
            />
          </svg>
          New Mapping
        </Link>
      </div>

      {/* Filters and Controls */}
      <div className="panel mb-6">
        <div className="p-4 flex flex-col gap-4">
          {/* Search Row */}
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
              <svg
                className="w-4 h-4 text-text-muted"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z"
                  clipRule="evenodd"
                />
              </svg>
            </div>
            <input
              type="text"
              className="input py-2"
              style={{ paddingLeft: '2.75rem' }}
              placeholder="Search mappings..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>

          {/* Filter Pills and View Toggle Row */}
          <div className="flex flex-wrap items-center justify-between gap-3">
            {/* Filter Pills */}
            <div className="flex flex-wrap items-center gap-2">
              <button
                className={clsx(
                  'px-3 py-1.5 rounded-md font-mono text-xs font-semibold whitespace-nowrap transition-colors',
                  filter === 'all'
                    ? 'bg-cyan-glow text-cyan-base border border-cyan-dim'
                    : 'bg-surface-2 text-text-secondary hover:text-text-primary'
                )}
                onClick={() => setFilter('all')}
              >
                All ({mappings.length})
              </button>
              <button
                className={clsx(
                  'px-3 py-1.5 rounded-md font-mono text-xs font-semibold whitespace-nowrap transition-colors',
                  filter === 'active'
                    ? 'bg-emerald-glow text-emerald-base border border-emerald-dim'
                    : 'bg-surface-2 text-text-secondary hover:text-text-primary'
                )}
                onClick={() => setFilter('active')}
              >
                Active ({activeCount})
              </button>
              <button
                className={clsx(
                  'px-3 py-1.5 rounded-md font-mono text-xs font-semibold whitespace-nowrap transition-colors',
                  filter === 'disabled'
                    ? 'bg-amber-glow text-amber-base border border-amber-dim'
                    : 'bg-surface-2 text-text-secondary hover:text-text-primary'
                )}
                onClick={() => setFilter('disabled')}
              >
                Disabled ({disabledCount})
              </button>
            </div>

            {/* View Toggle */}
            <div className="flex items-center gap-1 bg-surface-2 rounded-md p-1 shrink-0">
              <button
                className={clsx(
                  'p-2 rounded transition-colors',
                  viewMode === 'grid'
                    ? 'bg-surface-4 text-text-primary'
                    : 'text-text-muted hover:text-text-secondary'
                )}
                onClick={() => setViewMode('grid')}
                title="Grid view"
              >
                <svg className="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
                  <path d="M5 3a2 2 0 00-2 2v2a2 2 0 002 2h2a2 2 0 002-2V5a2 2 0 00-2-2H5zM5 11a2 2 0 00-2 2v2a2 2 0 002 2h2a2 2 0 002-2v-2a2 2 0 00-2-2H5zM11 5a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V5zM11 13a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z" />
                </svg>
              </button>
              <button
                className={clsx(
                  'p-2 rounded transition-colors',
                  viewMode === 'table'
                    ? 'bg-surface-4 text-text-primary'
                    : 'text-text-muted hover:text-text-secondary'
                )}
                onClick={() => setViewMode('table')}
                title="Table view"
              >
                <svg className="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
                  <path
                    fillRule="evenodd"
                    d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z"
                    clipRule="evenodd"
                  />
                </svg>
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Mappings Display */}
      {filteredMappings.length === 0 ? (
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          className="panel"
        >
          <div className="empty-state py-16">
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
            <h3 className="empty-state-title">
              {searchQuery
                ? 'No mappings found'
                : filter !== 'all'
                ? `No ${filter} mappings`
                : 'No mappings yet'}
            </h3>
            <p className="empty-state-description">
              {searchQuery
                ? `No mappings match "${searchQuery}". Try a different search term.`
                : 'Create your first port mapping to start tunneling traffic through Octoporty.'}
            </p>
            {!searchQuery && filter === 'all' && (
              <Link to="/mappings/new" className="btn btn-primary mt-4">
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
                Create First Mapping
              </Link>
            )}
          </div>
        </motion.div>
      ) : viewMode === 'grid' ? (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
          <AnimatePresence mode="popLayout">
            {filteredMappings.map((mapping, index) => (
              <MappingCard
                key={mapping.id}
                mapping={mapping}
                index={index}
                onEdit={handleEdit}
                onDelete={setDeleteTarget}
                onToggle={handleToggle}
              />
            ))}
          </AnimatePresence>
        </div>
      ) : (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          className="panel overflow-hidden"
        >
          <div className="overflow-x-auto">
            <table className="table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>External Endpoint</th>
                  <th>Internal Target</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredMappings.map((mapping) => (
                  <MappingRow
                    key={mapping.id}
                    mapping={mapping}
                    onEdit={handleEdit}
                    onDelete={setDeleteTarget}
                    onToggle={handleToggle}
                  />
                ))}
              </tbody>
            </table>
          </div>
        </motion.div>
      )}

      {/* Summary */}
      {filteredMappings.length > 0 && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 0.3 }}
          className="mt-6 text-center text-xs font-mono text-text-muted"
        >
          Showing {filteredMappings.length} of {mappings.length} mappings
        </motion.div>
      )}

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDelete}
        title="Delete Mapping"
        message={`Are you sure you want to delete "${deleteTarget?.name}"? This will immediately stop all traffic through this tunnel endpoint.`}
        confirmLabel="Delete"
        cancelLabel="Cancel"
        variant="danger"
        isLoading={isDeleting}
      />
    </div>
  )
}
