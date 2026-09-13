import { Link } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { gatewayApi } from '../lib/api'
import type { PillarDto } from '../lib/types'

export function LandingPage() {
  const [pillars, setPillars] = useState<PillarDto[]>([])

  useEffect(() => {
    gatewayApi.pillars().then(setPillars).catch(() => {
      setPillars([
        { id: 'ingestion', title: 'Sensor Data Ingestion and Telemetry', summary: 'Live ESP32 telemetry gateway.', enabled: true },
        { id: 'commands', title: 'Real-Time Command Stream and History', summary: 'Actuator command bus.', enabled: false, deferredUntil: 'Part 2' },
        { id: 'mesh', title: 'Network Topology and Mesh Routing', summary: 'Mesh routing visualiser.', enabled: false, deferredUntil: 'Final PoE' },
      ])
    })
  }, [])

  return (
    <div className="space-y-10">
      <section className="rounded-3xl border border-white/10 bg-gradient-to-br from-emerald-400/10 via-transparent to-sky-500/10 p-8">
        <p className="mono text-xs uppercase tracking-[0.35em] text-emerald-300">South African IoT mesh · ESP32 telemetry</p>
        <h1 className="mt-3 max-w-3xl text-4xl font-semibold leading-tight md:text-5xl">Watch the mesh, not the spreadsheet.</h1>
        <p className="mt-4 max-w-2xl text-slate-300">
          Smart-X ingests multi-typed packets from hydroponic farms, estate utility meters and microgrid feeders.
          The console uses semantic colour-coded thresholds plus live SignalR streaming so spikes pop red and silent nodes freeze grey.
        </p>
      </section>

      <section className="grid gap-4 md:grid-cols-3">
        {pillars.map((pillar) => (
          <article key={pillar.id} className={`rounded-2xl border p-5 ${pillar.enabled ? 'border-emerald-400/40 bg-emerald-400/10' : 'border-white/10 bg-white/5 opacity-70'}`}>
            <p className="mono text-[11px] uppercase tracking-widest text-slate-400">{pillar.enabled ? 'Active' : `Locked · ${pillar.deferredUntil}`}</p>
            <h2 className="mt-2 text-xl font-semibold">{pillar.title}</h2>
            <p className="mt-2 text-sm text-slate-400">{pillar.summary}</p>
            {pillar.enabled ? (
              <Link to="/telemetry" className="mt-5 inline-flex rounded-full bg-emerald-400 px-4 py-2 text-sm font-semibold text-slate-950">
                Open telemetry console
              </Link>
            ) : (
              <button type="button" disabled className="mt-5 rounded-full border border-white/10 px-4 py-2 text-sm text-slate-500">
                Disabled for this part
              </button>
            )}
          </article>
        ))}
      </section>
    </div>
  )
}
