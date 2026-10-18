import type { CommandDto, DeploymentNodeDto, InsightDto, PillarDto, RealtimeDiagnosticsDto, SensorDto, SensorLiveDto, TelemetrySampleDto } from './types'

const api = import.meta.env.VITE_API_URL ?? ''

async function json<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  const raw = sessionStorage.getItem('smartx-session')
  if (raw) {
    try {
      const session = JSON.parse(raw) as { token?: string }
      if (session.token) headers.set('Authorization', `Bearer ${session.token}`)
    } catch {
      /* ignore broken session */
    }
  }
  if (init?.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  const response = await fetch(`${api}${path}`, { ...init, headers })
  if (!response.ok) {
    const text = await response.text()
    try {
      const parsed = JSON.parse(text) as { message?: string }
      throw new Error(parsed.message || text || response.statusText)
    } catch (err) {
      if (err instanceof SyntaxError) {
        throw new Error(text || response.statusText)
      }
      throw err
    }
  }
  if (response.status === 204) {
    return undefined as T
  }
  return response.json() as Promise<T>
}

export const gatewayApi = {
  login: (macAddress: string) =>
    json<{ token: string; macAddress: string; displayName: string }>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ macAddress }),
    }),
  logout: () => json<void>('/api/auth/logout', { method: 'POST' }),
  pillars: () => json<PillarDto[]>('/api/pillars'),
  snapshot: () => json<SensorLiveDto[]>('/api/telemetry/snapshot'),
  history: (deviceId: string) => json<TelemetrySampleDto[]>(`/api/telemetry?deviceId=${encodeURIComponent(deviceId)}&take=180`),
  sensors: () => json<SensorDto[]>('/api/sensors'),
  registerSensor: (body: Record<string, unknown>) =>
    json<SensorDto>('/api/sensors', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),
  ingest: (body: Record<string, unknown>) =>
    json<TelemetrySampleDto>('/api/telemetry/ingest', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),
  spike: (deviceId: string) =>
    json<TelemetrySampleDto>(`/api/telemetry/simulate-spike?deviceId=${encodeURIComponent(deviceId)}`, { method: 'POST' }),
  upload: async (mac: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return json(`/api/sensors/${encodeURIComponent(mac)}/attachments`, { method: 'POST', body: form })
  },
  aggregateMeters: (body: Record<string, unknown>) =>
    json<{ expression: string; combinedWatts: number; combinedPowerFactor: number; leftExceedsRight: boolean }>(
      '/api/meters/aggregate',
      { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) },
    ),
  sampleDeployment: () => json<{ tree: DeploymentNodeDto; validation: { isSafe: boolean; errors: string[]; resolvedPath: string; ancestorChain?: string[] } }>(
    '/api/deployments/sample',
  ),
  validateDeployment: (root: DeploymentNodeDto, requiredMac?: string) =>
    json('/api/deployments/validate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ root, requiredMac }),
    }),
  diagnostics: () => json<RealtimeDiagnosticsDto>('/api/realtime/diagnostics'),
  insights: () => json<InsightDto[]>('/api/realtime/insights'),
  commandHistory: () => json<CommandDto[]>('/api/commands'),
  issueCommand: (targetMac: string, verb: string, reason?: string) =>
    json<{ command: CommandDto; insights: InsightDto[] }>('/api/commands', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ targetMac, verb, reason }),
    }),
  undoCommand: () =>
    json<CommandDto>('/api/commands/undo', { method: 'POST' }),
  searchDevices: (query: string, category: string, severity: string) =>
    json<{ matches: SensorLiveDto[]; relatedActuators: Record<string, string>; insights: InsightDto[] }>(
      '/api/realtime/search',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          query: query || null,
          category: category === 'all' ? null : category,
          severity: severity === 'all' ? null : severity,
        }),
      },
    ),
  demoPriority: () => json<{ explanation: string; processingOrder: string[] }>('/api/realtime/demo-priority'),
}

export const apiBase = api
