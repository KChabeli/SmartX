import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'
import { useTelemetryStream } from './useTelemetry'

type GatewayContextValue = ReturnType<typeof useTelemetryStream> & {
  selectedMac: string | null
  setSelectedMac: (mac: string | null) => void
}

const GatewayContext = createContext<GatewayContextValue | null>(null)

export function GatewayProvider({ children }: { children: ReactNode }) {
  const stream = useTelemetryStream()
  const [selectedMac, setSelectedMac] = useState<string | null>(null)
  const value = useMemo(
    () => ({ ...stream, selectedMac, setSelectedMac }),
    [stream, selectedMac],
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
