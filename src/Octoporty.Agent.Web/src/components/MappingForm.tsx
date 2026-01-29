// ═══════════════════════════════════════════════════════════════════════════
// MAPPING FORM COMPONENT
// Create/Edit port mapping form with validation
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useEffect } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import clsx from 'clsx'
import type { PortMapping, CreateMappingRequest, Protocol } from '../types'

interface MappingFormProps {
  mapping?: PortMapping | null
  onSubmit: (data: CreateMappingRequest) => Promise<void>
  onCancel: () => void
  isLoading?: boolean
}

interface FormErrors {
  name?: string
  externalDomain?: string
  internalHost?: string
  internalPort?: string
}

export function MappingForm({
  mapping,
  onSubmit,
  onCancel,
  isLoading = false,
}: MappingFormProps) {
  const isEditing = !!mapping

  const [formData, setFormData] = useState<CreateMappingRequest>({
    name: mapping?.name ?? '',
    externalDomain: mapping?.externalDomain ?? '',
    internalHost: mapping?.internalHost ?? 'localhost',
    internalPort: mapping?.internalPort ?? 80,
    internalProtocol: mapping?.internalProtocol ?? 'Http',
    allowInvalidCertificates: mapping?.allowInvalidCertificates ?? false,
    enabled: mapping?.enabled ?? true,
  })

  const [errors, setErrors] = useState<FormErrors>({})
  const [touched, setTouched] = useState<Record<string, boolean>>({})

  // Reset form when mapping changes
  useEffect(() => {
    if (mapping) {
      setFormData({
        name: mapping.name,
        externalDomain: mapping.externalDomain,
        internalHost: mapping.internalHost,
        internalPort: mapping.internalPort,
        internalProtocol: mapping.internalProtocol,
        allowInvalidCertificates: mapping.allowInvalidCertificates,
        enabled: mapping.enabled,
      })
    }
  }, [mapping])

  const validate = (): boolean => {
    const newErrors: FormErrors = {}

    if (!formData.name.trim()) {
      newErrors.name = 'Name is required'
    } else if (formData.name.length > 50) {
      newErrors.name = 'Name must be 50 characters or less'
    }

    if (!formData.externalDomain.trim()) {
      newErrors.externalDomain = 'External domain is required'
    } else if (!/^[a-zA-Z0-9][a-zA-Z0-9.-]+[a-zA-Z0-9]$/.test(formData.externalDomain)) {
      newErrors.externalDomain = 'Invalid domain format'
    }

    if (!formData.internalHost.trim()) {
      newErrors.internalHost = 'Internal host is required'
    }

    if (!formData.internalPort || formData.internalPort < 1 || formData.internalPort > 65535) {
      newErrors.internalPort = 'Port must be between 1 and 65535'
    }

    setErrors(newErrors)
    return Object.keys(newErrors).length === 0
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setTouched({
      name: true,
      externalDomain: true,
      internalHost: true,
      internalPort: true,
    })

    if (validate()) {
      await onSubmit(formData)
    }
  }

  const handleBlur = (field: string) => {
    setTouched((prev) => ({ ...prev, [field]: true }))
    validate()
  }

  const updateField = <K extends keyof CreateMappingRequest>(
    field: K,
    value: CreateMappingRequest[K]
  ) => {
    setFormData((prev) => ({ ...prev, [field]: value }))
    if (touched[field]) {
      validate()
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {/* Mapping Name */}
      <div>
        <label className="label">Mapping Name</label>
        <input
          type="text"
          className={clsx('input', touched.name && errors.name && 'input-error')}
          placeholder="e.g., My Web App"
          value={formData.name}
          onChange={(e) => updateField('name', e.target.value)}
          onBlur={() => handleBlur('name')}
          disabled={isLoading}
        />
        <AnimatePresence>
          {touched.name && errors.name && (
            <motion.p
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              className="text-rose-base text-xs mt-2 font-mono"
            >
              {errors.name}
            </motion.p>
          )}
        </AnimatePresence>
      </div>

      {/* External Configuration */}
      <div className="panel p-4">
        <div className="panel-header -m-4 mb-4 px-4 py-3">
          <svg
            className="w-4 h-4 text-cyan-base"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          >
            <circle cx="12" cy="12" r="10" />
            <line x1="2" y1="12" x2="22" y2="12" />
            <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
          </svg>
          <span className="panel-title">External Endpoint</span>
        </div>

        <div>
          <label className="label">Domain</label>
          <div className="flex items-center gap-3">
            <input
              type="text"
              className={clsx(
                'input flex-1',
                touched.externalDomain && errors.externalDomain && 'input-error'
              )}
              placeholder="app.example.com"
              value={formData.externalDomain}
              onChange={(e) => updateField('externalDomain', e.target.value)}
              onBlur={() => handleBlur('externalDomain')}
              disabled={isLoading}
            />
            <div className="flex items-center gap-1.5 px-3 py-2 bg-emerald-glow border border-emerald-dim rounded-md shrink-0">
              <svg
                className="w-4 h-4 text-emerald-base"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
              >
                <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                <path d="M7 11V7a5 5 0 0 1 10 0v4" />
              </svg>
              <span className="text-xs font-mono font-semibold text-emerald-base">
                HTTPS
              </span>
            </div>
          </div>
          {touched.externalDomain && errors.externalDomain && (
            <p className="text-rose-base text-xs mt-2 font-mono">
              {errors.externalDomain}
            </p>
          )}
          <p className="text-text-muted text-xs mt-2">
            External endpoints always use HTTPS with automatic certificate provisioning
          </p>
        </div>
      </div>

      {/* Internal Configuration */}
      <div className="panel p-4">
        <div className="panel-header -m-4 mb-4 px-4 py-3">
          <svg
            className="w-4 h-4 text-amber-base"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          >
            <rect x="2" y="2" width="20" height="8" rx="2" ry="2" />
            <rect x="2" y="14" width="20" height="8" rx="2" ry="2" />
            <line x1="6" y1="6" x2="6.01" y2="6" />
            <line x1="6" y1="18" x2="6.01" y2="18" />
          </svg>
          <span className="panel-title">Internal Target</span>
        </div>

        <div className="grid grid-cols-4 gap-4">
          <div>
            <label className="label">Protocol</label>
            <select
              className="select"
              value={formData.internalProtocol}
              onChange={(e) =>
                updateField('internalProtocol', e.target.value as Protocol)
              }
              disabled={isLoading}
            >
              <option value="Http">HTTP</option>
              <option value="Https">HTTPS</option>
            </select>
          </div>

          <div className="col-span-2">
            <label className="label">Host</label>
            <input
              type="text"
              className={clsx(
                'input',
                touched.internalHost && errors.internalHost && 'input-error'
              )}
              placeholder="localhost"
              value={formData.internalHost}
              onChange={(e) => updateField('internalHost', e.target.value)}
              onBlur={() => handleBlur('internalHost')}
              disabled={isLoading}
            />
            {touched.internalHost && errors.internalHost && (
              <p className="text-rose-base text-xs mt-2 font-mono">
                {errors.internalHost}
              </p>
            )}
          </div>

          <div>
            <label className="label">Port</label>
            <input
              type="number"
              className={clsx(
                'input',
                touched.internalPort && errors.internalPort && 'input-error'
              )}
              placeholder="80"
              value={formData.internalPort}
              onChange={(e) =>
                updateField('internalPort', parseInt(e.target.value) || 80)
              }
              onBlur={() => handleBlur('internalPort')}
              disabled={isLoading}
              min={1}
              max={65535}
            />
            {touched.internalPort && errors.internalPort && (
              <p className="text-rose-base text-xs mt-2 font-mono">
                {errors.internalPort}
              </p>
            )}
          </div>
        </div>

        {/* SSL Options (shown when HTTPS selected) */}
        <AnimatePresence>
          {formData.internalProtocol === 'Https' && (
            <motion.div
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              className="mt-4 pt-4 border-t border-border-subtle"
            >
              <label className="flex items-center gap-3 cursor-pointer">
                <input
                  type="checkbox"
                  className="sr-only"
                  checked={formData.allowInvalidCertificates}
                  onChange={(e) =>
                    updateField('allowInvalidCertificates', e.target.checked)
                  }
                  disabled={isLoading}
                />
                <div
                  className="toggle"
                  data-checked={formData.allowInvalidCertificates}
                />
                <div>
                  <span className="text-sm text-text-primary font-medium">
                    Allow Invalid Certificates
                  </span>
                  <p className="text-xs text-text-tertiary mt-0.5">
                    Accept self-signed or expired certificates
                  </p>
                </div>
              </label>

              {formData.allowInvalidCertificates && (
                <motion.div
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  className="mt-3 p-3 bg-amber-glow border border-amber-dim rounded-md"
                >
                  <div className="flex items-start gap-2">
                    <svg
                      className="w-4 h-4 text-amber-base mt-0.5 shrink-0"
                      viewBox="0 0 20 20"
                      fill="currentColor"
                    >
                      <path
                        fillRule="evenodd"
                        d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z"
                        clipRule="evenodd"
                      />
                    </svg>
                    <p className="text-xs text-amber-base">
                      This reduces security. Only enable for development or
                      trusted internal services.
                    </p>
                  </div>
                </motion.div>
              )}
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* Enable/Disable */}
      <label className="flex items-center gap-3 cursor-pointer p-4 bg-surface-2 rounded-lg border border-border-subtle">
        <input
          type="checkbox"
          className="sr-only"
          checked={formData.enabled}
          onChange={(e) => updateField('enabled', e.target.checked)}
          disabled={isLoading}
        />
        <div className="toggle" data-checked={formData.enabled} />
        <div>
          <span className="text-sm text-text-primary font-medium">
            Enable Mapping
          </span>
          <p className="text-xs text-text-tertiary mt-0.5">
            Mapping will be active immediately after saving
          </p>
        </div>
      </label>

      {/* Form Actions */}
      <div className="flex justify-end gap-3 pt-4 border-t border-border-subtle">
        <button
          type="button"
          onClick={onCancel}
          className="btn btn-secondary"
          disabled={isLoading}
        >
          Cancel
        </button>
        <button type="submit" className="btn btn-primary" disabled={isLoading}>
          {isLoading ? (
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
              Saving...
            </>
          ) : (
            <>
              <svg
                className="w-4 h-4"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
                  clipRule="evenodd"
                />
              </svg>
              {isEditing ? 'Update Mapping' : 'Create Mapping'}
            </>
          )}
        </button>
      </div>
    </form>
  )
}
