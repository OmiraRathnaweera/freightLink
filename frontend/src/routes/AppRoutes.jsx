import { createBrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import DashboardLayout from '../layouts/DashboardLayout.jsx'

import LoginPage from '../features/auth/pages/LoginPage.jsx'
import RegisterPage from '../features/auth/pages/RegisterPage.jsx'
import LoadsPage from '../features/loads/pages/LoadsPage.jsx'
import AgenciesPage from '../features/agencies/pages/AgenciesPage.jsx'
import TripsPage from '../features/trips/pages/TripsPage.jsx'
import BillingPage from '../features/billing/pages/BillingPage.jsx'
import AgentWorkflowConsolePage from '../features/agent-workflows/pages/AgentWorkflowConsolePage.jsx'

// Still createBrowserRouter + RouterProvider (the data-router API, wired up
// in App.jsx), tree written as JSX <Route> elements via createRoutesFromElements.
//
// The ProtectedRoute guard (src/routes/ProtectedRoute.jsx) is intentionally
// NOT wired in here for now — every feature route below is reachable without
// auth while pages are still placeholders. Re-add it by wrapping the feature
// routes in <Route element={<ProtectedRoute allowedRoles={[...]} />}> once
// login is real and route access needs to be enforced again.
const router = createBrowserRouter(
  
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />

      {/* --- Public routes --- */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      {/* --- Feature routes, behind the shared DashboardLayout shell --- */}
      <Route element={<DashboardLayout />}>
        <Route path="/loads" element={<LoadsPage />} />
        <Route path="/agencies" element={<AgenciesPage />} />
        <Route path="/trips" element={<TripsPage />} />
        <Route path="/billing" element={<BillingPage />} />
        <Route path="/agent-workflows" element={<AgentWorkflowConsolePage />} />
      </Route>
    </Routes>
  
)

export default router
