// ═══════════════════════════════════════════════════════════════════════════
// OCTOPORTY AGENT WEB UI - TYPE DEFINITIONS
// ═══════════════════════════════════════════════════════════════════════════

export type Protocol = 'Http' | 'Https'

export interface PortMapping {
  id: string
  name: string
  externalDomain: string
  externalPort: number
  internalHost: string
  internalPort: number
  internalProtocol: Protocol
  allowInvalidCertificates: boolean
  enabled: boolean
  createdAt: string
  updatedAt: string | null
}

export interface CreateMappingRequest {
  name: string
  externalDomain: string
  externalPort?: number
  internalHost: string
  internalPort: number
  internalProtocol: Protocol
  allowInvalidCertificates?: boolean
  enabled?: boolean
}

export interface UpdateMappingRequest extends CreateMappingRequest {
  id: string
}

export type ConnectionStatus = 'Connected' | 'Disconnected' | 'Connecting' | 'Reconnecting'

export interface AgentStatus {
  connectionStatus: ConnectionStatus
  gatewayUrl: string
  lastConnected: string | null
  lastDisconnected: string | null
  reconnectAttempts: number
  uptime: number | null
  version: string
  activeMappings: number
  gatewayVersion?: string | null
  gatewayUpdateAvailable: boolean
}

export interface ApiError {
  message: string
  errors?: Record<string, string[]>
}

export interface LoginRequest {
  username: string
  password: string
}

export interface LoginResponse {
  token: string
  expiresAt: string
}

export interface User {
  username: string
  isAuthenticated: boolean
}

// SignalR Hub messages
export interface StatusUpdate {
  connectionStatus: ConnectionStatus
  timestamp: string
  message?: string
}

export interface MappingStatusUpdate {
  mappingId: string
  status: 'Active' | 'Inactive' | 'Error'
  lastRequestAt?: string
  errorMessage?: string
}

// Toast/Notification types
export type ToastType = 'success' | 'error' | 'warning' | 'info'

export interface Toast {
  id: string
  type: ToastType
  title: string
  message?: string
  duration?: number
}

// Gateway Update types
export type UpdateStatus = 'Queued' | 'Rejected' | 'AlreadyQueued'

export interface TriggerUpdateResponse {
  success: boolean
  message: string
  error?: string
  agentVersion: string
  gatewayVersion?: string
  status?: UpdateStatus
}
