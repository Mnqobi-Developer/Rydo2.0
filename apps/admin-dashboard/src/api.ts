import { appConfig } from './config/environment'
import type {
  AdminSession,
  DashboardData,
  DriverApplication,
  PagedResult,
} from './types'

const sessionKey = 'rydo.admin.session'

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

export function readSession(): AdminSession | null {
  const value = sessionStorage.getItem(sessionKey)
  if (!value) return null
  try {
    return JSON.parse(value) as AdminSession
  } catch {
    sessionStorage.removeItem(sessionKey)
    return null
  }
}

export function writeSession(session: AdminSession | null) {
  if (session) sessionStorage.setItem(sessionKey, JSON.stringify(session))
  else sessionStorage.removeItem(sessionKey)
}

async function readError(response: Response) {
  try {
    const problem = (await response.json()) as { detail?: string; title?: string }
    return problem.detail ?? problem.title ?? `Request failed (${response.status})`
  } catch {
    return `Request failed (${response.status})`
  }
}

export class AdminApi {
  private readonly getSession: () => AdminSession | null
  private readonly updateSession: (session: AdminSession | null) => void

  constructor(
    getSession: () => AdminSession | null,
    updateSession: (session: AdminSession | null) => void,
  ) {
    this.getSession = getSession
    this.updateSession = updateSession
  }

  async login(email: string, password: string) {
    const response = await fetch(`${appConfig.apiBaseUrl}/api/v1/admin/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    })
    if (!response.ok) throw new ApiError(await readError(response), response.status)
    const session = (await response.json()) as AdminSession
    this.updateSession(session)
    return session
  }

  async logout() {
    try {
      await this.request('/api/v1/auth/sessions/revoke', { method: 'POST' }, false)
    } finally {
      this.updateSession(null)
    }
  }

  async loadDashboard(): Promise<DashboardData> {
    const [overview, drivers, users, trips, payments, liveDrivers, disputes, audit] =
      await Promise.all([
        this.json('/api/v1/admin/overview'),
        this.json('/api/v1/admin/drivers?pageSize=100'),
        this.json('/api/v1/admin/users?pageSize=100'),
        this.json('/api/v1/admin/trips?pageSize=100'),
        this.json('/api/v1/admin/payments?pageSize=100'),
        this.json('/api/v1/admin/drivers/live'),
        this.json('/api/v1/admin/disputes?pageSize=100'),
        this.json('/api/v1/admin/audit?pageSize=100'),
      ])
    return { overview, drivers, users, trips, payments, liveDrivers, disputes, audit } as DashboardData
  }

  reviewDriver(driverId: string, approve: boolean, reason?: string) {
    return this.json<DriverApplication>(`/api/v1/admin/drivers/${driverId}/review`, {
      method: 'POST',
      body: JSON.stringify({ approve, reason: reason || null }),
    })
  }

  reviewDocument(driverId: string, documentId: string, approve: boolean, reason?: string) {
    return this.json<DriverApplication>(
      `/api/v1/admin/drivers/${driverId}/documents/${documentId}/review`,
      { method: 'POST', body: JSON.stringify({ approve, reason: reason || null }) },
    )
  }

  reviewDispute(disputeId: string, status: string, resolution?: string) {
    return this.json(`/api/v1/admin/disputes/${disputeId}/review`, {
      method: 'POST',
      body: JSON.stringify({ status, resolution: resolution || null }),
    })
  }

  async openDocument(driverId: string, documentId: string) {
    const response = await this.request(
      `/api/v1/admin/drivers/${driverId}/documents/${documentId}/content`,
    )
    const blob = await response.blob()
    return URL.createObjectURL(blob)
  }

  private async json<T>(path: string, options?: RequestInit): Promise<T> {
    const response = await this.request(path, options)
    return (await response.json()) as T
  }

  private async request(path: string, options: RequestInit = {}, allowRefresh = true): Promise<Response> {
    const session = this.getSession()
    if (!session) throw new ApiError('Your admin session has expired.', 401)
    const response = await fetch(`${appConfig.apiBaseUrl}${path}`, {
      ...options,
      headers: {
        ...(options.body ? { 'Content-Type': 'application/json' } : {}),
        ...options.headers,
        Authorization: `Bearer ${session.accessToken}`,
      },
    })
    if (response.status === 401 && allowRefresh && (await this.refresh())) {
      return this.request(path, options, false)
    }
    if (!response.ok) throw new ApiError(await readError(response), response.status)
    return response
  }

  private async refresh() {
    const current = this.getSession()
    if (!current) return false
    const response = await fetch(`${appConfig.apiBaseUrl}/api/v1/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: current.refreshToken }),
    })
    if (!response.ok) {
      this.updateSession(null)
      return false
    }
    this.updateSession((await response.json()) as AdminSession)
    return true
  }
}

export type { PagedResult }
