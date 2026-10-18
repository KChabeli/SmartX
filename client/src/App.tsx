import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { Shell } from './components/Shell'
import { AuthProvider, useAuth } from './lib/auth'
import { GatewayProvider } from './lib/gatewayContext'
import { CommandStreamPage } from './pages/CommandStreamPage'
import { DashboardPage } from './pages/DashboardPage'
import { LandingPage } from './pages/LandingPage'
import { LoginPage } from './pages/LoginPage'
import { MeshPlaceholderPage } from './pages/MeshPlaceholderPage'
import { SensorsPage } from './pages/SensorsPage'
import { ToolsPage } from './pages/ToolsPage'

function RequireAuth() {
  const { session } = useAuth()
  if (!session) {
    return <Navigate to="/login" replace />
  }
  return <Outlet />
}

function GatewayLayout() {
  return (
    <GatewayProvider>
      <Shell />
    </GatewayProvider>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<RequireAuth />}>
            <Route element={<GatewayLayout />}>
              <Route path="/" element={<LandingPage />} />
              <Route path="/telemetry" element={<DashboardPage />} />
              <Route path="/commands" element={<CommandStreamPage />} />
              <Route path="/mesh" element={<MeshPlaceholderPage />} />
              <Route path="/sensors" element={<SensorsPage />} />
              <Route path="/tools" element={<ToolsPage />} />
              <Route path="*" element={<Navigate to="/" replace />} />
            </Route>
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
