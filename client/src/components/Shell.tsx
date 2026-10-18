import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../lib/auth'

const links = [
  { to: '/', label: 'Gateway' },
  { to: '/telemetry', label: 'Telemetry' },
  { to: '/commands', label: 'Command stream' },
  { to: '/mesh', label: 'Mesh (locked)' },
  { to: '/sensors', label: 'Sensors' },
  { to: '/tools', label: 'Operators & Tree' },
]

export function Shell() {
  const { session, logout } = useAuth()

  return (
    <div className="min-h-screen">
      <header className="site-header">
        <div className="site-header-inner">
          <NavLink to="/" className="flex items-center gap-3">
            <span className="grid h-9 w-9 place-items-center rounded-lg bg-emerald-400/20 text-emerald-300 mono text-xs">SX</span>
            <div>
              <p className="text-sm font-semibold tracking-wide">SMART-X MESH GATEWAY</p>
              <p className="text-[11px] text-slate-400">Part 2 · Real-time processing &amp; device commands</p>
            </div>
          </NavLink>
          <nav className="site-nav">
            {links.map((link) => (
              <NavLink
                key={link.to}
                to={link.to}
                end={link.to === '/'}
                className={({ isActive }) => `rounded-full px-3 py-1.5 whitespace-nowrap ${isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white'}`}
              >
                {link.label}
              </NavLink>
            ))}
          </nav>
          <div className="session-chip">
            <span className="mono text-[11px] text-emerald-300">{session?.macAddress}</span>
            <button type="button" className="rounded-full border border-white/15 px-3 py-1 text-xs text-slate-300" onClick={() => logout()}>
              Sign out
            </button>
          </div>
        </div>
      </header>
      <main className="site-main">
        <Outlet />
      </main>
    </div>
  )
}
