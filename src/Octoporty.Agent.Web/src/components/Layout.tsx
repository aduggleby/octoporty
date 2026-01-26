// ═══════════════════════════════════════════════════════════════════════════
// LAYOUT COMPONENT
// Main application shell with sidebar navigation
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useEffect, useCallback, type ReactNode } from 'react'
import { NavLink, useNavigate } from 'react-router-dom'
import { motion, AnimatePresence } from 'motion/react'
import clsx from 'clsx'
import { ConnectionStatusBadge } from './ConnectionStatus'
import { GatewayUpdateBanner } from './GatewayUpdateBanner'
import { useSignalR } from '../hooks/useSignalR'
import { api } from '../api/client'
import type { AgentStatus, StatusUpdate } from '../types'

interface LayoutProps {
  children: ReactNode
}

export function Layout({ children }: LayoutProps) {
  const navigate = useNavigate()
  const [status, setStatus] = useState<AgentStatus | null>(null)
  const [sidebarOpen, setSidebarOpen] = useState(false)

  // Fetch initial status
  useEffect(() => {
    api.getStatus().then(setStatus).catch(console.error)
  }, [])

  // Handle SignalR status updates
  const handleStatusUpdate = useCallback((update: StatusUpdate) => {
    setStatus((prev) =>
      prev ? { ...prev, connectionStatus: update.connectionStatus } : null
    )
  }, [])

  useSignalR({
    onStatusUpdate: handleStatusUpdate,
  })

  const handleLogout = () => {
    api.logout()
    navigate('/login')
  }

  const navItems = [
    {
      to: '/',
      label: 'Dashboard',
      icon: (
        <svg className="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <rect x="3" y="3" width="7" height="9" rx="1" />
          <rect x="14" y="3" width="7" height="5" rx="1" />
          <rect x="14" y="12" width="7" height="9" rx="1" />
          <rect x="3" y="16" width="7" height="5" rx="1" />
        </svg>
      ),
    },
    {
      to: '/mappings',
      label: 'Mappings',
      icon: (
        <svg className="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
          <polyline points="15 3 21 3 21 9" />
          <line x1="10" y1="14" x2="21" y2="3" />
        </svg>
      ),
    },
  ]

  return (
    <div className="min-h-screen">
      {/* Sidebar */}
      <aside
        className="sidebar"
        data-open={sidebarOpen}
      >
        {/* Logo */}
        <div className="sidebar-header">
          <div className="flex items-center gap-3">
            <motion.div
              className="w-10 h-10 rounded-lg bg-gradient-to-br from-cyan-base to-cyan-dim flex items-center justify-center"
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
            >
              <svg
                className="w-6 h-6 text-surface-0"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
              >
                <circle cx="12" cy="10" r="5" />
                <path d="M7 13 L3 20" />
                <path d="M9 14 L6 22" />
                <path d="M12 15 L12 23" />
                <path d="M15 14 L18 22" />
                <path d="M17 13 L21 20" />
              </svg>
            </motion.div>
            <div>
              <h1 className="font-display text-lg font-bold text-text-primary tracking-tight">
                Octoporty
              </h1>
              <p className="text-[10px] font-mono text-text-muted tracking-wider">
                AGENT v{status?.version ?? '0.0.0'}
              </p>
            </div>
          </div>
        </div>

        {/* Connection Status */}
        <div className="px-4 py-3 border-b border-border-subtle">
          {status && (
            <ConnectionStatusBadge status={status.connectionStatus} />
          )}
        </div>

        {/* Navigation */}
        <nav className="sidebar-nav">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                clsx('sidebar-link', isActive && 'sidebar-link-active')
              }
              onClick={() => setSidebarOpen(false)}
            >
              {item.icon}
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>

        {/* Footer */}
        <div className="sidebar-footer">
          <button
            onClick={handleLogout}
            className="sidebar-link w-full text-rose-base hover:bg-rose-glow"
          >
            <svg className="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
              <polyline points="16 17 21 12 16 7" />
              <line x1="21" y1="12" x2="9" y2="12" />
            </svg>
            <span>Logout</span>
          </button>
        </div>
      </aside>

      {/* Mobile Menu Button */}
      <button
        className="fixed top-4 left-4 z-50 md:hidden btn btn-secondary p-2"
        onClick={() => setSidebarOpen(!sidebarOpen)}
      >
        <svg className="w-6 h-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          {sidebarOpen ? (
            <path d="M6 18L18 6M6 6l12 12" />
          ) : (
            <path d="M4 6h16M4 12h16M4 18h16" />
          )}
        </svg>
      </button>

      {/* Mobile Overlay */}
      <AnimatePresence>
        {sidebarOpen && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-surface-0/80 backdrop-blur-sm z-40 md:hidden"
            onClick={() => setSidebarOpen(false)}
          />
        )}
      </AnimatePresence>

      {/* Main Content */}
      <main className="main-content">
        {/* Gateway Update Banner */}
        {status && (
          <GatewayUpdateBanner
            agentVersion={status.version}
            gatewayVersion={status.gatewayVersion}
            visible={status.gatewayUpdateAvailable}
            onUpdateTriggered={() => {
              // Refresh status after update is triggered
              api.getStatus().then(setStatus).catch(console.error)
            }}
          />
        )}

        <AnimatePresence mode="wait">
          <motion.div
            key={location.pathname}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
            transition={{ duration: 0.2 }}
          >
            {children}
          </motion.div>
        </AnimatePresence>
      </main>
    </div>
  )
}
