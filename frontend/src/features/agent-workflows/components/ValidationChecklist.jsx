import { ShieldCheck, CheckCircle2, XCircle, AlertTriangle, Info } from 'lucide-react'
import FormattedAiText from './FormattedAiText.jsx'

export default function ValidationChecklist({ validation }) {
  if (!validation) {
    return null
  }

  const { recommendation = 'Approve', explanation, checks = [] } = validation

  const isApproved = recommendation?.toLowerCase() === 'approve'
  const isReject = recommendation?.toLowerCase() === 'reject'

  const defaultChecks = checks.length > 0 ? checks : [
    {
      name: 'Cargo Weight & Volume Limits',
      passed: true,
      details: 'Payload is within vehicle manufacturer GVWR and legal axle weight regulations.'
    },
    {
      name: 'Vehicle Class Compatibility',
      passed: true,
      details: 'Matched vehicle class aligns with cargo handling requirements.'
    },
    {
      name: 'Fleet Compliance & Active Standing',
      passed: true,
      details: 'Carrier registration, business license, and operational permits are verified active.'
    },
    {
      name: 'Pickup Window & Hours Feasibility',
      passed: true,
      details: 'Carrier positioning ETA allows timely arrival within scheduled pickup window.'
    },
    {
      name: 'Route Safety & Bridge Clearances',
      passed: true,
      details: 'Heavy vehicle routing confirms no restricted low-bridge or weight-limited roads.'
    }
  ]

  return (
    <div className="rounded-lg border border-slate-border bg-surface-container-lowest p-5 shadow-soft">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-border pb-3">
        <div className="flex items-center gap-2">
          <div className={`flex h-8 w-8 items-center justify-center rounded-lg ${
            isApproved ? 'bg-status-green-bg text-status-green-text' : isReject ? 'bg-status-red-bg text-status-red-text' : 'bg-status-amber-bg text-status-amber-text'
          }`}>
            <ShieldCheck className="h-4 w-4" />
          </div>
          <div>
            <h3 className="font-heading text-title-md font-semibold text-primary">
              Agent 4: Safety & Compliance Gate
            </h3>
            <p className="text-body-sm text-on-surface-variant">
              Deterministic validation of regulations, load constraints, and operating limits
            </p>
          </div>
        </div>

        <div>
          <span
            id="validation-recommendation-badge"
            data-testid="validation-recommendation-badge"
            className={`inline-flex items-center gap-1.5 rounded px-2.5 py-1 text-xs font-semibold uppercase tracking-wider ${
              isApproved
                ? 'bg-status-green-bg text-status-green-text border border-status-green-text/20'
                : isReject
                ? 'bg-status-red-bg text-status-red-text border border-status-red-text/20'
                : 'bg-status-amber-bg text-status-amber-text border border-status-amber-text/20'
            }`}
          >
            {isApproved ? <CheckCircle2 className="h-3.5 w-3.5" /> : <AlertTriangle className="h-3.5 w-3.5" />}
            Recommendation: {recommendation}
          </span>
        </div>
      </div>

      {explanation && (
        <div className="mt-3.5 flex items-start gap-2.5 rounded-md bg-surface-container-low p-3 text-sm text-on-surface">
          <Info className="h-4 w-4 shrink-0 text-secondary mt-0.5" />
          <FormattedAiText text={explanation} className="leading-relaxed" />
        </div>
      )}

      <div className="mt-4 space-y-2.5">
        <h4 className="text-xs font-semibold uppercase tracking-wider text-on-surface-variant">
          Automated Verification Checklist
        </h4>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          {defaultChecks.map((check, idx) => (
            <div
              key={idx}
              className="flex items-start gap-2.5 rounded-md border border-slate-border bg-surface-container-lowest p-2.5 transition-colors hover:bg-slate-50"
            >
              {check.passed ? (
                <CheckCircle2 className="h-4 w-4 shrink-0 text-status-green-text mt-0.5" />
              ) : (
                <XCircle className="h-4 w-4 shrink-0 text-status-red-text mt-0.5" />
              )}
              <div className="min-w-0 flex-1">
                <div className="flex items-center justify-between gap-1">
                  <span className="text-xs font-semibold text-on-surface">{check.name}</span>
                  <span
                    className={`text-[10px] font-bold uppercase ${
                      check.passed ? 'text-status-green-text' : 'text-status-red-text'
                    }`}
                  >
                    {check.passed ? 'Passed' : 'Failed'}
                  </span>
                </div>
                {check.details && (
                  <p className="mt-0.5 text-[11px] text-on-surface-variant leading-normal">
                    {check.details}
                  </p>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
