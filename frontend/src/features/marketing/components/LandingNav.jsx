import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Truck, Menu, X } from 'lucide-react'
import Button from '../../../components/Button.jsx'

const NAV_LINKS = [
  { href: '#how-it-works', label: 'How it Works' },
  { href: '#network', label: 'Our Network' },
]

// Fixed top nav for the public landing page only — distinct from
// DashboardLayout's sidebar chrome, which never applies here since this
// page renders outside <DashboardLayout>.
//
// Below `md`, the inline link row is replaced by a hamburger-triggered
// panel (DESIGN.md mobile breakpoint: <640px single column, 44px min touch
// targets) — menu state is local UI state (useState), not Redux/Context,
// per ADR-005: it's scoped to this one component subtree.
function LandingNav() {
  const [isOpen, setIsOpen] = useState(false)

  return (
    <header className="fixed left-0 top-0 z-50 w-full border-b border-slate-border bg-surface-container-lowest">
      <div className="mx-auto flex h-16 max-w-[1280px] items-center justify-between px-container-margin">
        <Link to="/" className="flex items-center gap-2" onClick={() => setIsOpen(false)}>
          <Truck className="h-6 w-6 text-primary" strokeWidth={1.5} />
          <span className="text-headline-md font-bold text-primary">FreightLink</span>
        </Link>

        <div className="hidden items-center gap-6 md:flex">
          {NAV_LINKS.map(({ href, label }) => (
            <a key={href} href={href} className="text-body-md text-secondary transition-colors hover:text-primary">
              {label}
            </a>
          ))}
          <Button as={Link} to="/login" variant="primary">
            Sign In
          </Button>
        </div>

        <button
          type="button"
          onClick={() => setIsOpen((prev) => !prev)}
          aria-expanded={isOpen}
          aria-controls="landing-mobile-menu"
          aria-label={isOpen ? 'Close menu' : 'Open menu'}
          className="flex h-11 w-11 items-center justify-center text-primary md:hidden"
        >
          {isOpen ? <X className="h-6 w-6" strokeWidth={1.5} /> : <Menu className="h-6 w-6" strokeWidth={1.5} />}
        </button>
      </div>

      {isOpen && (
        <div
          id="landing-mobile-menu"
          className="flex flex-col gap-1 border-t border-slate-border bg-surface-container-lowest px-container-margin py-4 md:hidden"
        >
          {NAV_LINKS.map(({ href, label }) => (
            <a
              key={href}
              href={href}
              onClick={() => setIsOpen(false)}
              className="flex min-h-11 items-center text-body-md text-secondary transition-colors hover:text-primary"
            >
              {label}
            </a>
          ))}
          <Button as={Link} to="/login" variant="primary" onClick={() => setIsOpen(false)} className="mt-2 w-full">
            Sign In
          </Button>
        </div>
      )}
    </header>
  )
}

export default LandingNav
