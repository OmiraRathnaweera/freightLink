import { Truck } from 'lucide-react'

const FOOTER_LINKS = [
  {
    heading: 'PLATFORM',
    links: ['Shipper Portal', 'Agency Console', 'API Documentation'],
  },
  {
    heading: 'LEGAL & SUPPORT',
    links: ['Terms of Service', 'Privacy Policy', 'System Status'],
  },
]

function LandingFooter() {
  return (
    <footer className="mt-20 border-t border-primary bg-primary-container px-container-margin py-12 text-on-primary-container">
      <div className="mx-auto grid max-w-[1280px] grid-cols-1 gap-8 md:grid-cols-4">
        <div className="col-span-1 md:col-span-2">
          <div className="mb-4 flex items-center gap-2">
            <Truck className="h-6 w-6 text-inverse-primary" strokeWidth={1.5} />
            <span className="text-headline-md font-bold text-inverse-primary">FreightLink LK</span>
          </div>
          <p className="max-w-sm text-body-md text-primary-fixed-dim">
            Industrial logistics infrastructure for Sri Lanka. Engineered for precision, built for scale.
          </p>
        </div>

        {FOOTER_LINKS.map(({ heading, links }) => (
          <div key={heading}>
            <h4 className="mb-4 text-label-caps text-inverse-primary">{heading}</h4>
            <ul className="space-y-2 text-body-md text-primary-fixed-dim">
              {links.map((label) => (
                <li key={label}>
                  <a href="#" className="transition-colors hover:text-inverse-primary">
                    {label}
                  </a>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

      <div className="mx-auto mt-12 max-w-[1280px] border-t border-surface-tint pt-8 text-center text-data-mono text-primary-fixed-dim">
        © 2026 FreightLink LK Systems. All operations logged.
      </div>
    </footer>
  )
}

export default LandingFooter
