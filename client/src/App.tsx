import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Shell } from './components/Shell'
import { DashboardPage } from './pages/DashboardPage'
import { LandingPage } from './pages/LandingPage'
import { SensorsPage } from './pages/SensorsPage'
import { ToolsPage } from './pages/ToolsPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Shell />}>
          <Route path="/" element={<LandingPage />} />
          <Route path="/telemetry" element={<DashboardPage />} />
          <Route path="/sensors" element={<SensorsPage />} />
          <Route path="/tools" element={<ToolsPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
