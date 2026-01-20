// ═══════════════════════════════════════════════════════════════════════════
// MODAL COMPONENT
// Reusable modal dialog with animations
// ═══════════════════════════════════════════════════════════════════════════

import { useEffect, type ReactNode } from 'react'
import { motion, AnimatePresence } from 'motion/react'

interface ModalProps {
  isOpen: boolean
  onClose: () => void
  title: string
  children: ReactNode
  size?: 'sm' | 'md' | 'lg'
}

const sizeClasses = {
  sm: 'max-w-sm',
  md: 'max-w-md',
  lg: 'max-w-2xl',
}

export function Modal({
  isOpen,
  onClose,
  title,
  children,
  size = 'md',
}: ModalProps) {
  // Handle escape key
  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }

    if (isOpen) {
      document.addEventListener('keydown', handleEscape)
      document.body.style.overflow = 'hidden'
    }

    return () => {
      document.removeEventListener('keydown', handleEscape)
      document.body.style.overflow = ''
    }
  }, [isOpen, onClose])

  return (
    <AnimatePresence>
      {isOpen && (
        <div className="modal-backdrop" onClick={onClose}>
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            transition={{ type: 'spring', stiffness: 500, damping: 30 }}
            className={`modal ${sizeClasses[size]}`}
            onClick={(e) => e.stopPropagation()}
          >
            {/* Header */}
            <div className="modal-header">
              <h2 className="modal-title">{title}</h2>
              <button
                onClick={onClose}
                className="p-2 rounded-md hover:bg-surface-3 text-text-muted hover:text-text-primary transition-colors"
              >
                <svg
                  className="w-5 h-5"
                  viewBox="0 0 20 20"
                  fill="currentColor"
                >
                  <path
                    fillRule="evenodd"
                    d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z"
                    clipRule="evenodd"
                  />
                </svg>
              </button>
            </div>

            {/* Body */}
            <div className="modal-body">{children}</div>
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  )
}

// Confirmation Dialog variant
interface ConfirmDialogProps {
  isOpen: boolean
  onClose: () => void
  onConfirm: () => void
  title: string
  message: string
  confirmLabel?: string
  cancelLabel?: string
  variant?: 'danger' | 'warning' | 'info'
  isLoading?: boolean
}

export function ConfirmDialog({
  isOpen,
  onClose,
  onConfirm,
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  variant = 'danger',
  isLoading = false,
}: ConfirmDialogProps) {
  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title} size="sm">
      <div className="text-center">
        {/* Icon */}
        <div
          className={`w-14 h-14 mx-auto mb-4 rounded-full flex items-center justify-center ${
            variant === 'danger'
              ? 'bg-rose-glow'
              : variant === 'warning'
              ? 'bg-amber-glow'
              : 'bg-cyan-glow'
          }`}
        >
          <svg
            className={`w-7 h-7 ${
              variant === 'danger'
                ? 'text-rose-base'
                : variant === 'warning'
                ? 'text-amber-base'
                : 'text-cyan-base'
            }`}
            viewBox="0 0 20 20"
            fill="currentColor"
          >
            {variant === 'danger' ? (
              <path
                fillRule="evenodd"
                d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z"
                clipRule="evenodd"
              />
            ) : (
              <path
                fillRule="evenodd"
                d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z"
                clipRule="evenodd"
              />
            )}
          </svg>
        </div>

        <p className="text-text-secondary mb-6">{message}</p>

        <div className="flex justify-center gap-3">
          <button
            onClick={onClose}
            className="btn btn-secondary"
            disabled={isLoading}
          >
            {cancelLabel}
          </button>
          <button
            onClick={onConfirm}
            className={`btn ${
              variant === 'danger' ? 'btn-danger' : 'btn-primary'
            }`}
            disabled={isLoading}
          >
            {isLoading ? 'Please wait...' : confirmLabel}
          </button>
        </div>
      </div>
    </Modal>
  )
}
