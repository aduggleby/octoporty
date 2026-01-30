// ═══════════════════════════════════════════════════════════════════════════
// SETTINGS PAGE
// Landing page editor with live preview
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useEffect, useCallback, useRef } from 'react'
import { motion } from 'motion/react'
import { useToast } from '../hooks/useToast'
import { api } from '../api/client'

export function SettingsPage() {
  const { addToast } = useToast()
  const [html, setHtml] = useState('')
  const [originalHtml, setOriginalHtml] = useState('')
  const [hash, setHash] = useState('')
  const [isDefault, setIsDefault] = useState(true)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [isResetting, setIsResetting] = useState(false)
  const [showResetConfirm, setShowResetConfirm] = useState(false)
  const iframeRef = useRef<HTMLIFrameElement>(null)

  const hasChanges = html !== originalHtml

  // Fetch landing page on mount
  useEffect(() => {
    api
      .getLandingPage()
      .then((data) => {
        setHtml(data.html)
        setOriginalHtml(data.html)
        setHash(data.hash)
        setIsDefault(data.isDefault)
      })
      .catch((err) => {
        addToast('error', 'Failed to load landing page', err.message)
      })
      .finally(() => {
        setIsLoading(false)
      })
  }, [addToast])

  // Update iframe preview when HTML changes
  useEffect(() => {
    if (iframeRef.current) {
      const doc = iframeRef.current.contentDocument
      if (doc) {
        doc.open()
        doc.write(html)
        doc.close()
      }
    }
  }, [html])

  const handleSave = useCallback(async () => {
    setIsSaving(true)
    try {
      const response = await api.updateLandingPage(html)
      setOriginalHtml(html)
      setHash(response.hash)
      setIsDefault(false)
      addToast('success', 'Landing page saved', 'Changes synced to Gateway')
    } catch (err) {
      const error = err as Error
      addToast('error', 'Failed to save landing page', error.message)
    } finally {
      setIsSaving(false)
    }
  }, [html, addToast])

  const handleReset = useCallback(async () => {
    setIsResetting(true)
    try {
      const response = await api.resetLandingPage()
      setHtml(response.html)
      setOriginalHtml(response.html)
      setHash(response.hash)
      setIsDefault(true)
      setShowResetConfirm(false)
      addToast('success', 'Landing page reset', 'Restored to default Octoporty branding')
    } catch (err) {
      const error = err as Error
      addToast('error', 'Failed to reset landing page', error.message)
    } finally {
      setIsResetting(false)
    }
  }, [addToast])

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
          <h1 className="page-title">Settings</h1>
          <p className="page-subtitle">Configure your Gateway landing page</p>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => setShowResetConfirm(true)}
            disabled={isDefault || isSaving}
            className="btn btn-ghost"
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
            Reset to Default
          </button>
          <button
            onClick={handleSave}
            disabled={!hasChanges || isSaving}
            className="btn btn-primary"
          >
            {isSaving ? (
              <motion.div
                className="w-4 h-4 border-2 border-white border-t-transparent rounded-full"
                animate={{ rotate: 360 }}
                transition={{ duration: 1, repeat: Infinity, ease: 'linear' }}
              />
            ) : (
              <svg
                className="w-4 h-4"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path d="M7.707 10.293a1 1 0 10-1.414 1.414l3 3a1 1 0 001.414 0l3-3a1 1 0 00-1.414-1.414L11 11.586V6h5a2 2 0 012 2v7a2 2 0 01-2 2H4a2 2 0 01-2-2V8a2 2 0 012-2h5v5.586l-1.293-1.293zM9 4a1 1 0 012 0v2H9V4z" />
              </svg>
            )}
            Save Changes
          </button>
        </div>
      </div>

      {/* Status Bar */}
      <div className="mb-6 p-3 bg-surface-1 rounded-lg border border-border-subtle flex items-center justify-between text-xs font-mono">
        <div className="flex items-center gap-4">
          <span className="text-text-muted">
            Status:{' '}
            <span className={isDefault ? 'text-amber-400' : 'text-emerald-400'}>
              {isDefault ? 'Default' : 'Custom'}
            </span>
          </span>
          <span className="text-text-muted">
            Hash: <span className="text-text-secondary">{hash.slice(0, 8)}</span>
          </span>
        </div>
        {hasChanges && (
          <span className="text-amber-400 flex items-center gap-1">
            <svg className="w-3 h-3" viewBox="0 0 20 20" fill="currentColor">
              <path
                fillRule="evenodd"
                d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z"
                clipRule="evenodd"
              />
            </svg>
            Unsaved changes
          </span>
        )}
      </div>

      {/* Editor and Preview */}
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
        {/* HTML Editor */}
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
              <polyline points="16 18 22 12 16 6" />
              <polyline points="8 6 2 12 8 18" />
            </svg>
            <span className="panel-title">HTML Editor</span>
          </div>
          <div className="panel-body p-0">
            <textarea
              value={html}
              onChange={(e) => setHtml(e.target.value)}
              className="w-full h-[500px] p-4 bg-surface-0 text-text-primary font-mono text-sm resize-none focus:outline-none focus:ring-0 border-0"
              placeholder="Enter your custom landing page HTML..."
              spellCheck={false}
            />
          </div>
        </motion.div>

        {/* Live Preview */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
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
              <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
              <line x1="8" y1="21" x2="16" y2="21" />
              <line x1="12" y1="17" x2="12" y2="21" />
            </svg>
            <span className="panel-title">Live Preview</span>
          </div>
          <div className="panel-body p-0 overflow-hidden">
            <iframe
              ref={iframeRef}
              className="w-full h-[500px] bg-white"
              sandbox="allow-same-origin"
              title="Landing Page Preview"
            />
          </div>
        </motion.div>
      </div>

      {/* Reset Confirmation Modal */}
      {showResetConfirm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center">
          <div
            className="absolute inset-0 bg-surface-0/80 backdrop-blur-sm"
            onClick={() => setShowResetConfirm(false)}
          />
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="relative bg-surface-2 rounded-lg border border-border-emphasis p-6 max-w-md w-full mx-4 shadow-xl"
          >
            <h3 className="text-lg font-semibold text-text-primary mb-2">
              Reset to Default?
            </h3>
            <p className="text-text-secondary mb-6">
              This will replace your custom landing page with the default Octoporty
              branding. This action cannot be undone.
            </p>
            <div className="flex justify-end gap-3">
              <button
                onClick={() => setShowResetConfirm(false)}
                disabled={isResetting}
                className="btn btn-ghost"
              >
                Cancel
              </button>
              <button
                onClick={handleReset}
                disabled={isResetting}
                className="btn btn-danger"
              >
                {isResetting ? (
                  <motion.div
                    className="w-4 h-4 border-2 border-white border-t-transparent rounded-full"
                    animate={{ rotate: 360 }}
                    transition={{ duration: 1, repeat: Infinity, ease: 'linear' }}
                  />
                ) : (
                  'Reset'
                )}
              </button>
            </div>
          </motion.div>
        </div>
      )}
    </div>
  )
}
