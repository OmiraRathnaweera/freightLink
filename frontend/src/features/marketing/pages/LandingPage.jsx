import LandingNav from '../components/LandingNav.jsx'
import HeroSection from '../components/HeroSection.jsx'
import TrustStrip from '../components/TrustStrip.jsx'
import WorkflowSection from '../components/WorkflowSection.jsx'
import LandingFooter from '../components/LandingFooter.jsx'

// Public marketing homepage — cloned from the Stitch screen
// "FreightLink — Logistics Matching Platform" (projects/11517759538014673778/
// screens/157c0e52f98f46de97659b81930f9d28). Renders outside DashboardLayout:
// it owns its own fixed nav instead of the dashboard sidebar shell.
function LandingPage() {
  return (
    <div className="min-h-screen bg-background text-on-background">
      <LandingNav />
      <main className="mx-auto max-w-[1280px] pt-16">
        <HeroSection />
        <TrustStrip />
        <WorkflowSection />
      </main>
      <LandingFooter />
    </div>
  )
}

export default LandingPage
