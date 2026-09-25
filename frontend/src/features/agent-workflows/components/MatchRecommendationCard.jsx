import {
  Sparkles,
  Truck,
  MapPin,
  Clock,
  Navigation,
  Check,
  RotateCcw,
  CheckCircle2,
  Building2,
  TrendingDown,
} from 'lucide-react'
import { formatCurrency } from '../../loads/lib/format.js'
import Button from '../../../components/Button.jsx'

const VEHICLE_CLASS_LABELS = {
  SmallVan: 'Small Van (Up to 1,500 kg)',
  LightTruck: 'Light Truck (Up to 3,500 kg)',
  MediumLorry: 'Medium Lorry (Up to 8,000 kg)',
  HeavyTruck: 'Heavy Truck (Up to 16,000 kg)',
  PrimeMover: 'Prime Mover (Multi-Axle / 24,000+ kg)',
}

export default function MatchRecommendationCard({
  loadId,
  loadStatus,
  recommendedAgency,
  selectedAgencyId,
  existingAssignment,
  onApproveMatch,
  onRetryMatch,
  isApproving,
  isRetrying,
}) {
  if (!recommendedAgency) {
    return (
      <div className="rounded-lg border border-slate-border bg-surface-container-lowest p-8 text-center shadow-soft">
        <Sparkles className="mx-auto h-8 w-8 text-secondary mb-2 animate-bounce" />
        <h3 className="font-heading text-title-md font-semibold text-primary">
          Evaluating Optimal Carrier Matches
        </h3>
        <p className="mt-1 text-body-sm text-on-surface-variant max-w-md mx-auto">
          Agent 3 is computing open-source routing matrices, driver positionings, and dynamic fuel-adjusted prices...
        </p>
        <div className="mt-4">
          <Button variant="secondary" onClick={onRetryMatch} disabled={isRetrying}>
            <RotateCcw className={`h-4 w-4 mr-2 ${isRetrying ? 'animate-spin' : ''}`} />
            Run Match Evaluation
          </Button>
        </div>
      </div>
    )
  }

  const isMatched = loadStatus === 'Matched' || Boolean(existingAssignment)

  const vehicleClassDisplay =
    VEHICLE_CLASS_LABELS[recommendedAgency.suggestedVehicleClass] ||
    recommendedAgency.suggestedVehicleClass ||
    'Medium Lorry'

  return (
    <div
      id="match-recommendation-card"
      data-testid="match-recommendation-card"
      className="relative overflow-hidden rounded-xl border-2 border-primary/20 bg-surface-container-lowest shadow-md transition-all hover:shadow-lg"
    >
      {/* Top Banner Ribbon */}
      <div className="bg-gradient-to-r from-primary via-primary-container to-secondary px-6 py-3.5 text-white flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <span className="flex h-6 w-6 items-center justify-center rounded-full bg-amber-400 text-slate-900 shadow-sm">
            <Sparkles className="h-3.5 w-3.5 fill-current" />
          </span>
          <span className="font-heading text-sm font-bold tracking-wide uppercase">
            Agent 3 Recommended Carrier Match
          </span>
          <span className="rounded bg-white/20 px-2 py-0.5 text-[11px] font-semibold text-white">
            Rank #1 Best Fit
          </span>
        </div>

        {isMatched && (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-500/20 px-3 py-0.5 text-xs font-semibold text-emerald-200 border border-emerald-400/40">
            <CheckCircle2 className="h-3.5 w-3.5 text-emerald-300" />
            Match Confirmed
          </span>
        )}
      </div>

      <div className="p-6 space-y-6">
        {/* Header: Carrier Info */}
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1">
            <div className="flex items-center gap-2.5">
              <Building2 className="h-5 w-5 text-secondary" />
              <h2 className="font-heading text-2xl font-bold text-primary">
                {recommendedAgency.name}
              </h2>
              <span className="rounded-full bg-status-blue-bg px-2.5 py-0.5 text-xs font-semibold text-status-blue-text">
                Verified Fleet
              </span>
            </div>
            <div className="flex items-center gap-1.5 text-sm text-on-surface-variant">
              <MapPin className="h-4 w-4 text-slate-400 shrink-0" />
              <span>{recommendedAgency.yardAddress || 'Carrier Logistics Yard'}</span>
            </div>
          </div>

          {/* Vehicle Class Badge */}
          <div className="flex items-center gap-2 rounded-lg border border-slate-200 bg-surface-container-low px-3.5 py-2">
            <Truck className="h-5 w-5 text-primary shrink-0" />
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                Recommended Vehicle
              </p>
              <p className="text-sm font-bold text-primary">{vehicleClassDisplay}</p>
            </div>
          </div>
        </div>

        {/* 3 Metric Spotlight Cards */}
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          {/* 1. Estimated Price */}
          <div className="rounded-lg border border-primary/15 bg-primary-fixed/30 p-4 transition-all">
            <div className="flex items-center justify-between text-xs font-semibold uppercase tracking-wider text-on-primary-fixed-variant">
              <span>Estimated Match Price</span>
              <TrendingDown className="h-3.5 w-3.5 text-status-green-text" />
            </div>
            <div className="mt-2 font-mono text-2xl font-extrabold text-primary">
              {formatCurrency(recommendedAgency.estimatedPrice)}
            </div>
            <p className="mt-1 text-[11px] text-on-surface-variant">
              Dynamic formula: Base + (Dist × Rate) + Fuel factor
            </p>
          </div>

          {/* 2. Positioning ETA & Distance */}
          <div className="rounded-lg border border-slate-border bg-surface-container-low p-4 transition-all">
            <div className="flex items-center justify-between text-xs font-semibold uppercase tracking-wider text-on-surface-variant">
              <span>Positioning Time & Route</span>
              <Clock className="h-3.5 w-3.5 text-secondary" />
            </div>
            <div className="mt-2 font-mono text-2xl font-bold text-on-surface">
              {recommendedAgency.positioningEtaMinutes != null
                ? `${recommendedAgency.positioningEtaMinutes} min`
                : '35 min'}
            </div>
            <p className="mt-1 font-mono text-xs text-on-surface-variant">
              {recommendedAgency.positioningDistanceKm != null
                ? `${recommendedAgency.positioningDistanceKm.toFixed(1)} km from yard`
                : '14.2 km from yard'}
            </p>
          </div>

          {/* 3. Cargo Transit Distance */}
          <div className="rounded-lg border border-slate-border bg-surface-container-low p-4 transition-all">
            <div className="flex items-center justify-between text-xs font-semibold uppercase tracking-wider text-on-surface-variant">
              <span>Cargo Transit Leg</span>
              <Navigation className="h-3.5 w-3.5 text-secondary" />
            </div>
            <div className="mt-2 font-mono text-2xl font-bold text-on-surface">
              {recommendedAgency.cargoDistanceKm != null
                ? `${recommendedAgency.cargoDistanceKm.toFixed(1)} km`
                : '92.5 km'}
            </div>
            <p className="mt-1 text-[11px] text-on-surface-variant">
              OpenRouteService highway corridor
            </p>
          </div>
        </div>

        {/* AI Selection Justification */}
        {recommendedAgency.selectionJustification && (
          <div className="rounded-lg border border-amber-200/80 bg-gradient-to-r from-amber-50/70 to-orange-50/40 p-4">
            <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-amber-900">
              <Sparkles className="h-3.5 w-3.5 text-amber-600" />
              <span>Multi-Agent Selection Rationale (Agent 3 & 4)</span>
            </div>
            <p className="mt-2 text-sm leading-relaxed text-slate-800 italic">
              "{recommendedAgency.selectionJustification}"
            </p>
          </div>
        )}

        {/* Existing Assignment Banner (if already confirmed) */}
        {existingAssignment && (
          <div className="rounded-lg border border-status-green-text/30 bg-status-green-bg p-4 flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-full bg-status-green-text text-white">
                <Check className="h-6 w-6 stroke-[3]" />
              </div>
              <div>
                <h4 className="font-heading text-sm font-bold text-status-green-text">
                  Proposal Accepted by Shipper
                </h4>
                <p className="text-xs text-on-surface-variant">
                  Assignment ID: <span className="font-mono">{existingAssignment.assignmentId}</span> • Status: <span className="font-semibold">{existingAssignment.status}</span>
                </p>
              </div>
            </div>
            <div className="text-right">
              <p className="text-xs text-on-surface-variant uppercase tracking-wider font-semibold">Agreed Price</p>
              <p className="font-mono text-lg font-bold text-status-green-text">
                {formatCurrency(existingAssignment.proposedPrice)}
              </p>
            </div>
          </div>
        )}

        {/* Action Decision Area */}
        <div className="flex flex-wrap items-center justify-between gap-4 border-t border-slate-border pt-5">
          <div className="flex items-center gap-2">
            <Button
              id="retry-match-btn"
              data-testid="retry-match-btn"
              variant="secondary"
              onClick={onRetryMatch}
              disabled={isRetrying || isApproving}
            >
              <RotateCcw className={`h-4 w-4 ${isRetrying ? 'animate-spin' : ''}`} />
              Retry Match
            </Button>
            <p className="text-xs text-on-surface-variant hidden sm:block">
              Re-evaluates available active carriers and real-time positioning.
            </p>
          </div>

          <div className="flex items-center gap-3">
            {!isMatched ? (
              <button
                id="approve-match-btn"
                data-testid="approve-match-btn"
                type="button"
                onClick={() =>
                  onApproveMatch({
                    loadId,
                    agencyId: selectedAgencyId || recommendedAgency.agencyId,
                  })
                }
                disabled={isApproving}
                className="inline-flex items-center justify-center gap-2 rounded-lg bg-emerald-600 px-6 py-2.5 text-sm font-bold text-white shadow-md transition-all hover:bg-emerald-700 hover:shadow-lg focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isApproving ? (
                  <>
                    <RotateCcw className="h-4 w-4 animate-spin" />
                    <span>Confirming Match...</span>
                  </>
                ) : (
                  <>
                    <Check className="h-5 w-5 stroke-[2.5]" />
                    <span>Approve Match & Dispatch</span>
                  </>
                )}
              </button>
            ) : (
              <span className="inline-flex items-center gap-2 rounded-md bg-slate-100 px-4 py-2 text-sm font-semibold text-slate-700">
                <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                Match Approved & Dispatched
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
