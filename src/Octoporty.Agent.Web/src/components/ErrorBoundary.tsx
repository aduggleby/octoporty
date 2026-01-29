// ═══════════════════════════════════════════════════════════════════════════
// ERROR BOUNDARY COMPONENT
// Catches React errors and displays them in a modal dialog
// Also provides a global error context for API errors
// ═══════════════════════════════════════════════════════════════════════════

import { Component, createContext, useContext, useState, useCallback, type ReactNode } from 'react'
import { Modal } from './Modal'

// ─────────────────────────────────────────────────────────────────────────────
// Error Context for global error handling
// ─────────────────────────────────────────────────────────────────────────────

interface ErrorContextType {
  showError: (title: string, message: string, details?: string) => void
  clearError: () => void
}

const ErrorContext = createContext<ErrorContextType | null>(null)

export function useError() {
  const context = useContext(ErrorContext)
  if (!context) {
    throw new Error('useError must be used within an ErrorProvider')
  }
  return context
}

interface ErrorState {
  isOpen: boolean
  title: string
  message: string
  details?: string
}

export function ErrorProvider({ children }: { children: ReactNode }) {
  const [error, setError] = useState<ErrorState>({
    isOpen: false,
    title: '',
    message: '',
  })

  const showError = useCallback((title: string, message: string, details?: string) => {
    setError({ isOpen: true, title, message, details })
  }, [])

  const clearError = useCallback(() => {
    setError((prev) => ({ ...prev, isOpen: false }))
  }, [])

  return (
    <ErrorContext.Provider value={{ showError, clearError }}>
      {children}
      <ErrorModal
        isOpen={error.isOpen}
        onClose={clearError}
        title={error.title}
        message={error.message}
        details={error.details}
      />
    </ErrorContext.Provider>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// Error Modal Component
// ─────────────────────────────────────────────────────────────────────────────

interface ErrorModalProps {
  isOpen: boolean
  onClose: () => void
  title: string
  message: string
  details?: string
}

function ErrorModal({ isOpen, onClose, title, message, details }: ErrorModalProps) {
  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title} size="md">
      <div className="text-center">
        {/* Error Icon */}
        <div className="w-14 h-14 mx-auto mb-4 rounded-full flex items-center justify-center bg-rose-glow">
          <svg
            className="w-7 h-7 text-rose-base"
            viewBox="0 0 20 20"
            fill="currentColor"
          >
            <path
              fillRule="evenodd"
              d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
              clipRule="evenodd"
            />
          </svg>
        </div>

        <p className="text-text-secondary mb-4">{message}</p>

        {details && (
          <div className="bg-surface-2 border border-border-subtle rounded-lg p-4 mb-6 text-left">
            <p className="text-[10px] font-mono text-text-muted uppercase mb-2">Details</p>
            <pre className="text-xs font-mono text-rose-base whitespace-pre-wrap break-all">
              {details}
            </pre>
          </div>
        )}

        <button onClick={onClose} className="btn btn-secondary">
          Close
        </button>
      </div>
    </Modal>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// React Error Boundary (Class Component)
// ─────────────────────────────────────────────────────────────────────────────

interface ErrorBoundaryProps {
  children: ReactNode
  fallback?: ReactNode
}

interface ErrorBoundaryState {
  hasError: boolean
  error: Error | null
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error }
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    console.error('ErrorBoundary caught an error:', error, errorInfo)
  }

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback
      }

      return (
        <div className="min-h-screen flex items-center justify-center p-4">
          <div className="panel w-full max-w-md p-8 text-center">
            {/* Error Icon */}
            <div className="w-16 h-16 mx-auto mb-6 rounded-full flex items-center justify-center bg-rose-glow">
              <svg
                className="w-8 h-8 text-rose-base"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                  clipRule="evenodd"
                />
              </svg>
            </div>

            <h1 className="font-display text-xl font-bold text-text-primary mb-2">
              Something went wrong
            </h1>
            <p className="text-text-secondary mb-6">
              An unexpected error occurred. Please try refreshing the page.
            </p>

            {this.state.error && (
              <div className="bg-surface-2 border border-border-subtle rounded-lg p-4 mb-6 text-left">
                <p className="text-[10px] font-mono text-text-muted uppercase mb-2">Error</p>
                <pre className="text-xs font-mono text-rose-base whitespace-pre-wrap break-all">
                  {this.state.error.message}
                </pre>
              </div>
            )}

            <button
              onClick={() => window.location.reload()}
              className="btn btn-primary"
            >
              Refresh Page
            </button>
          </div>
        </div>
      )
    }

    return this.props.children
  }
}
