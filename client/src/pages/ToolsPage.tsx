import { useState } from 'react'
import { gatewayApi } from '../lib/api'
import type { DeploymentNodeDto } from '../lib/types'

export function ToolsPage() {
  const [leftWatts, setLeftWatts] = useState(2400)
  const [rightWatts, setRightWatts] = useState(1850)
  const [meterResult, setMeterResult] = useState('')
  const [tree, setTree] = useState<DeploymentNodeDto | null>(null)
  const [validation, setValidation] = useState('')

  const aggregate = async () => {
    const result = await gatewayApi.aggregateMeters({
      leftId: 'Meter1',
      leftWatts,
      leftPf: 0.96,
      rightId: 'Meter2',
      rightWatts,
      rightPf: 0.91,
    })
    setMeterResult(`${result.expression}. Meter1 > Meter2: ${result.leftExceedsRight}`)
  }

  const loadSample = async () => {
    const sample = await gatewayApi.sampleDeployment()
    setTree(sample.tree)
    setValidation(`Safe=${sample.validation.isSafe}. Path=${sample.validation.ancestorChain?.join(' → ')}`)
  }

  const validate = async () => {
    if (!tree) return
    const result = await gatewayApi.validateDeployment(tree, 'AA:7C:3F:10:00:01') as { isSafe: boolean; errors: string[]; ancestorChain?: string[] }
    setValidation(result.isSafe
      ? `Node is safely configured within ${result.ancestorChain?.slice(1).join(' → ')}`
      : result.errors.join('\n'))
  }

  return (
    <div className="tools-grid">
      <section className="rounded-2xl border border-white/10 bg-white/5 p-5">
        <h1 className="text-xl font-semibold">Operator overload · smart meters</h1>
        <p className="mt-2 text-sm text-slate-400">
          Backend uses <code className="mono">Meter3 = Meter1 + Meter2</code> via overloaded <code className="mono">+</code> and comparison operators on <code className="mono">SmartMeterReading</code>.
        </p>
        <div className="mt-4 grid grid-cols-2 gap-3">
          <label className="text-sm">Meter 1 watts
            <input type="number" className="mt-1 w-full rounded-lg border border-white/10 bg-slate-950 px-3 py-2" value={leftWatts} onChange={(e) => setLeftWatts(Number(e.target.value))} />
          </label>
          <label className="text-sm">Meter 2 watts
            <input type="number" className="mt-1 w-full rounded-lg border border-white/10 bg-slate-950 px-3 py-2" value={rightWatts} onChange={(e) => setRightWatts(Number(e.target.value))} />
          </label>
        </div>
        <button type="button" className="mt-4 rounded-full bg-sky-400 px-4 py-2 text-sm font-semibold text-slate-950" onClick={aggregate}>
          Aggregate load
        </button>
        {meterResult && <p className="mono mt-3 text-sm text-sky-200">{meterResult}</p>}
      </section>

      <section className="rounded-2xl border border-white/10 bg-white/5 p-5">
        <h2 className="text-xl font-semibold">Recursive deployment tree</h2>
        <p className="mt-2 text-sm text-slate-400">
          Validates Facility → Zone → Sub-Zone → Node, e.g. Sub-Zone B inside Zone 1 inside Facility A.
        </p>
        <div className="mt-4 flex gap-2">
          <button type="button" className="rounded-full bg-white px-4 py-2 text-sm font-semibold text-slate-950" onClick={loadSample}>Load sample tree</button>
          <button type="button" className="rounded-full border border-white/20 px-4 py-2 text-sm" onClick={validate}>Validate MAC AA:7C:3F:10:00:01</button>
        </div>
        {tree && (
          <pre className="mono mt-4 max-h-72 overflow-auto rounded-xl bg-slate-950 p-3 text-xs text-emerald-200">
            {JSON.stringify(tree, null, 2)}
          </pre>
        )}
        {validation && <p className="mt-3 whitespace-pre-wrap text-sm text-amber-200">{validation}</p>}
      </section>
    </div>
  )
}
