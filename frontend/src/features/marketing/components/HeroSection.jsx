import { Link } from 'react-router-dom'
import { Package, Bus, TrendingUp } from 'lucide-react'
import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import landingHero from '../../../assets/landing-hero.jpg'

function HeroSection() {
  return (
    <section className="grid grid-cols-1 items-center gap-12 px-container-margin py-20 lg:grid-cols-2 lg:py-32">
      <div className="space-y-8">
        <div className="space-y-4">
          <h1 className="text-headline-lg text-primary lg:text-[48px] lg:leading-[56px]">
            Industrial-Grade Logistics Execution for Sri Lanka
          </h1>
          <p className="max-w-xl text-body-lg text-secondary">
            A definitive operational platform engineered to synchronize shippers and transport agencies. Minimize
            deadhead kilometers, optimize load matching, and ensure real-time accountability across the island.
          </p>
        </div>

        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <Card className="group cursor-pointer transition-colors hover:bg-surface-container-low">
            <Package className="mb-4 h-8 w-8 text-primary transition-transform group-hover:scale-110" strokeWidth={1.5} />
            <h3 className="mb-2 text-headline-md text-primary">Have a shipment?</h3>
            <p className="mb-4 text-body-md text-secondary">
              Post loads, track transit, and manage invoices securely.
            </p>
            <Button as={Link} to="/register?role=shipper" variant="primary" className="w-full">
              Register as Shipper
            </Button>
          </Card>

          <Card className="group cursor-pointer transition-colors hover:bg-surface-container-low">
            <Bus className="mb-4 h-8 w-8 text-primary transition-transform group-hover:scale-110" strokeWidth={1.5} />
            <h3 className="mb-2 text-headline-md text-primary">Transport Agency?</h3>
            <p className="mb-4 text-body-md text-secondary">
              Access verified loads, manage fleets, and optimize routes.
            </p>
            <Button as={Link} to="/register?role=agency" variant="secondary" className="w-full">
              Join Agency Network
            </Button>
          </Card>
        </div>
      </div>

      <div className="relative h-[400px] overflow-hidden rounded-xl border border-slate-border lg:h-[600px]">
        <img
          src={landingHero}
          alt="A high-density logistics dashboard displayed on a monitor in a modern office, showing shipment metrics, a map of Sri Lanka, and status indicators."
          className="h-full w-full object-cover"
        />
        <div className="absolute inset-x-4 bottom-4 flex items-center justify-between rounded border border-slate-border bg-surface-container-lowest/90 p-4 backdrop-blur">
          <div>
            <div className="mb-1 text-label-caps text-secondary">ACTIVE LOADS (LKR)</div>
            <div className="text-data-mono text-lg text-primary">24,500,000.00</div>
          </div>
          <span className="flex items-center gap-1 rounded bg-status-green-bg px-2 py-1 text-status-badge text-status-green-text">
            <TrendingUp className="h-3.5 w-3.5" strokeWidth={1.5} /> +12.4%
          </span>
        </div>
      </div>
    </section>
  )
}

export default HeroSection
