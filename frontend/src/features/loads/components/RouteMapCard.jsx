import { MapPin } from 'lucide-react'
import Card from '../../../components/Card.jsx'

// Static, schematic origin→destination diagram — this project has no maps
// API key configured (CLAUDE.md: never fabricate env values), so this is
// an illustrative placeholder standing in for Stitch's embedded live map,
// not a real map integration.
function RouteMapCard({ origin, destination, distanceKm, remainingKm }) {
  return (
    <Card className="overflow-hidden p-0">
      <div className="relative flex h-40 items-center justify-between bg-surface-container-low px-8">
        <div className="absolute inset-x-16 top-1/2 -translate-y-1/2 border-t-2 border-dashed border-outline-variant" />
        <div className="relative z-10 flex flex-col items-center gap-1">
          <MapPin className="h-6 w-6 text-primary" strokeWidth={1.5} />
          <span className="text-label-caps text-on-surface-variant">{origin}</span>
        </div>
        <div className="relative z-10 flex flex-col items-center gap-1">
          <MapPin className="h-6 w-6 text-status-green-text" strokeWidth={1.5} />
          <span className="text-label-caps text-on-surface-variant">{destination}</span>
        </div>
      </div>
      {distanceKm != null && (
        <Card.Footer className="flex items-center justify-between">
          <span className="text-body-md text-on-surface-variant">Distance</span>
          <span className="text-data-mono text-on-surface">
            {distanceKm} km{remainingKm != null ? ` · ${remainingKm} km remaining` : ''}
          </span>
        </Card.Footer>
      )}
    </Card>
  )
}

export default RouteMapCard
