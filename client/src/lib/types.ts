export type AlertSeverity = 'Nominal' | 'Warning' | 'Disconnected' | 'Critical'
export type SensorCategory = 'Environmental' | 'PowerConsumption' | 'Actuator'
export type TelemetryValueKind = 'Float' | 'Integer' | 'Boolean'
export type DeploymentKind = 'Facility' | 'Zone' | 'SubZone' | 'Node'

export type PillarDto = {
  id: string
  title: string
  summary: string
  enabled: boolean
  deferredUntil?: string | null
}

export type AttachmentDto = {
  id: string
  fileName: string
  contentType: string
  byteSize: number
  uploadedAt: string
}

export type SensorDto = {
  id: string
  macAddress: string
  displayName: string
  location: string
  category: SensorCategory
  metric: string
  unit: string
  valueKind: TelemetryValueKind
  deploymentPath: string
  attachments: AttachmentDto[]
}

export type TelemetrySampleDto = {
  packetId: string
  deviceId: string
  displayName: string
  location: string
  metric: string
  unit?: string | null
  valueKind: TelemetryValueKind
  numericValue?: number | null
  booleanValue?: boolean | null
  timestamp: string
  sequence: number
  severity: AlertSeverity
  severityLabel: string
  colourHex: string
  isStale: boolean
  secondsSinceLastSeen: number
}

export type SensorLiveDto = {
  sensor: SensorDto
  latest: TelemetrySampleDto | null
  sparkline: TelemetrySampleDto[]
  severity: AlertSeverity
  colourHex: string
}

export type DeploymentNodeDto = {
  id: string
  name: string
  kind: DeploymentKind
  macAddress?: string | null
  children?: DeploymentNodeDto[]
}

export const formatValue = (sample: TelemetrySampleDto | null) => {
  if (!sample) return '—'
  if (sample.valueKind === 'Boolean') return sample.booleanValue ? 'ON' : 'OFF'
  if (sample.numericValue == null) return '—'
  return `${sample.numericValue.toFixed(sample.valueKind === 'Integer' ? 0 : 2)} ${sample.unit ?? ''}`
}
