import { useMemo, useState } from 'react'
import { HaloCard } from '../components/HaloCard'
import { LiveChart } from '../components/LiveChart'
import { gatewayApi } from '../lib/api'
import { useGateway } from '../lib/gatewayContext'

export function DashboardPage() {
  const { nodes, alerts, connected, packetRate, selectedMac, setSelectedMac } = useGateway()
  const [filter, setFilter] = useState('all')

  const visible = useMemo(
    () => nodes.filter((n) => filter === 'all' || n.sensor.category === filter),
    [nodes, filter],
  )
  const selected = visible.find((n) => n.sensor.macAddress === selectedMac) ?? visible[0] ?? null
  const disconnects = nodes.filter((n) => n.severity === 'Disconnected')
  const spikes = nodes.filter((n) => n.severity === 'Critical')

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Threshold Halo Grid</h1>
          <p className="text-sm text-slate-400">Research strategy: semantic colour + real-time streaming. Disconnects freeze as dashed grey halos; spikes expand red.</p>
        </div>
        <div className="flex items-center gap-3 text-sm">
          <span className={`mono rounded-full px-3 py-1 ${connected ? 'bg-emerald-400/15 text-emerald-300' : 'bg-red-400/15 text-red-300'}`}>
            {connected ? 'SignalR live' : 'reconnecting'}
          </span>
          <span className="mono rounded-full bg-white/5 px-3 py-1">{packetRate} pkt/s</span>
        </div>
      </div>

      <div className="flex flex-wrap gap-2 text-xs">
        {[
          ['all', 'All nodes'],
          ['Environmental', 'Environmental'],
          ['PowerConsumption', 'Power'],
          ['Actuator', 'Actuators'],
        ].map(([id, label]) => (
          <button key={id} type="button" onClick={() => setFilter(id)} className={`rounded-full px-3 py-1 ${filter === id ? 'bg-white text-slate-950' : 'bg-white/10'}`}>
            {label}
          </button>
        ))}
      </div>

      <div className="halo-board">
        <div className="halo-cards">
          {visible.map((node) => (
            <HaloCard
              key={node.sensor.macAddress}
              node={node}
              selected={selected?.sensor.macAddress === node.sensor.macAddress}
              onSelect={() => setSelectedMac(node.sensor.macAddress)}
            />
          ))}
        </div>
        <aside className="space-y-4">
          <section className="rounded-2xl border border-amber-400/20 bg-amber-400/5 p-4">
            <h2 className="text-sm font-semibold uppercase tracking-widest text-amber-200">Disconnect watch</h2>
            <ul className="mt-2 space-y-2 text-sm">
              {disconnects.length === 0 && <li className="text-slate-500">All heartbeats present.</li>}
              {disconnects.map((n) => (
                <li key={n.sensor.macAddress} className="flex justify-between gap-2">
                  <span>{n.sensor.displayName}</span>
                  <span className="mono text-amber-200">{n.latest?.secondsSinceLastSeen}s silent</span>
                </li>
              ))}
            </ul>
          </section>
          <section className="rounded-2xl border border-red-400/20 bg-red-400/5 p-4">
            <h2 className="text-sm font-semibold uppercase tracking-widest text-red-200">Spike radar</h2>
            <ul className="mt-2 space-y-2 text-sm">
              {spikes.length === 0 && <li className="text-slate-500">No critical breaches.</li>}
              {spikes.map((n) => (
                <li key={n.sensor.macAddress} className="flex items-center justify-between gap-2">
                  <span>{n.sensor.displayName}</span>
                  <button type="button" className="rounded-full bg-red-400 px-2 py-0.5 text-xs text-slate-950" onClick={() => setSelectedMac(n.sensor.macAddress)}>inspect</button>
                </li>
              ))}
            </ul>
          </section>
          <section className="rounded-2xl border border-white/10 bg-white/5 p-4">
            <h2 className="text-sm font-semibold uppercase tracking-widest text-slate-400">Recent alerts</h2>
            <ul className="mt-2 space-y-2 text-xs">
              {alerts.slice(0, 6).map((a) => (
                <li key={a.packetId} className="flex justify-between gap-2">
                  <span>{a.displayName}</span>
                  <span style={{ color: a.colourHex }}>{a.severityLabel}</span>
                </li>
              ))}
            </ul>
          </section>
        </aside>
      </div>

      <LiveChart node={selected} />

      {selected && (
        <button
          type="button"
          className="rounded-full border border-red-400/40 px-4 py-2 text-sm text-red-200"
          onClick={() => gatewayApi.spike(selected.sensor.macAddress)}
        >
          Inject anomaly spike on selected node
        </button>
      )}
    </div>
  )
}
