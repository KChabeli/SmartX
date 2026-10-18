import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Shell } from './components/Shell'
import { GatewayProvider } from './lib/gatewayContext'
import { CommandStreamPage } from './pages/CommandStreamPage'
import { DashboardPage } from './pages/DashboardPage'
import { LandingPage } from './pages/LandingPage'
import { MeshPlaceholderPage } from './pages/MeshPlaceholderPage'
import { SensorsPage } from './pages/SensorsPage'
import { ToolsPage } from './pages/ToolsPage'

export default function App() {
  return (
    <BrowserRouter>
      <GatewayProvider>
        <Routes>
          <Route element={<Shell />}>
            <Route path="/" element={<LandingPage />} />
            <Route path="/telemetry" element={<DashboardPage />} />
            <Route path="/commands" element={<CommandStreamPage />} />
            <Route path="/mesh" element={<MeshPlaceholderPage />} />
            <Route path="/sensors" element={<SensorsPage />} />
            <Route path="/tools" element={<ToolsPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      </GatewayProvider>
    </BrowserRouter>
  )
}
