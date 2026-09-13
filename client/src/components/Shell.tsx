import { NavLink, Outlet } from 'react-router-dom'

const links = [
  { to: '/', label: 'Gateway' },
  { to: '/telemetry', label: 'Telemetry' },
  { to: '/sensors', label: 'Sensors' },
  { to: '/tools', label: 'Operators & Tree' },
]

export function Shell() {
  return (
    <div className="min-h-screen">
      <header className="sticky top-0 z-20 border-b border-white/10 bg-[#07111c]/80 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-5 py-3">
          <NavLink to="/" className="flex items-center gap-3">
            <span className="grid h-9 w-9 place-items-center rounded-lg bg-emerald-400/20 text-emerald-300 mono text-xs">SX</span>
            <div>
              <p className="text-sm font-semibold tracking-wide">SMART-X MESH GATEWAY</p>
              <p className="text-[11px] text-slate-400">Part 1 · Ingestion &amp; colour-coded telemetry</p>
            </div>
          </NavLink>
          <nav className="flex gap-1 text-sm">
            {links.map((link) => (
              <NavLink
                key={link.to}
                to={link.to}
                end={link.to === '/'}
                className={({ isActive }) => `rounded-full px-3 py-1.5 ${isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white'}`}
              >
                {link.label}
              </NavLink>
            ))}
          </nav>
        </div>
      </header>
      <main className="mx-auto max-w-7xl px-5 py-6">
        <Outlet />
      </main>
    </div>
  )
}
