import { Area, AreaChart, CartesianGrid, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { SensorLiveDto } from '../lib/types'

type Props = { node: SensorLiveDto | null }

export function LiveChart({ node }: Props) {
  if (!node) {
    return <div className="flex h-72 items-center justify-center rounded-2xl border border-white/10 text-slate-500">Select a node to inspect its threshold corridor.</div>
  }

  const data = node.sparkline.map((s) => ({
    t: new Date(s.timestamp).toLocaleTimeString(),
    value: s.isStale ? null : (s.numericValue ?? (s.booleanValue ? 1 : 0)),
    colour: s.colourHex,
    stale: s.isStale,
  }))
  const disconnect = node.sparkline.find((s) => s.isStale)

  return (
    <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
      <div className="mb-3 flex flex-wrap items-end justify-between gap-2">
        <div>
          <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Threshold corridor</p>
          <h2 className="text-xl font-semibold">{node.sensor.displayName}</h2>
          <p className="text-sm text-slate-400">{node.sensor.location}</p>
        </div>
        <p className="mono text-sm" style={{ color: node.colourHex }}>{node.latest?.severityLabel}</p>
      </div>
      <div className="h-72">
        <ResponsiveContainer>
          <AreaChart data={data}>
            <defs>
              <linearGradient id="fill" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={node.colourHex} stopOpacity={0.45} />
                <stop offset="100%" stopColor={node.colourHex} stopOpacity={0.02} />
              </linearGradient>
            </defs>
            <CartesianGrid stroke="rgba(255,255,255,0.06)" />
            <XAxis dataKey="t" tick={{ fill: '#94a3b8', fontSize: 11 }} />
            <YAxis tick={{ fill: '#94a3b8', fontSize: 11 }} domain={['auto', 'auto']} />
            <Tooltip contentStyle={{ background: '#0b1a27', border: '1px solid #1e3a4c', color: '#e8eef7' }} />
            {disconnect && <ReferenceLine x={new Date(disconnect.timestamp).toLocaleTimeString()} stroke="#94A3B8" strokeDasharray="4 4" label={{ value: 'disconnect', fill: '#94A3B8', fontSize: 11 }} />}
            <Area type="monotone" dataKey="value" stroke={node.colourHex} fill="url(#fill)" connectNulls={false} />
          </AreaChart>
        </ResponsiveContainer>
      </div>
      <p className="mt-2 text-xs text-slate-400">
        Dual cue: the halo colour pops before you read the number, and a chart gap (null points / dashed marker) separates a sensor disconnect from a genuine spike.
      </p>
    </div>
  )
}
