import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'
import { gatewayApi } from './api'

export type Session = {
  token: string
  macAddress: string
  displayName: string
}

const storageKey = 'smartx-session'

const AuthContext = createContext<{
  session: Session | null
  login: (macAddress: string) => Promise<void>
  logout: () => Promise<void>
} | null>(null)

function readStored(): Session | null {
  const raw = sessionStorage.getItem(storageKey)
  if (!raw) return null
  try {
    return JSON.parse(raw) as Session
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => readStored())

  const value = useMemo(() => ({
    session,
    login: async (macAddress: string) => {
      const next = await gatewayApi.login(macAddress)
      const stored = { token: next.token, macAddress: next.macAddress, displayName: next.displayName }
      sessionStorage.setItem(storageKey, JSON.stringify(stored))
      setSession(stored)
    },
    logout: async () => {
      await gatewayApi.logout().catch(() => undefined)
      sessionStorage.removeItem(storageKey)
      setSession(null)
    },
  }), [session])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}

export function readToken() {
  return readStored()?.token ?? null
}
