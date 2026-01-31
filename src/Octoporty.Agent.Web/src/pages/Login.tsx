// ═══════════════════════════════════════════════════════════════════════════
// LOGIN PAGE
// Industrial control panel authentication interface
// ═══════════════════════════════════════════════════════════════════════════

import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'motion/react'
import { api } from '../api/client'
import type { ApiError } from '../types'

export function LoginPage() {
  const navigate = useNavigate()
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setIsLoading(true)

    try {
      await api.login({ password })
      navigate('/')
    } catch (err) {
      const apiError = err as ApiError
      setError(apiError.message || 'Invalid password')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      {/* Background decoration */}
      <div className="fixed inset-0 overflow-hidden pointer-events-none">
        {/* Grid lines emanating from center */}
        <div className="absolute inset-0 flex items-center justify-center">
          <motion.div
            className="w-[800px] h-[800px] border border-border-subtle rounded-full"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 0.3, scale: 1 }}
            transition={{ duration: 1.5 }}
          />
          <motion.div
            className="absolute w-[600px] h-[600px] border border-border-subtle rounded-full"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 0.4, scale: 1 }}
            transition={{ duration: 1.5, delay: 0.1 }}
          />
          <motion.div
            className="absolute w-[400px] h-[400px] border border-border-subtle rounded-full"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 0.5, scale: 1 }}
            transition={{ duration: 1.5, delay: 0.2 }}
          />
        </div>

        {/* Floating particles */}
        {[...Array(6)].map((_, i) => (
          <motion.div
            key={i}
            className="absolute w-1 h-1 bg-cyan-base rounded-full"
            style={{
              left: `${20 + i * 15}%`,
              top: `${30 + (i % 3) * 20}%`,
            }}
            animate={{
              y: [-20, 20, -20],
              opacity: [0.3, 0.8, 0.3],
            }}
            transition={{
              duration: 3 + i * 0.5,
              repeat: Infinity,
              ease: 'easeInOut',
            }}
          />
        ))}
      </div>

      {/* Login Card */}
      <motion.div
        initial={{ opacity: 0, y: 20, scale: 0.95 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.5 }}
        className="panel w-full max-w-md relative z-10"
      >
        {/* Header */}
        <div className="p-8 text-center border-b border-border-subtle">
          {/* Logo */}
          <motion.div
            className="w-20 h-20 mx-auto mb-6 rounded-2xl overflow-hidden"
            initial={{ rotate: -10 }}
            animate={{ rotate: 0 }}
            transition={{ type: 'spring', stiffness: 200 }}
          >
            <img
              src="/octoporty_logo.png"
              alt="Octoporty"
              className="w-full h-full object-cover"
            />
          </motion.div>

          <h1 className="font-display text-2xl font-bold text-text-primary tracking-tight">
            Octoporty
          </h1>
          <p className="text-text-tertiary text-sm mt-2 font-mono">
            AGENT CONTROL PANEL
          </p>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="p-8 space-y-6">
          {/* Error Message */}
          {error && (
            <motion.div
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              className="p-4 bg-rose-glow border border-rose-dim rounded-lg"
            >
              <div className="flex items-center gap-3">
                <svg
                  className="w-5 h-5 text-rose-base shrink-0"
                  viewBox="0 0 20 20"
                  fill="currentColor"
                >
                  <path
                    fillRule="evenodd"
                    d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                    clipRule="evenodd"
                  />
                </svg>
                <p className="text-rose-base text-sm font-medium">{error}</p>
              </div>
            </motion.div>
          )}

          {/* Password */}
          <div>
            <label className="label">Password</label>
            <div className="relative">
              {/* Icon container: fixed width of 48px (w-12) to match input's pl-12 padding */}
              {/* Uses flex centering to perfectly center the 20px icon within this space */}
              <div className="absolute inset-y-0 left-0 w-12 flex items-center justify-center pointer-events-none">
                <svg
                  className="w-5 h-5 text-text-muted"
                  viewBox="0 0 20 20"
                  fill="currentColor"
                >
                  <path
                    fillRule="evenodd"
                    d="M5 9V7a5 5 0 0110 0v2a2 2 0 012 2v5a2 2 0 01-2 2H5a2 2 0 01-2-2v-5a2 2 0 012-2zm8-2v2H7V7a3 3 0 016 0z"
                    clipRule="evenodd"
                  />
                </svg>
              </div>
              <input
                type="password"
                className="input pl-12!"
                placeholder="Enter password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                autoFocus
                disabled={isLoading}
              />
            </div>
          </div>

          {/* Submit Button */}
          <button
            type="submit"
            className="btn btn-primary w-full btn-lg"
            disabled={isLoading || !password}
          >
            {isLoading ? (
              <>
                <motion.svg
                  className="w-5 h-5"
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
                Authenticating...
              </>
            ) : (
              'Access Control Panel'
            )}
          </button>
        </form>

        {/* Footer */}
        <div className="px-8 pb-6 text-center">
          <p className="text-[10px] font-mono text-text-muted tracking-wider">
            SECURE TUNNEL MANAGEMENT INTERFACE
          </p>
        </div>
      </motion.div>
    </div>
  )
}
