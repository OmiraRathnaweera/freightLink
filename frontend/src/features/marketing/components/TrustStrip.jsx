import { Anchor, Factory, Wheat } from 'lucide-react'

// Placeholder network logos, per the Stitch source screen — swap for real
// partner marks once FreightLink has named launch partners.
const NETWORK_LOGOS = [
  { icon: Anchor, label: 'Colombo Ports Co.' },
  { icon: Factory, label: 'Lanka Manufacturing' },
  { icon: Wheat, label: 'Agro Freight LK' },
]

// The track renders NETWORK_LOGOS twice back-to-back; animate-marquee
// (src/index.css) shifts it exactly -50% (one copy's width) on a loop, so
// the seam is invisible. Respects prefers-reduced-motion automatically.
const MARQUEE_LOGOS = [...NETWORK_LOGOS, ...NETWORK_LOGOS]

function TrustStrip() {
  return (
    <section id="network" className="border-y border-slate-border bg-surface-container-lowest py-8">
      <div className="mx-auto flex max-w-7xl flex-col items-center gap-6 px-container-margin opacity-70 md:flex-row md:justify-between">
        <span className="shrink-0 text-center text-label-caps text-secondary md:text-left">
          TRUSTED BY LEADING SRI LANKAN LOGISTICS NETWORKS
        </span>
        <div
          className="w-full overflow-hidden md:w-auto md:flex-1"
          style={{
            maskImage: 'linear-gradient(to right, transparent, black 10%, black 90%, transparent)',
            WebkitMaskImage: 'linear-gradient(to right, transparent, black 10%, black 90%, transparent)',
          }}
        >
          <div className="flex w-max animate-marquee gap-12 hover:[animation-play-state:paused]">
            {MARQUEE_LOGOS.map(({ icon: Icon, label }, index) => (
              <div
                key={`${label}-${index}`}
                className="flex shrink-0 items-center gap-2 text-headline-md font-bold text-secondary"
              >
                <Icon className="h-5 w-5" strokeWidth={1.5} />
                {label}
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  )
}

export default TrustStrip
