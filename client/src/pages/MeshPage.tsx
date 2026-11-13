import { useEffect, useMemo, useState } from 'react'
import { gatewayApi } from '../lib/api'
import { useGateway } from '../lib/gatewayContext'
import type { MeshHistoryDto, MeshTopologyDto, MeshVertexDto } from '../lib/types'
import { formatValue } from '../lib/types'

function toInputValue(date: Date) {
  const pad = (n: number) => n.toString().padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

export function MeshPage() {
  const { nodes, selectedMac, setSelectedMac, connected, packetRate } = useGateway()
  const [topology, setTopology] = useState<MeshTopologyDto | null>(null)
  const [fromLocal, setFromLocal] = useState(() => toInputValue(new Date(Date.now() - 2 * 60 * 60 * 1000)))
  const [toLocal, setToLocal] = useState(() => toInputValue(new Date()))
  const [history, setHistory] = useState<MeshHistoryDto | null>(null)
  const [query, setQuery] = useState('')

  const mac = selectedMac ?? topology?.vertices.find((v) => !v.isGateway)?.id ?? 'AA:7C:3F:10:00:01'

  useEffect(() => {
    let cancelled = false
    const load = () => {
      gatewayApi.meshTopology(mac).then((data) => {
        if (!cancelled) setTopology(data)
      }).catch(() => undefined)
    }
    load()
    const handle = window.setInterval(load, 2500)
    return () => {
      cancelled = true
      window.clearInterval(handle)
    }
  }, [mac, nodes.filter((n) => n.severity === 'Disconnected').length])

  const runRange = () => {
    const from = new Date(fromLocal).toISOString()
    const to = new Date(toLocal).toISOString()
    gatewayApi.meshHistory(mac, from, to).then(setHistory).catch(() => undefined)
  }

  useEffect(() => {
    runRange()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mac])

  const byId = useMemo(() => {
    const map = new Map<string, MeshVertexDto>()
    topology?.vertices.forEach((v) => map.set(v.id, v))
    return map
  }, [topology])

  const visibleNodes = topology?.vertices.filter((v) => {
    if (!query.trim()) return true
    const q = query.trim().toLowerCase()
    return v.label.toLowerCase().includes(q) || v.id.toLowerCase().includes(q) || v.facility.toLowerCase().includes(q)
  }) ?? []

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Network topology &amp; mesh routing</h1>
          <p className="text-sm text-slate-400">
            Vertices are ESP32 nodes. Edges are radio links. Emerald is the Kruskal MST (lowest milli-joule backbone).
            Cyan is the hop path of the selected MAC. BFS from the gateway finds who is still reachable when a node drops.
          </p>
        </div>
        <div className="flex items-center gap-3 text-sm">
          <span className={`mono rounded-full px-3 py-1 ${connected ? 'bg-emerald-400/15 text-emerald-300' : 'bg-red-400/15 text-red-300'}`}>
            {connected ? 'SignalR live' : 'reconnecting'}
          </span>
          <span className="mono rounded-full bg-white/5 px-3 py-1">{packetRate} pkt/s</span>
        </div>
      </div>

      <input
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder="Find MAC, pump, feeder, Stellenbosch…"
        className="w-full rounded-full border border-white/10 bg-slate-950 px-4 py-2 text-sm"
      />

      <div className="mesh-board">
        <div className="rounded-2xl border border-white/10 bg-slate-950/40 p-3 overflow-x-auto">
          <svg viewBox="0 0 1000 560" className="mesh-canvas min-w-[640px] w-full">
            {topology?.links.map((link) => {
              const a = byId.get(link.from)
              const b = byId.get(link.to)
              if (!a || !b) return null
              const stroke = link.onSelectedPath ? '#38bdf8' : link.onMst ? '#34d399' : 'rgba(148,163,184,0.28)'
              const width = link.onSelectedPath ? 3.4 : link.onMst ? 2.2 : 1
              return (
                <line
                  key={`${link.from}-${link.to}`}
                  x1={a.x}
                  y1={a.y}
                  x2={b.x}
                  y2={b.y}
                  stroke={stroke}
                  strokeWidth={width}
                  strokeDasharray={link.onMst || link.onSelectedPath ? undefined : '5 6'}
                />
              )
            })}
            {topology?.vertices.map((v) => {
              const selected = v.id === mac
              const r = v.isGateway ? 16 : 11
              return (
                <g key={v.id} className="cursor-pointer" onClick={() => setSelectedMac(v.id === 'GW-SMARTX' ? mac : v.id)}>
                  {v.isGateway ? (
                    <rect x={v.x - 16} y={v.y - 16} width={32} height={32} rx={6} fill="#022c22" stroke="#34d399" strokeWidth={selected ? 3 : 1.5} />
                  ) : (
                    <circle
                      cx={v.x}
                      cy={v.y}
                      r={r}
                      fill={v.isDown ? '#0f172a' : '#022c22'}
                      stroke={v.isDown ? '#94a3b8' : v.colourHex}
                      strokeWidth={selected ? 3 : 1.6}
                      strokeDasharray={v.isDown ? '3 3' : undefined}
                    />
                  )}
                  <text x={v.x} y={v.y + 28} textAnchor="middle" fontSize="10" fill="#cbd5e1">
                    {v.isGateway ? 'GATEWAY' : v.label.length > 22 ? `${v.label.slice(0, 20)}…` : v.label}
                  </text>
                </g>
              )
            })}
          </svg>
          <div className="mt-2 flex flex-wrap gap-3 text-[11px] text-slate-400">
            <span className="text-emerald-300">━ MST backbone</span>
            <span className="text-sky-300">━ selected hop path</span>
            <span>┄ radio neighbour</span>
            <span>dashed ring = down / partitioned</span>
          </div>
        </div>

        <aside className="space-y-4">
          <section className="rounded-2xl border border-emerald-400/20 bg-emerald-400/5 p-4">
            <h2 className="text-sm font-semibold uppercase tracking-widest text-emerald-200">MST · Kruskal</h2>
            <p className="mono mt-2 text-2xl">{topology?.mstCostMilliJoule ?? '—'} mJ</p>
            <p className="mt-1 text-xs text-slate-400">{topology?.mstEdges.length ?? 0} cheapest live radio edges spanning the mesh.</p>
            <p className="mt-3 text-sm text-slate-300">{topology?.healSummary}</p>
          </section>
          <section className="rounded-2xl border border-sky-400/20 bg-sky-400/5 p-4">
            <h2 className="text-sm font-semibold uppercase tracking-widest text-sky-200">BFS from gateway</h2>
            <p className="mt-2 text-sm">{topology?.bfsOrder.length ?? 0} reachable · {topology?.partitioned.length ?? 0} partitioned</p>
            <ul className="mt-2 space-y-1 text-xs mono text-amber-100">
              {(topology?.disconnectedMacs ?? []).slice(0, 6).map((id) => <li key={id}>{id}</li>)}
              {(topology?.disconnectedMacs.length ?? 0) === 0 && <li className="text-slate-500">No silent nodes.</li>}
            </ul>
            {!!topology?.alternateHops.length && (
              <p className="mt-2 text-xs text-sky-200">
                Reroute via {topology.alternateHops.map((h) => `${h.to} (${h.costMilliJoule} mJ)`).join(', ')}
              </p>
            )}
          </section>
          <section className="rounded-2xl border border-white/10 bg-white/5 p-4">
            <h2 className="text-sm font-semibold uppercase tracking-widest text-slate-400">Hop path</h2>
            <ol className="mt-2 space-y-1 text-xs">
              {(topology?.hopPath ?? []).map((id, index) => (
                <li key={id} className="flex justify-between gap-2">
                  <span>{index}. {byId.get(id)?.label ?? id}</span>
                  <span className="mono text-slate-500">{id === 'GW-SMARTX' ? 'GW' : id.slice(-8)}</span>
                </li>
              ))}
            </ol>
          </section>
        </aside>
      </div>

      <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
        <div className="flex flex-wrap items-end gap-3">
          <label className="text-sm">From
            <input type="datetime-local" className="mt-1 block rounded-lg border border-white/10 bg-slate-950 px-3 py-2" value={fromLocal} onChange={(e) => setFromLocal(e.target.value)} />
          </label>
          <label className="text-sm">To
            <input type="datetime-local" className="mt-1 block rounded-lg border border-white/10 bg-slate-950 px-3 py-2" value={toLocal} onChange={(e) => setToLocal(e.target.value)} />
          </label>
          <button type="button" className="rounded-full bg-emerald-400 px-4 py-2 text-sm font-semibold text-slate-950" onClick={runRange}>
            AVL range query
          </button>
          <p className="text-xs text-slate-400">
            Indexed {history?.avlCount ?? topology?.avlSampleCount ?? 0} samples on {mac}. Matched {history?.matched ?? 0} in window.
          </p>
        </div>
        <div className="mt-3 max-h-64 overflow-auto">
          <table className="w-full text-left text-xs">
            <thead className="text-slate-500">
              <tr><th className="py-1">Time (UTC)</th><th>Value</th><th>Severity</th></tr>
            </thead>
            <tbody>
              {(history?.samples ?? []).slice(-40).reverse().map((row) => (
                <tr key={row.packetId} className="border-t border-white/5">
                  <td className="py-1 mono">{row.timestamp.replace('T', ' ').slice(0, 19)}</td>
                  <td>{formatValue(row)}</td>
                  <td style={{ color: row.colourHex }}>{row.severity}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        {visibleNodes.filter((v) => !v.isGateway).slice(0, 21).map((v) => (
          <button
            key={v.id}
            type="button"
            onClick={() => setSelectedMac(v.id)}
            className={`rounded-full px-3 py-1 text-xs ${v.id === mac ? 'bg-white text-slate-950' : 'bg-white/10'}`}
          >
            {v.label}
          </button>
        ))}
      </div>
    </div>
  )
}
