import PageHeader from '../../../components/PageHeader.jsx'
import PricingFormulaSection from '../components/PricingFormulaSection.jsx'

// Admin-only screen (see src/features/auth/lib/roleAccess.js) — third
// Pricing Configuration sub-page, alongside FuelRatesPage.jsx and
// VehicleEfficiencyPage.jsx. GET/POST/DELETE /api/v1/admin/pricing/formula-config.
function PricingFormulaPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="ADMIN / PRICING CONFIG"
        title="Pricing Formula"
        description="Manage the current and historical base pricing formula used for load price estimates."
      />
      <PricingFormulaSection />
    </div>
  )
}

export default PricingFormulaPage
