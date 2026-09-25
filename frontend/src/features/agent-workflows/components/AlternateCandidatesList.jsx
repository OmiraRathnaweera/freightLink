import { MapPin, Clock, ChevronRight, Check } from 'lucide-react'

export default function AlternateCandidatesList({
  candidates = [],
  selectedAgencyId,
  onSelectAgency,
  isMatched,
}) {
  if (!candidates || candidates.length === 0) {
    return null
  }

  return (
    <div className="rounded-lg border border-slate-border bg-surface-container-lowest p-5 shadow-soft">
      <div className="mb-4 border-b border-slate-border pb-3">
        <h3 className="font-heading text-title-md font-semibold text-primary">
          Ranked Candidate Carriers (#2 – #5)
        </h3>
        <p className="text-body-sm text-on-surface-variant">
          Alternative carriers evaluated by Agent 3 during multi-criteria ranking. You may choose an alternative to override the AI recommendation.
        </p>
      </div>

      <div className="space-y-3">
        {candidates.map((candidate) => {
          const isSelected = selectedAgencyId === candidate.agencyId

          return (
            <div
              key={candidate.agencyId}
              id={`candidate-row-${candidate.agencyId}`}
              data-testid={`candidate-row-${candidate.agencyId}`}
              className={`flex flex-wrap items-center justify-between gap-4 rounded-lg border p-4 transition-all ${
                isSelected
                  ? 'border-primary bg-primary-fixed/20 shadow-sm'
                  : 'border-slate-border bg-surface-container-lowest hover:bg-slate-50'
              }`}
            >
              {/* Left Info */}
              <div className="flex items-center gap-3">
                <div
                  className={`flex h-8 w-8 items-center justify-center rounded-full font-mono text-xs font-bold ${
                    isSelected
                      ? 'bg-primary text-white'
                      : 'bg-surface-container-high text-on-surface-variant'
                  }`}
                >
                  #{candidate.rank || 2}
                </div>

                <div className="space-y-0.5">
                  <div className="flex items-center gap-2">
                    <h4 className="font-heading text-sm font-bold text-on-surface">
                      {candidate.name}
                    </h4>
                    {candidate.eligible ? (
                      <span className="rounded bg-status-green-bg px-2 py-0.5 text-[10px] font-semibold text-status-green-text">
                        Eligible
                      </span>
                    ) : (
                      <span className="rounded bg-status-red-bg px-2 py-0.5 text-[10px] font-semibold text-status-red-text">
                        Ineligible
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-1.5 text-xs text-on-surface-variant">
                    <MapPin className="h-3.5 w-3.5 text-slate-400" />
                    <span>{candidate.yardAddress || 'Carrier Logistics Yard'}</span>
                  </div>
                </div>
              </div>

              {/* Right Metrics & Selection Button */}
              <div className="flex items-center gap-4">
                <div className="text-right">
                  <div className="flex items-center gap-1 text-xs text-on-surface-variant">
                    <Clock className="h-3.5 w-3.5 text-secondary" />
                    <span className="font-mono font-medium">
                      {candidate.positioningEtaMinutes != null
                        ? `${candidate.positioningEtaMinutes} min ETA`
                        : '—'}
                    </span>
                  </div>
                  <p className="font-mono text-[11px] text-slate-500">
                    {candidate.positioningDistanceKm != null
                      ? `${candidate.positioningDistanceKm.toFixed(1)} km positioning`
                      : '—'}
                  </p>
                </div>

                {!isMatched && (
                  <button
                    id={`select-carrier-${candidate.agencyId}`}
                    data-testid={`select-carrier-${candidate.agencyId}`}
                    type="button"
                    onClick={() => onSelectAgency(candidate.agencyId)}
                    disabled={!candidate.eligible}
                    className={`inline-flex items-center gap-1 rounded-md px-3 py-1.5 text-xs font-semibold transition-colors ${
                      isSelected
                        ? 'bg-primary text-white shadow-sm'
                        : 'border border-slate-300 bg-white text-primary hover:bg-slate-100 disabled:opacity-40'
                    }`}
                  >
                    {isSelected ? (
                      <>
                        <Check className="h-3.5 w-3.5" />
                        Selected
                      </>
                    ) : (
                      <>
                        Choose This
                        <ChevronRight className="h-3.5 w-3.5" />
                      </>
                    )}
                  </button>
                )}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
