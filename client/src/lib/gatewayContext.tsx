import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'
import type { InsightDto } from './types'
import { useTelemetryStream } from './useTelemetry'

type GatewayContextValue = ReturnType<typeof useTelemetryStream> & {
  selectedMac: string | null
  setSelectedMac: (mac: string | null) => void
  telemetryFilter: string
  setTelemetryFilter: (value: string) => void
  commandQuery: string
  setCommandQuery: (value: string) => void
  commandCategory: string
  setCommandCategory: (value: string) => void
  commandSeverity: string
  setCommandSeverity: (value: string) => void
  insights: InsightDto[]
  setInsights: (value: InsightDto[]) => void
  commandMessage: string
  setCommandMessage: (value: string) => void
}

const GatewayContext = createContext<GatewayContextValue | null>(null)

export function GatewayProvider({ children }: { children: ReactNode }) {
  const stream = useTelemetryStream()
  const [selectedMac, setSelectedMac] = useState<string | null>(null)
  const [telemetryFilter, setTelemetryFilter] = useState('all')
  const [commandQuery, setCommandQuery] = useState('')
  const [commandCategory, setCommandCategory] = useState('all')
  const [commandSeverity, setCommandSeverity] = useState('all')
  const [insights, setInsights] = useState<InsightDto[]>([])
  const [commandMessage, setCommandMessage] = useState('')
  const value = useMemo(
    () => ({
      ...stream,
      selectedMac,
      setSelectedMac,
      telemetryFilter,
      setTelemetryFilter,
      commandQuery,
      setCommandQuery,
      commandCategory,
      setCommandCategory,
      commandSeverity,
      setCommandSeverity,
      insights,
      setInsights,
      commandMessage,
      setCommandMessage,
    }),
    [stream, selectedMac, telemetryFilter, commandQuery, commandCategory, commandSeverity, insights, commandMessage],
  )
  return <GatewayContext.Provider value={value}>{children}</GatewayContext.Provider>
}

export function useGateway() {
  const ctx = useContext(GatewayContext)
  if (!ctx) {
    throw new Error('useGateway must be used inside GatewayProvider')
  }
  return ctx
}
