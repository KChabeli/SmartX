import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { apiBase, gatewayApi } from './api'
import type { SensorLiveDto, TelemetrySampleDto } from './types'

export function useTelemetryStream() {
  const [nodes, setNodes] = useState<SensorLiveDto[]>([])
  const [alerts, setAlerts] = useState<TelemetrySampleDto[]>([])
  const [connected, setConnected] = useState(false)
  const [packetRate, setPacketRate] = useState(0)
  const hits = useRef(0)

  useEffect(() => {
    let cancelled = false
    gatewayApi.snapshot().then((data) => {
      if (!cancelled) setNodes(data)
    }).catch(() => undefined)

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBase}/hubs/telemetry`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('telemetry', (sample: TelemetrySampleDto) => {
      hits.current += 1
      setNodes((current) => upsert(current, sample))
    })
    connection.on('alert', (sample: TelemetrySampleDto) => {
      setAlerts((current) => [sample, ...current].slice(0, 12))
    })

    connection.start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false))

    const rateTimer = window.setInterval(() => {
      setPacketRate(hits.current)
      hits.current = 0
    }, 1000)

    const refreshTimer = window.setInterval(() => {
      gatewayApi.snapshot().then(setNodes).catch(() => undefined)
    }, 8000)

    return () => {
      cancelled = true
      window.clearInterval(rateTimer)
      window.clearInterval(refreshTimer)
      connection.stop().catch(() => undefined)
    }
  }, [])

  return { nodes, alerts, connected, packetRate }
}

function upsert(nodes: SensorLiveDto[], sample: TelemetrySampleDto): SensorLiveDto[] {
  const index = nodes.findIndex((n) => n.sensor.macAddress === sample.deviceId)
  if (index < 0) return nodes
  const existing = nodes[index]
  const sparkline = [...existing.sparkline, sample].slice(-48)
  const next = [...nodes]
  next[index] = {
    ...existing,
    latest: sample,
    sparkline,
    severity: sample.severity,
    colourHex: sample.colourHex,
  }
  return next
}
