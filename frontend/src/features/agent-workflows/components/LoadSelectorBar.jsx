import { Link } from 'react-router-dom'
import { Package, ExternalLink, ArrowRight, ChevronDown, Check } from 'lucide-react'
import StatusBadge from '../../../components/StatusBadge.jsx'
import { getLoadStatusTone } from '../../loads/lib/statusTone.js'
import { formatWeight } from '../../loads/lib/format.js'

export default function LoadSelectorBar({
  currentLoad,
  availableLoads = [],
  onSelectLoad,
  isLoading,
}) {
  return (
    <div className="rounded-lg border border-slate-border bg-surface-container-lowest p-4 shadow-soft">
      <div className="flex flex-wrap items-center justify-between gap-4">
        {/* Current Load Summary */}
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary text-white">
            <Package className="h-5 w-5" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="font-heading text-lg font-bold text-primary">
                {currentLoad?.referenceCode || 'Select a Load'}
              </span>
              {currentLoad?.status && (
                <StatusBadge tone={getLoadStatusTone(currentLoad.status)}>
                  {currentLoad.status}
                </StatusBadge>
              )}
            </div>
            {currentLoad && (
              <p className="text-xs text-on-surface-variant flex items-center gap-1.5 mt-0.5">
                <span>{currentLoad.pickupAddress}</span>
                <ArrowRight className="h-3 w-3 text-slate-400" />
                <span>{currentLoad.dropoffAddress}</span>
                <span className="text-slate-300">•</span>
                <span className="font-mono">{formatWeight(currentLoad.weightKg)}</span>
              </p>
            )}
          </div>
        </div>

        {/* Load Switcher & Detail Link */}
        <div className="flex items-center gap-3">
          {availableLoads.length > 0 && (
            <div className="relative">
              <label htmlFor="load-select" className="sr-only">
                Switch Load
              </label>
              <select
                id="load-select"
                data-testid="load-select"
                value={currentLoad?.loadId || ''}
                onChange={(e) => onSelectLoad(e.target.value)}
                disabled={isLoading}
                aria-label="Switch Active Load"
                className="rounded-md border border-slate-300 bg-white py-1.5 pl-3 pr-8 text-xs font-semibold text-on-surface shadow-sm focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
              >
                <option value="" disabled>
                  Switch active load...
                </option>
                {availableLoads.map((l) => (
                  <option key={l.loadId} value={l.loadId}>
                    {l.referenceCode} — {l.pickupAddress?.split(',')[0]} → {l.dropoffAddress?.split(',')[0]} ({l.status})
                  </option>
                ))}
              </select>
            </div>
          )}

          {currentLoad?.loadId && (
            <Link
              to={`/loads/${currentLoad.loadId}`}
              className="inline-flex items-center gap-1 rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-semibold text-primary hover:bg-slate-50 transition-colors"
            >
              <span>View Full Load</span>
              <ExternalLink className="h-3.5 w-3.5" />
            </Link>
          )}
        </div>
      </div>
    </div>
  )
}
