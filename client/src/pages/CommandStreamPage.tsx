import { useEffect, useMemo, useState } from 'react'
import { gatewayApi } from '../lib/api'
import { useGateway } from '../lib/gatewayContext'
import type { InsightDto, SensorLiveDto } from '../lib/types'
import { formatValue } from '../lib/types'

export function CommandStreamPage() {
  const { nodes, commands, queues, connected, packetRate, selectedMac, setSelectedMac } = useGateway()
  const [query, setQuery] = useState('')
  const [category, setCategory] = useState('all')
  const [severity, setSeverity] = useState('all')
  const [insights, setInsights] = useState<InsightDto[]>([])
  const [message, setMessage] = useState('')
  const [demoOrder, setDemoOrder] = useState<string[]>([])

  useEffect(() => {
    gatewayApi.insights().then(setInsights).catch(() => undefined)
  }, [queues?.uniqueFaults, queues?.priorityEnqueued])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      gatewayApi.searchDevices(query, category, severity)
        .then((result) => setInsights(result.insights))
        .catch(() => undefined)
    }, 280)
    return () => window.clearTimeout(handle)
  }, [query, category, severity, nodes.length])

  const visible = useMemo(() => {
    const q = query.trim().toLowerCase()
    return nodes.filter((n) => {
      if (category !== 'all' && n.sensor.category !== category) return false
      if (severity !== 'all' && n.severity !== severity) return false
      if (!q) return true
      return n.sensor.displayName.toLowerCase().includes(q)
        || n.sensor.macAddress.toLowerCase().includes(q)
        || n.sensor.location.toLowerCase().includes(q)
        || n.sensor.metric.toLowerCase().includes(q)
    })
  }, [nodes, query, category, severity])

  const selected = visible.find((n) => n.sensor.macAddress === selectedMac) ?? visible[0]

  const issue = async (mac: string, verb: string, reason: string) => {
    try {
      const result = await gatewayApi.issueCommand(mac, verb, reason)
      setInsights(result.insights)
      setMessage(`${verb} ${result.command.targetName}`)
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Command failed')
    }
  }

  const undo = async () => {
    try {
      const reverted = await gatewayApi.undoCommand()
      setMessage(`Undid override on ${reverted.targetName}`)
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Nothing to undo')
    }
  }

  const runDemo = async () => {
    const demo = await gatewayApi.demoPriority()
    setDemoOrder(demo.processingOrder)
    setMessage(demo.explanation)
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Command stream &amp; override rail</h1>
          <p className="text-sm text-slate-400">
            ESP32 packets enter a FIFO <span className="mono">Queue&lt;T&gt;</span>. Critical spikes jump a
            {' '}<span className="mono">PriorityQueue</span>. Manual overrides push a <span className="mono">Stack&lt;T&gt;</span> so the last command can be undone.
          </p>
        </div>
        <div className="flex flex-wrap gap-2 text-sm">
          <span className={`mono rounded-full px-3 py-1 ${connected ? 'bg-emerald-400/15 text-emerald-300' : 'bg-red-400/15 text-red-300'}`}>
            {connected ? 'SignalR live' : 'reconnecting'}
          </span>
          <span className="mono rounded-full bg-white/5 px-3 py-1">{packetRate} pkt/s</span>
          <button type="button" onClick={undo} className="rounded-full bg-white px-3 py-1 font-semibold text-slate-950">
            Undo last override
          </button>
        </div>
      </div>

      <LaneStrip queues={queues} />

      <div className="flex flex-wrap gap-2">
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search MAC, pump, moisture, feeder…"
          className="min-w-[16rem] flex-1 rounded-full border border-white/10 bg-slate-950 px-4 py-2 text-sm"
        />
        {(['all', 'Environmental', 'PowerConsumption', 'Actuator'] as const).map((id) => (
          <Chip key={id} active={category === id} onClick={() => setCategory(id)}>{id === 'PowerConsumption' ? 'Power' : id}</Chip>
        ))}
        {(['all', 'Critical', 'Warning', 'Disconnected', 'Nominal'] as const).map((id) => (
          <Chip key={id} active={severity === id} onClick={() => setSeverity(id)}>{id}</Chip>
        ))}
      </div>

      <div className="command-board">
        <div className="space-y-3">
          {visible.map((node) => (
            <DeviceRow
              key={node.sensor.macAddress}
              node={node}
              selected={selected?.sensor.macAddress === node.sensor.macAddress}
              onSelect={() => setSelectedMac(node.sensor.macAddress)}
              onCommand={issue}
            />
          ))}
          {visible.length === 0 && <p className="text-sm text-slate-500">No devices match that filter.</p>}
        </div>

        <aside className="space-y-4">
          <section className="rounded-2xl border border-sky-400/30 bg-sky-400/10 p-4">
            <h2 className="text-sm font-semibold uppercase tracking-widest text-sky-200">Automated insights</h2>
            <p className="mt-1 text-xs text-slate-400">Context-conditioned association: what operators actually did after this metric went critical — not a hard-coded if-then.</p>
            <ul className="mt-3 space-y-3">
              {insights.length === 0 && <li className="text-sm text-slate-500">Waiting for a critical or disconnect context… inject a moisture spike to see Pump A suggested.</li>}
              {insights.map((insight) => (
                <li key={insight.id} className="rounded-xl border border-white/10 bg-slate-950/40 p-3">
                  <p className="font-medium">{insight.title}</p>
                  <p className="mt-1 text-xs text-slate-400">{insight.rationale}</p>
                  <div className="mt-2 flex items-center justify-between gap-2">
                    <span className="mono text-xs text-sky-300">{insight.confidence}% confidence</span>
                    <button
                      type="button"
                      className="rounded-full bg-sky-400 px-3 py-1 text-xs font-semibold text-slate-950"
                      onClick={() => issue(insight.targetMac, insight.verb, `insight:${insight.context}`)}
                    >
                      Execute
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          </section>

          <section className="rounded-2xl border border-white/10 bg-white/5 p-4">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold uppercase tracking-widest text-slate-400">Undo stack</h2>
              <button type="button" className="text-xs text-slate-400 underline" onClick={runDemo}>Demo priority bypass</button>
            </div>
            <ul className="mt-2 space-y-2 text-xs">
              {commands.length === 0 && <li className="text-slate-500">Stack empty — issue an override.</li>}
              {commands.slice(0, 8).map((cmd) => (
                <li key={cmd.commandId} className="flex justify-between gap-2">
                  <span>{cmd.verb} · {cmd.targetName}</span>
                  <span className="mono text-slate-500">{cmd.reason}</span>
                </li>
              ))}
            </ul>
            {demoOrder.length > 0 && (
              <p className="mono mt-3 text-[11px] text-amber-200">Drain order: {demoOrder.join(' → ')}</p>
            )}
          </section>

          {queues && (
            <section className="rounded-2xl border border-white/10 bg-white/5 p-4 text-xs">
              <h2 className="text-sm font-semibold uppercase tracking-widest text-slate-400">Unique fault set</h2>
              <p className="mt-1 text-slate-500"><span className="mono">HashSet</span> blocks duplicate disconnect processing. Dictionary size {queues.dictionarySize}. Sorted log {queues.sortedLogCount}.</p>
              <ul className="mt-2 space-y-1 mono text-amber-100">
                {queues.activeFaults.length === 0 && <li className="text-slate-500">No unique faults.</li>}
                {queues.activeFaults.slice(0, 8).map((f) => <li key={f}>{f}</li>)}
              </ul>
            </section>
          )}
        </aside>
      </div>

      {message && <p className="text-sm text-emerald-300">{message}</p>}
    </div>
  )
}

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: string }) {
  return (
    <button type="button" onClick={onClick} className={`rounded-full px-3 py-1 text-xs ${active ? 'bg-white text-slate-950' : 'bg-white/10'}`}>
      {children}
    </button>
  )
}

function LaneStrip({ queues }: { queues: ReturnType<typeof useGateway>['queues'] }) {
  const fifo = queues?.standardProcessed ?? 0
  const prio = queues?.priorityProcessed ?? 0
  const total = Math.max(fifo + prio, 1)
  return (
    <div className="grid gap-3 md:grid-cols-2">
      <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
        <p className="text-xs uppercase tracking-widest text-slate-400">Standard FIFO lane · Queue&lt;T&gt;</p>
        <p className="mono mt-1 text-2xl">{queues?.standardQueueDepth ?? 0} waiting · {fifo} processed</p>
        <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-900">
          <div className="h-full bg-emerald-400" style={{ width: `${Math.min(100, (fifo / total) * 100)}%` }} />
        </div>
      </div>
      <div className="rounded-2xl border border-red-400/30 bg-red-400/10 p-4">
        <p className="text-xs uppercase tracking-widest text-red-200">Critical bypass · PriorityQueue</p>
        <p className="mono mt-1 text-2xl">{queues?.priorityQueueDepth ?? 0} waiting · {prio} processed</p>
        <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-900">
          <div className="h-full bg-red-400" style={{ width: `${Math.min(100, (prio / total) * 100)}%` }} />
        </div>
      </div>
    </div>
  )
}

function DeviceRow({
  node,
  selected,
  onSelect,
  onCommand,
}: {
  node: SensorLiveDto
  selected: boolean
  onSelect: () => void
  onCommand: (mac: string, verb: string, reason: string) => void
}) {
  const actuator = node.sensor.category === 'Actuator'
  return (
    <article className={`rounded-2xl border p-4 ${selected ? 'border-sky-400 bg-sky-400/10' : 'border-white/10 bg-white/5'}`}>
      <button type="button" onClick={onSelect} className="w-full text-left">
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="font-semibold">{node.sensor.displayName}</p>
            <p className="mono text-[11px] text-slate-400">{node.sensor.macAddress} · {node.sensor.category}</p>
          </div>
          <span className="mono text-xs" style={{ color: node.colourHex }}>{node.latest?.severityLabel ?? node.severity} · {formatValue(node.latest)}</span>
        </div>
      </button>
      {actuator && (
        <div className="mt-3 flex gap-2">
          <button type="button" className="rounded-full bg-emerald-400 px-3 py-1 text-xs font-semibold text-slate-950" onClick={() => onCommand(node.sensor.macAddress, 'Energise', 'manual-override')}>
            Energise
          </button>
          <button type="button" className="rounded-full bg-red-400 px-3 py-1 text-xs font-semibold text-slate-950" onClick={() => onCommand(node.sensor.macAddress, 'DeEnergise', 'manual-override')}>
            De-energise
          </button>
        </div>
      )}
    </article>
  )
}
