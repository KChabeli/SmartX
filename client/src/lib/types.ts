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

export type CommandDto = {
  commandId: string
  targetMac: string
  targetName: string
  verb: string
  previousState: boolean
  newState: boolean
  issuedAt: string
  reason: string
  wasUndone: boolean
}

export type InsightDto = {
  id: string
  title: string
  rationale: string
  confidence: number
  targetMac: string
  targetName: string
  verb: string
  contextMac: string
  contextName: string
  context: string
  source?: string
  supportCount?: number
  contextCount?: number
}

export type MeshVertexDto = {
  id: string
  label: string
  facility: string
  zone: string
  x: number
  y: number
  isGateway: boolean
  severity: string
  isDown: boolean
  reachableFromGateway: boolean
  metric: string
  colourHex: string
}

export type MeshLinkDto = {
  from: string
  to: string
  costMilliJoule: number
  radio: string
  onMst: boolean
  onSelectedPath: boolean
}

export type MeshHopDto = {
  from: string
  to: string
  costMilliJoule: number
}

export type MeshTopologyDto = {
  vertices: MeshVertexDto[]
  links: MeshLinkDto[]
  hopPath: string[]
  bfsOrder: string[]
  partitioned: string[]
  mstEdges: MeshLinkDto[]
  mstCostMilliJoule: number
  mstAlgorithm: string
  alternateHops: MeshHopDto[]
  healSummary: string
  avlSampleCount: number
  disconnectedMacs: string[]
}

export type MeshHistoryDto = {
  deviceId: string
  from: string
  to: string
  avlCount: number
  matched: number
  samples: TelemetrySampleDto[]
}

export type RealtimeDiagnosticsDto = {
  standardQueueDepth: number
  priorityQueueDepth: number
  undoStackDepth: number
  dictionarySize: number
  sortedLogCount: number
  uniqueFaults: number
  disconnectedNodes: number
  standardEnqueued: number
  priorityEnqueued: number
  standardProcessed: number
  priorityProcessed: number
  activeFaults: string[]
  disconnectedMacs: string[]
}

export const formatValue = (sample: TelemetrySampleDto | null) => {
  if (!sample) return '—'
  if (sample.valueKind === 'Boolean') return sample.booleanValue ? 'ON' : 'OFF'
  if (sample.numericValue == null) return '—'
  return `${sample.numericValue.toFixed(sample.valueKind === 'Integer' ? 0 : 2)} ${sample.unit ?? ''}`
}
