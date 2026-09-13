import type { SensorLiveDto } from '../lib/types'
import { formatValue } from '../lib/types'

type Props = {
  node: SensorLiveDto
  selected: boolean
  onSelect: () => void
}

export function HaloCard({ node, selected, onSelect }: Props) {
  const colour = node.colourHex
  const stale = node.latest?.isStale ?? node.severity === 'Disconnected'
  const haloClass =
    node.severity === 'Critical' ? 'halo-critical'
      : node.severity === 'Disconnected' ? 'halo-disconnected'
        : node.severity === 'Warning' ? 'halo-warning'
          : 'halo-nominal'
  const dash = stale ? '6 6' : undefined
  const spark = node.sparkline.map((s) => s.numericValue ?? (s.booleanValue ? 1 : 0))
  const max = Math.max(...spark, 1)
  const min = Math.min(...spark, 0)
  const path = spark.map((v, i) => {
    const x = (i / Math.max(spark.length - 1, 1)) * 120
    const y = 28 - ((v - min) / Math.max(max - min, 0.01)) * 24
    return `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`
  }).join(' ')

  return (
    <button
      type="button"
      onClick={onSelect}
      className={`text-left rounded-2xl border p-3 transition ${selected ? 'border-sky-400 bg-sky-400/10' : 'border-white/10 bg-white/5 hover:border-white/25'}`}
    >
      <div className="flex gap-3">
        <svg width="72" height="72" viewBox="0 0 72 72" aria-hidden>
          <circle cx="36" cy="36" r="30" fill="none" stroke={colour} strokeOpacity="0.2" strokeWidth="6" />
          <circle
            className={haloClass}
            cx="36"
            cy="36"
            r="30"
            fill="none"
            stroke={colour}
            strokeWidth={stale ? 3 : 4}
            strokeDasharray={dash}
            style={{ transformOrigin: '36px 36px' }}
          />
          <circle cx="36" cy="36" r="18" fill={colour} fillOpacity={stale ? 0.12 : 0.22} />
          <text x="36" y="40" textAnchor="middle" fontSize="9" fill="#e8eef7" fontFamily="IBM Plex Mono">
            {stale ? 'GAP' : node.severity === 'Critical' ? 'SPIKE' : 'LIVE'}
          </text>
        </svg>
        <div className="min-w-0 flex-1">
          <div className="flex items-center justify-between gap-2">
            <p className="truncate text-sm font-semibold">{node.sensor.displayName}</p>
            <span className="mono text-[10px] uppercase" style={{ color: colour }}>{node.latest?.severityLabel ?? node.severity}</span>
          </div>
          <p className="truncate text-[11px] text-slate-400">{node.sensor.location}</p>
          <p className="mono mt-1 text-lg text-white">{formatValue(node.latest)}</p>
          <p className="mono text-[10px] text-slate-500">
            last seen {node.latest?.secondsSinceLastSeen ?? '—'}s · {node.sensor.macAddress}
          </p>
          {path && (
            <svg width="100%" height="32" viewBox="0 0 120 32" preserveAspectRatio="none" className="mt-1">
              <path d={path} fill="none" stroke={colour} strokeWidth="1.6" />
            </svg>
          )}
        </div>
      </div>
    </button>
  )
}
