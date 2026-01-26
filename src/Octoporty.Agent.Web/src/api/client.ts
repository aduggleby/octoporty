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
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string>),
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
    return this.request<PortMapping>('/mappings', {
      method: 'POST',
      body: JSON.stringify(mapping),
    })
  }

  async updateMapping(mapping: UpdateMappingRequest): Promise<PortMapping> {
    return this.request<PortMapping>(`/mappings/${mapping.id}`, {
      method: 'PUT',
      body: JSON.stringify(mapping),
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
}

export const api = new ApiClient()
