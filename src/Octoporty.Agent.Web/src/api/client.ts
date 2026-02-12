// ═══════════════════════════════════════════════════════════════════════════
// OCTOPORTY API CLIENT
// ═══════════════════════════════════════════════════════════════════════════

import type {
  PortMapping,
  CreateMappingRequest,
  UpdateMappingRequest,
  AgentStatus,
  LoginRequest,
  LoginResponse,
  ApiError,
  TriggerUpdateResponse,
  GetLogsResponse,
  GetAgentLogsResponse,
  LandingPageResponse,
  UpdateLandingPageResponse,
  DiagnoseResponse,
  AgentDefinitionsExportV1,
  AgentDefinitionsImportV1,
  AgentDefinitionsImportResponse,
} from '../types'

const API_BASE = '/api/v1'

class ApiClient {
  private token: string | null = null

  constructor() {
    // Restore token from localStorage on init
    this.token = localStorage.getItem('octoporty_token')
  }

  private async request<T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> {
    const headers: Record<string, string> = {
      ...(options.headers as Record<string, string>),
    }

    // Only set Content-Type when we actually have a JSON body.
    // Setting it on GET/DELETE with an empty body can cause some servers to attempt JSON parsing and fail with 400.
    if (options.body !== undefined && !(options.body instanceof FormData)) {
      headers['Content-Type'] = 'application/json'
    }

    if (this.token) {
      headers['Authorization'] = `Bearer ${this.token}`
    }

    const response = await fetch(`${API_BASE}${endpoint}`, {
      ...options,
      headers,
    })

    if (response.status === 401) {
      this.clearToken()
      window.location.href = '/login'
      throw new Error('Unauthorized')
    }

    if (!response.ok) {
      const error: ApiError = await response.json().catch(() => ({
        message: 'An unexpected error occurred',
      }))
      throw error
    }

    // Handle 204 No Content
    if (response.status === 204) {
      return undefined as T
    }

    return response.json()
  }

  private async requestRaw(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<Response> {
    const headers: Record<string, string> = {
      ...(options.headers as Record<string, string>),
    }

    if (options.body !== undefined && !(options.body instanceof FormData)) {
      headers['Content-Type'] = 'application/json'
    }

    if (this.token) {
      headers['Authorization'] = `Bearer ${this.token}`
    }

    const response = await fetch(`${API_BASE}${endpoint}`, {
      ...options,
      headers,
    })

    if (response.status === 401) {
      this.clearToken()
      window.location.href = '/login'
      throw new Error('Unauthorized')
    }

    if (!response.ok) {
      const error: ApiError = await response.json().catch(() => ({
        message: 'An unexpected error occurred',
      }))
      throw error
    }

    return response
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Authentication
  // ─────────────────────────────────────────────────────────────────────────

  async login(credentials: LoginRequest): Promise<LoginResponse> {
    const response = await this.request<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(credentials),
    })
    this.setToken(response.token)
    return response
  }

  logout(): void {
    this.clearToken()
    window.location.href = '/login'
  }

  setToken(token: string): void {
    this.token = token
    localStorage.setItem('octoporty_token', token)
  }

  clearToken(): void {
    this.token = null
    localStorage.removeItem('octoporty_token')
  }

  isAuthenticated(): boolean {
    return this.token !== null
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Status
  // ─────────────────────────────────────────────────────────────────────────

  async getStatus(): Promise<AgentStatus> {
    return this.request<AgentStatus>('/status')
  }

  async reconnect(): Promise<void> {
    return this.request('/reconnect', { method: 'POST' })
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Gateway
  // ─────────────────────────────────────────────────────────────────────────

  async triggerGatewayUpdate(force: boolean = false): Promise<TriggerUpdateResponse> {
    return this.request<TriggerUpdateResponse>('/gateway/update', {
      method: 'POST',
      body: JSON.stringify({ force }),
    })
  }

  async getGatewayLogs(beforeId: number = 0, count: number = 1000): Promise<GetLogsResponse> {
    const params = new URLSearchParams()
    if (beforeId > 0) params.set('beforeId', beforeId.toString())
    if (count !== 1000) params.set('count', count.toString())
    const queryString = params.toString()
    return this.request<GetLogsResponse>(`/gateway/logs${queryString ? `?${queryString}` : ''}`)
  }

  async getAgentLogs(beforeId: number = 0, count: number = 1000): Promise<GetAgentLogsResponse> {
    const params = new URLSearchParams()
    if (beforeId > 0) params.set('beforeId', beforeId.toString())
    if (count !== 1000) params.set('count', count.toString())
    const queryString = params.toString()
    return this.request<GetAgentLogsResponse>(`/agent/logs${queryString ? `?${queryString}` : ''}`)
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Mappings
  // ─────────────────────────────────────────────────────────────────────────

  async getMappings(): Promise<PortMapping[]> {
    return this.request<PortMapping[]>('/mappings')
  }

  async getMapping(id: string): Promise<PortMapping> {
    return this.request<PortMapping>(`/mappings/${id}`)
  }

  async createMapping(mapping: CreateMappingRequest): Promise<PortMapping> {
    // Transform UI model to Agent API contract.
    // UI uses: internalProtocol, allowInvalidCertificates, enabled, name
    // API expects: internalUseTls, allowSelfSignedCerts, isEnabled, description
    const body = {
      externalDomain: mapping.externalDomain,
      internalHost: mapping.internalHost,
      internalPort: mapping.internalPort,
      internalUseTls: mapping.internalProtocol === 'Https',
      allowSelfSignedCerts: mapping.allowInvalidCertificates ?? false,
      isEnabled: mapping.enabled ?? true,
      description: mapping.name,
    }
    return this.request<PortMapping>('/mappings', {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  async updateMapping(mapping: UpdateMappingRequest): Promise<PortMapping> {
    const body = {
      id: mapping.id,
      externalDomain: mapping.externalDomain,
      internalHost: mapping.internalHost,
      internalPort: mapping.internalPort,
      internalUseTls: mapping.internalProtocol === 'Https',
      allowSelfSignedCerts: mapping.allowInvalidCertificates ?? false,
      isEnabled: mapping.enabled ?? true,
      description: mapping.name,
    }
    return this.request<PortMapping>(`/mappings/${mapping.id}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    })
  }

  async deleteMapping(id: string): Promise<void> {
    return this.request(`/mappings/${id}`, { method: 'DELETE' })
  }

  async toggleMapping(id: string, enabled: boolean): Promise<PortMapping> {
    return this.request<PortMapping>(`/mappings/${id}/toggle`, {
      method: 'PATCH',
      body: JSON.stringify({ enabled }),
    })
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Settings - Landing Page
  // ─────────────────────────────────────────────────────────────────────────

  async getLandingPage(): Promise<LandingPageResponse> {
    return this.request<LandingPageResponse>('/settings/landing-page')
  }

  async updateLandingPage(html: string): Promise<UpdateLandingPageResponse> {
    return this.request<UpdateLandingPageResponse>('/settings/landing-page', {
      method: 'PUT',
      body: JSON.stringify({ html }),
    })
  }

  async resetLandingPage(): Promise<LandingPageResponse> {
    return this.request<LandingPageResponse>('/settings/landing-page', {
      method: 'DELETE',
    })
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Gateway - Caddy Config
  // ─────────────────────────────────────────────────────────────────────────

  async getCaddyConfig(): Promise<CaddyConfigResponse> {
    return this.request<CaddyConfigResponse>('/gateway/caddy-config')
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Diagnostics
  // ─────────────────────────────────────────────────────────────────────────

  async diagnoseUrl(url: string, maxBodyBytes: number = 131072): Promise<DiagnoseResponse> {
    return this.request<DiagnoseResponse>('/test/diagnose', {
      method: 'POST',
      body: JSON.stringify({ url, maxBodyBytes }),
    })
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Import / Export
  // ─────────────────────────────────────────────────────────────────────────

  async exportDefinitions(): Promise<AgentDefinitionsExportV1> {
    return this.request<AgentDefinitionsExportV1>('/import-export/export')
  }

  async importDefinitions(payload: AgentDefinitionsImportV1): Promise<AgentDefinitionsImportResponse> {
    return this.request<AgentDefinitionsImportResponse>('/import-export/import', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  }

  async downloadSqliteBackup(): Promise<{ blob: Blob; filename: string }> {
    const response = await this.requestRaw('/import-export/sqlite', { method: 'GET' })
    const blob = await response.blob()

    const contentDisposition = response.headers.get('content-disposition') ?? ''
    const filenameMatch = contentDisposition.match(/filename=\"?([^\";]+)\"?/i)
    const filename = filenameMatch?.[1] ?? 'octoporty-agent-backup.db'

    return { blob, filename }
  }
}

export interface CaddyConfigResponse {
  success: boolean
  error?: string
  configJson?: string
}

export const api = new ApiClient()
