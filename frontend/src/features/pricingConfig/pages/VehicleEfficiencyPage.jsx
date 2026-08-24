import PageHeader from '../../../components/PageHeader.jsx'
import VehicleEfficiencySection from '../components/VehicleEfficiencySection.jsx'

// Admin-only screen (see src/features/auth/lib/roleAccess.js) — one of two
// Pricing Configuration sub-pages (ADR-019), the other being
// FuelRatesPage.jsx. GET/POST/DELETE /api/v1/admin/pricing/vehicle-efficiency.
function VehicleEfficiencyPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="ADMIN / PRICING CONFIG"
        title="Vehicle Class Efficiency"
        description="Manage current and historical fuel-consumption tiers per vehicle class."
      />
      <VehicleEfficiencySection />
    </div>
  )
}

export default VehicleEfficiencyPage
