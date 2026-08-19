import PageHeader from '../../../components/PageHeader.jsx'
import FuelRateSection from '../components/FuelRateSection.jsx'

// Admin-only screen (see src/features/auth/lib/roleAccess.js) — one of two
// Pricing Configuration sub-pages (ADR-019), the other being
// VehicleEfficiencyPage.jsx. GET/POST/DELETE /api/v1/admin/pricing/fuel-rates.
function FuelRatesPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="ADMIN / PRICING CONFIG"
        title="Fuel Prices"
        description="Manage current and historical fuel price rates used in load pricing."
      />
      <FuelRateSection />
    </div>
  )
}

export default FuelRatesPage
