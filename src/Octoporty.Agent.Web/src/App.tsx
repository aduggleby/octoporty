// ═══════════════════════════════════════════════════════════════════════════
// OCTOPORTY AGENT WEB UI - MAIN APPLICATION
// ═══════════════════════════════════════════════════════════════════════════

import {
  BrowserRouter,
  Routes,
  Route,
  Navigate,
  useLocation,
} from 'react-router-dom'
import { Layout } from './components/Layout'
import { ToastProvider } from './hooks/useToast'
import { ErrorBoundary, ErrorProvider } from './components/ErrorBoundary'
import { LoginPage } from './pages/Login'
import { DashboardPage } from './pages/Dashboard'
import { MappingsPage } from './pages/Mappings'
import { MappingDetailPage } from './pages/MappingDetail'
import { GatewayPage } from './pages/Gateway'
import { SettingsPage } from './pages/Settings'
import { api } from './api/client'
import type { ReactNode } from 'react'

// Protected route wrapper
function ProtectedRoute({ children }: { children: ReactNode }) {
  const location = useLocation()

  if (!api.isAuthenticated()) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  return <>{children}</>
}

// Public route wrapper (redirect to dashboard if already authenticated)
function PublicRoute({ children }: { children: ReactNode }) {
  if (api.isAuthenticated()) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}

export function App() {
  return (
    <ErrorBoundary>
      <ErrorProvider>
        <ToastProvider>
          <BrowserRouter>
        <Routes>
          {/* Public routes */}
          <Route
            path="/login"
            element={
              <PublicRoute>
                <LoginPage />
              </PublicRoute>
            }
          />

          {/* Protected routes */}
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <Layout>
                  <DashboardPage />
                </Layout>
              </ProtectedRoute>
            }
          />

          <Route
            path="/mappings"
            element={
              <ProtectedRoute>
                <Layout>
                  <MappingsPage />
                </Layout>
              </ProtectedRoute>
            }
          />

          <Route
            path="/mappings/:id"
            element={
              <ProtectedRoute>
                <Layout>
                  <MappingDetailPage />
                </Layout>
              </ProtectedRoute>
            }
          />

          <Route
            path="/gateway"
            element={
              <ProtectedRoute>
                <Layout>
                  <GatewayPage />
                </Layout>
              </ProtectedRoute>
            }
          />

          <Route
            path="/settings"
            element={
              <ProtectedRoute>
                <Layout>
                  <SettingsPage />
                </Layout>
              </ProtectedRoute>
            }
          />

          {/* Fallback */}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
          </BrowserRouter>
        </ToastProvider>
      </ErrorProvider>
    </ErrorBoundary>
  )
}

export default App
