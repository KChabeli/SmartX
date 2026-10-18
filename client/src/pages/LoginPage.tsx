import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from '../lib/auth'

export function LoginPage() {
  const { session, login } = useAuth()
  const [mac, setMac] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  if (session) {
    return <Navigate to="/" replace />
  }

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      await login(mac)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sign-in failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="login-shell">
      <form className="login-card" onSubmit={onSubmit}>
        <p className="mono text-xs uppercase tracking-[0.35em] text-emerald-300">Smart-X operator gate</p>
        <h1 className="hero-title mt-3">Identify with a node MAC</h1>
        <p className="mt-3 text-sm text-slate-400">
          Authentication is a presence check only. The field must not be empty — there is no colon, hex or length format rule.
          The MAC is looked up in the live device dictionary.
        </p>
        <label className="mt-5 block text-sm">
          <span className="text-slate-400">MAC address</span>
          <input
            className="mt-1 w-full rounded-xl border border-white/10 bg-slate-950 px-3 py-3 mono"
            value={mac}
            onChange={(e) => setMac(e.target.value)}
            placeholder="e.g. AA:7C:3F:10:00:01 or any registered id"
            autoComplete="off"
          />
        </label>
        <button className="mt-4 w-full rounded-full bg-emerald-400 px-4 py-3 text-sm font-semibold text-slate-950" type="submit" disabled={busy}>
          {busy ? 'Checking…' : 'Enter gateway'}
        </button>
        {error && <p className="mt-3 text-sm text-red-300">{error}</p>}
        <p className="mt-4 text-xs text-slate-500">Demo node: AA:7C:3F:10:00:01 (Bed A moisture). Empty MAC is rejected; format is not validated.</p>
      </form>
    </div>
  )
}
