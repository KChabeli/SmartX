import { useEffect, useState, type FormEvent } from 'react'
import { gatewayApi } from '../lib/api'
import type { SensorDto } from '../lib/types'

const empty = {
  macAddress: 'AA:7C:3F:99:00:01',
  displayName: 'Custom probe',
  location: 'Facility A / Zone 1 / Sub-Zone B',
  category: 'Environmental',
  metric: 'temperature',
  unit: '°C',
  valueKind: 'Float',
  warningLow: 18,
  warningHigh: 28,
  criticalLow: 10,
  criticalHigh: 36,
}

export function SensorsPage() {
  const [sensors, setSensors] = useState<SensorDto[]>([])
  const [form, setForm] = useState(empty)
  const [message, setMessage] = useState('')
  const [fileMac, setFileMac] = useState('')

  const refresh = () => gatewayApi.sensors().then(setSensors)

  useEffect(() => {
    refresh().catch((err: Error) => setMessage(err.message))
  }, [])

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault()
    try {
      await gatewayApi.registerSensor(form)
      setMessage(`Registered ${form.macAddress}`)
      await refresh()
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Register failed')
    }
  }

  const onUpload = async (file: File | undefined) => {
    if (!file || !fileMac) {
      setMessage('Choose a sensor and a file first.')
      return
    }
    try {
      await gatewayApi.upload(fileMac, file)
      setMessage(`Attached ${file.name} to ${fileMac}`)
      await refresh()
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Upload failed')
    }
  }

  return (
    <div className="grid gap-6 lg:grid-cols-[0.9fr_1.1fr]">
      <form onSubmit={onSubmit} className="space-y-3 rounded-2xl border border-white/10 bg-white/5 p-5">
        <h1 className="text-xl font-semibold">Register ESP32 node</h1>
        {Object.entries({
          macAddress: 'MAC / unique ID',
          displayName: 'Display name',
          location: 'Deployment location',
          metric: 'Metric',
          unit: 'Unit',
        }).map(([key, label]) => (
          <label key={key} className="block text-sm">
            <span className="text-slate-400">{label}</span>
            <input
              className="mt-1 w-full rounded-lg border border-white/10 bg-slate-950 px-3 py-2"
              value={String(form[key as keyof typeof form])}
              onChange={(e) => setForm({ ...form, [key]: e.target.value })}
            />
          </label>
        ))}
        <label className="block text-sm">
          <span className="text-slate-400">Category</span>
          <select className="mt-1 w-full rounded-lg border border-white/10 bg-slate-950 px-3 py-2" value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })}>
            <option>Environmental</option>
            <option>PowerConsumption</option>
            <option>Actuator</option>
          </select>
        </label>
        <label className="block text-sm">
          <span className="text-slate-400">Payload type</span>
          <select className="mt-1 w-full rounded-lg border border-white/10 bg-slate-950 px-3 py-2" value={form.valueKind} onChange={(e) => setForm({ ...form, valueKind: e.target.value })}>
            <option>Float</option>
            <option>Integer</option>
            <option>Boolean</option>
          </select>
        </label>
        <button className="rounded-full bg-emerald-400 px-4 py-2 text-sm font-semibold text-slate-950" type="submit">Save sensor profile</button>
        {message && <p className="text-sm text-sky-300">{message}</p>}
      </form>

      <div className="space-y-4">
        <section className="rounded-2xl border border-white/10 bg-white/5 p-5">
          <h2 className="font-semibold">Media / log attachment</h2>
          <p className="text-sm text-slate-400">Multipart upload of config files, deployment photos or hardware logs onto a sensor profile.</p>
          <select className="mt-3 w-full rounded-lg border border-white/10 bg-slate-950 px-3 py-2" value={fileMac} onChange={(e) => setFileMac(e.target.value)}>
            <option value="">Select sensor MAC</option>
            {sensors.map((s) => <option key={s.macAddress} value={s.macAddress}>{s.displayName} · {s.macAddress}</option>)}
          </select>
          <input className="mt-3 block text-sm" type="file" onChange={(e) => onUpload(e.target.files?.[0])} />
        </section>
        <section className="rounded-2xl border border-white/10 bg-white/5 p-5">
          <h2 className="font-semibold">Registered mesh ({sensors.length})</h2>
          <ul className="mt-3 max-h-[28rem] space-y-2 overflow-auto text-sm">
            {sensors.map((s) => (
              <li key={s.id} className="rounded-xl border border-white/10 p-3">
                <p className="font-medium">{s.displayName}</p>
                <p className="mono text-xs text-slate-400">{s.macAddress} · {s.category} · {s.location}</p>
                {s.attachments.length > 0 && (
                  <p className="mt-1 text-xs text-emerald-300">{s.attachments.map((a) => a.fileName).join(', ')}</p>
                )}
              </li>
            ))}
          </ul>
        </section>
      </div>
    </div>
  )
}
