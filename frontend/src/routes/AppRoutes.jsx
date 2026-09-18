import { createBrowserRouter, createRoutesFromElements, Navigate, Route } from 'react-router-dom'
import DashboardLayout from '../layouts/DashboardLayout.jsx'
import ProtectedRoute from './ProtectedRoute.jsx'
import PublicRoute from './PublicRoute.jsx'

import LandingPage from '../features/marketing/pages/LandingPage.jsx'
import LoginPage from '../features/auth/pages/LoginPage.jsx'
import RegisterPage from '../features/auth/pages/RegisterPage.jsx'
import UnauthorizedPage from '../features/auth/pages/UnauthorizedPage.jsx'
import LoadsPage from '../features/loads/pages/LoadsPage.jsx'
import PostLoadPage from '../features/loads/pages/PostLoadPage.jsx'
import LoadDetailPage from '../features/loads/pages/LoadDetailPage.jsx'
import EditLoadPage from '../features/loads/pages/EditLoadPage.jsx'
import AgenciesPage from '../features/agencies/pages/AgenciesPage.jsx'
import AgencyVerificationPage from '../features/agencies/pages/AgencyVerificationPage.jsx'
import TripsPage from '../features/trips/pages/TripsPage.jsx'
import TripDetailPage from '../features/trips/pages/TripDetailPage.jsx'
import BillingPage from '../features/billing/pages/BillingPage.jsx'
import AgentWorkflowConsolePage from '../features/agent-workflows/pages/AgentWorkflowConsolePage.jsx'
import FuelRatesPage from '../features/pricingConfig/pages/FuelRatesPage.jsx'
import VehicleEfficiencyPage from '../features/pricingConfig/pages/VehicleEfficiencyPage.jsx'
import PricingFormulaPage from '../features/pricingConfig/pages/PricingFormulaPage.jsx'

// Still createBrowserRouter + RouterProvider (the data-router API, wired up
// in App.jsx), tree written as JSX <Route> elements via createRoutesFromElements.
//
// A single <ProtectedRoute /> wraps the whole DashboardLayout subtree â€” it
// checks isAuthenticated AND, per-request, looks up whether the current
// URL is allowed for the signed-in role via
// src/features/auth/lib/roleAccess.js's ROLE_ALLOWED_PREFIXES map. There's
// no per-route-group role wrapper to maintain here: a new feature route
// just gets added below, and its role access is granted by adding its
// path prefix to that map â€” nothing in this file changes.
const router = createBrowserRouter(
  createRoutesFromElements(
    <>
      <Route path="/" element={<LandingPage />} />

      {/* --- Public-only routes: bounced to role home if already signed in --- */}
      <Route element={<PublicRoute />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
      </Route>

      {/* --- Everything below requires a signed-in session, role-checked by path --- */}
      <Route element={<ProtectedRoute />}>
        <Route element={<DashboardLayout />}>
          <Route path="/unauthorized" element={<UnauthorizedPage />} />

          <Route path="/loads" element={<LoadsPage />} />
          <Route path="/loads/new" element={<PostLoadPage />} />
          <Route path="/loads/:loadId" element={<LoadDetailPage />} />
          <Route path="/loads/:loadId/edit" element={<EditLoadPage />} />

          <Route path="/agencies" element={<AgenciesPage />} />
          <Route path="/agencies/verification" element={<AgencyVerificationPage />} />
          <Route path="/trips" element={<TripsPage />} />
          <Route path="/trips/:tripId" element={<TripDetailPage />} />
          <Route path="/billing" element={<BillingPage />} />
          <Route path="/agent-workflows" element={<AgentWorkflowConsolePage />} />

          <Route path="/pricing-config" element={<Navigate to="/pricing-config/fuel-rates" replace />} />
          <Route path="/pricing-config/fuel-rates" element={<FuelRatesPage />} />
          <Route path="/pricing-config/vehicle-efficiency" element={<VehicleEfficiencyPage />} />
          <Route path="/pricing-config/formula" element={<PricingFormulaPage />} />
        </Route>
      </Route>
    </>,
  ),
)

export default router

