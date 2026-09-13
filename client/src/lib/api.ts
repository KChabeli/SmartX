import type { DeploymentNodeDto, PillarDto, SensorDto, SensorLiveDto, TelemetrySampleDto } from './types'

const api = import.meta.env.VITE_API_URL ?? ''

async function json<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${api}${path}`, init)
  if (!response.ok) {
    const text = await response.text()
    throw new Error(text || response.statusText)
  }
  return response.json() as Promise<T>
}

export const gatewayApi = {
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
}

export const apiBase = api
