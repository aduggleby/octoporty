// ═══════════════════════════════════════════════════════════════════════════
// MAPPING DETAIL/EDIT PAGE
// Create or edit a port mapping configuration
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useEffect } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { MappingForm } from '../components/MappingForm'
import { ConfirmDialog } from '../components/Modal'
import { useToast } from '../hooks/useToast'
import { api } from '../api/client'
import type { PortMapping, CreateMappingRequest } from '../types'

export function MappingDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { addToast } = useToast()

  const isNew = id === 'new'
  const [mapping, setMapping] = useState<PortMapping | null>(null)
  const [isLoading, setIsLoading] = useState(!isNew)
  const [isSaving, setIsSaving] = useState(false)
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false)
  const [isDeleting, setIsDeleting] = useState(false)

  // Fetch existing mapping
  useEffect(() => {
    // Skip if this is a new mapping or no ID is provided
    if (isNew || !id) {
      setIsLoading(false)
      return
    }

    // Ensure loading state is set
    setIsLoading(true)
    let cancelled = false

    api
      .getMapping(id)
      .then((data) => {
        if (!cancelled) {
          setMapping(data)
        }
      })
      .catch((err) => {
        if (!cancelled) {
          addToast('error', 'Failed to load mapping', err.message || 'Unknown error')
          navigate('/mappings')
        }
      })
      .finally(() => {
        if (!cancelled) {
          setIsLoading(false)
        }
      })

    // Cleanup function to prevent state updates on unmounted component
    return () => {
      cancelled = true
    }
  }, [id, isNew, navigate, addToast])

  const handleSubmit = async (data: CreateMappingRequest) => {
    setIsSaving(true)
    try {
      if (isNew) {
        const created = await api.createMapping(data)
        addToast('success', 'Mapping created', `"${created.name}" is now active`)
        navigate('/mappings')
      } else if (id) {
        const updated = await api.updateMapping({ ...data, id })
        addToast('success', 'Mapping updated', `"${updated.name}" has been saved`)
        setMapping(updated)
      }
    } catch (err) {
      const error = err as Error
      addToast('error', 'Save failed', error.message)
    } finally {
      setIsSaving(false)
    }
  }

  const handleDelete = async () => {
    if (!mapping) return

    setIsDeleting(true)
    try {
      await api.deleteMapping(mapping.id)
      addToast('success', 'Mapping deleted', `"${mapping.name}" has been removed`)
      navigate('/mappings')
    } catch (err) {
      const error = err as Error
      addToast('error', 'Delete failed', error.message)
    } finally {
      setIsDeleting(false)
      setShowDeleteConfirm(false)
    }
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
      {/* Breadcrumb */}
      <nav className="mb-6">
        <ol className="flex items-center gap-2 text-sm font-mono">
          <li>
            <Link
              to="/mappings"
              className="text-text-tertiary hover:text-text-secondary transition-colors"
            >
              Mappings
            </Link>
          </li>
          <li className="text-text-muted">/</li>
          <li className="text-text-primary">
            {isNew ? 'New Mapping' : mapping?.name ?? 'Edit'}
          </li>
        </ol>
      </nav>

      {/* Page Header */}
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 mb-8">
        <div>
          <h1 className="page-title">
            {isNew ? 'Create Mapping' : 'Edit Mapping'}
          </h1>
          <p className="page-subtitle">
            {isNew
              ? 'Configure a new tunnel endpoint'
              : 'Update tunnel configuration'}
          </p>
        </div>

        {!isNew && mapping && (
          <button
            onClick={() => setShowDeleteConfirm(true)}
            className="btn btn-ghost text-rose-base hover:bg-rose-glow"
          >
            <svg className="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
              <path
                fillRule="evenodd"
                d="M9 2a1 1 0 00-.894.553L7.382 4H4a1 1 0 000 2v10a2 2 0 002 2h8a2 2 0 002-2V6a1 1 0 100-2h-3.382l-.724-1.447A1 1 0 0011 2H9zM7 8a1 1 0 012 0v6a1 1 0 11-2 0V8zm5-1a1 1 0 00-1 1v6a1 1 0 102 0V8a1 1 0 00-1-1z"
                clipRule="evenodd"
              />
            </svg>
            Delete Mapping
          </button>
        )}
      </div>

      {/* Form Panel */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="panel max-w-2xl"
      >
        <div className="panel-header">
          <svg
            className="w-4 h-4 text-cyan-base"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          >
            <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
            <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
          </svg>
          <span className="panel-title">Mapping Configuration</span>
        </div>
        <div className="panel-body">
          <MappingForm
            mapping={mapping}
            onSubmit={handleSubmit}
            onCancel={() => navigate('/mappings')}
            isLoading={isSaving}
          />
        </div>
      </motion.div>

      {/* Info Panel */}
      {!isNew && mapping && (
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
          className="panel max-w-2xl mt-6"
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
            <span className="panel-title">Mapping Info</span>
          </div>
          <div className="panel-body">
            <div className="grid grid-cols-2 gap-6">
              <div>
                <p className="data-label">ID</p>
                <p className="data-value text-sm break-all">{mapping.id}</p>
              </div>
              <div>
                <p className="data-label">Created</p>
                <p className="data-value text-sm">
                  {new Date(mapping.createdAt).toLocaleString()}
                </p>
              </div>
              <div>
                <p className="data-label">Last Updated</p>
                <p className="data-value text-sm">
                  {mapping.updatedAt
                    ? new Date(mapping.updatedAt).toLocaleString()
                    : 'Never'}
                </p>
              </div>
              <div>
                <p className="data-label">Status</p>
                <p
                  className={`data-value text-sm ${
                    mapping.enabled ? 'text-emerald-base' : 'text-amber-base'
                  }`}
                >
                  {mapping.enabled ? 'Active' : 'Disabled'}
                </p>
              </div>
            </div>

            {/* Connection Example */}
            <div className="mt-6 pt-6 border-t border-border-subtle">
              <p className="data-label mb-3">Connection Flow</p>
              <div className="terminal">
                <p>
                  <span className="terminal-prompt">External</span>{' '}
                  <span className="terminal-command">
                    https://{mapping.externalDomain}
                  </span>
                </p>
                <p className="text-text-muted my-2">
                  {'    '}|
                  <br />
                  {'    '}v (via Octoporty tunnel)
                </p>
                <p>
                  <span className="terminal-prompt">Internal</span>{' '}
                  <span className="terminal-command">
                    {mapping.internalProtocol.toLowerCase()}://{mapping.internalHost}:{mapping.internalPort}
                  </span>
                </p>
              </div>
            </div>
          </div>
        </motion.div>
      )}

      {/* Delete Confirmation */}
      <ConfirmDialog
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        title="Delete Mapping"
        message={`Are you sure you want to delete "${mapping?.name}"? This will immediately stop all traffic through this tunnel endpoint.`}
        confirmLabel="Delete"
        cancelLabel="Cancel"
        variant="danger"
        isLoading={isDeleting}
      />
    </div>
  )
}
