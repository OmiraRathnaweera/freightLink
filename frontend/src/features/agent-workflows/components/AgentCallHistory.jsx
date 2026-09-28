import { useState } from 'react'
import { History, ChevronDown, ChevronUp, CheckCircle2, XCircle, Clock, RotateCcw } from 'lucide-react'
import { useLoadMatchHistoryQuery } from '../api/agentWorkflowsApi.js'
import { formatCurrency } from '../../loads/lib/format.js'

const STEP_LABELS = {
  Planner: 'Agent 1: Planner',
  DomainAnalysis: 'Agent 2: Domain Analysis',
  MatchingPricing: 'Agent 3: Matching & Pricing',
  ValidationSafety: 'Agent 4: Validation & Safety',
}

const GOOD_STATUSES = new Set(['Succeeded', 'Completed', 'AwaitingApproval'])
const BAD_STATUSES = new Set(['Failed', 'Aborted'])

function StatusBadge({ status }) {
  const isGood = GOOD_STATUSES.has(status)
  const isBad = BAD_STATUSES.has(status)
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider ${
        isGood
          ? 'bg-status-green-bg text-status-green-text'
          : isBad
          ? 'bg-status-red-bg text-status-red-text'
          : 'bg-status-amber-bg text-status-amber-text'
      }`}
    >
      {isGood ? <CheckCircle2 className="h-3 w-3" /> : isBad ? <XCircle className="h-3 w-3" /> : <Clock className="h-3 w-3" />}
      {status}
    </span>
  )
}

function ToolCallRow({ toolCall }) {
  return (
    <div className="rounded border border-slate-200 bg-white px-2.5 py-1.5 text-[11px]">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex min-w-0 items-center gap-2">
          {toolCall.success ? (
            <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-status-green-text" />
          ) : (
            <XCircle className="h-3.5 w-3.5 shrink-0 text-status-red-text" />
          )}
          <span className="font-mono font-semibold text-on-surface">{toolCall.toolName}</span>
          <span className="text-on-surface-variant">attempt #{toolCall.attemptNo}</span>
        </div>
        <div className="flex shrink-0 items-center gap-2 text-on-surface-variant">
          {toolCall.durationMs != null && <span className="font-mono">{toolCall.durationMs} ms</span>}
          {toolCall.httpStatusCode != null && <span className="font-mono">HTTP {toolCall.httpStatusCode}</span>}
          <span>{new Date(toolCall.calledAt).toLocaleTimeString()}</span>
        </div>
      </div>
      {toolCall.errorMessage && <p className="mt-1 text-status-red-text">{toolCall.errorMessage}</p>}
    </div>
  )
}

function StepRow({ step }) {
  const [expanded, setExpanded] = useState(false)
  const hasToolCalls = step.toolCalls && step.toolCalls.length > 0

  return (
    <div className="rounded-md border border-slate-border bg-surface-container-lowest p-2.5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <span className="font-mono text-[10px] font-bold text-on-surface-variant">#{step.stepNo}</span>
          <span className="text-xs font-semibold text-on-surface">{STEP_LABELS[step.agentRole] || step.agentRole}</span>
        </div>
        <div className="flex items-center gap-2">
          {step.durationMs != null && (
            <span className="font-mono text-[11px] text-on-surface-variant">{step.durationMs} ms</span>
          )}
          <StatusBadge status={step.status} />
          {hasToolCalls && (
            <button
              type="button"
              onClick={() => setExpanded((e) => !e)}
              className="inline-flex items-center gap-1 rounded border border-slate-300 px-1.5 py-0.5 text-[10px] font-semibold text-on-surface-variant hover:bg-slate-50"
            >
              {step.toolCalls.length} tool call{step.toolCalls.length === 1 ? '' : 's'}
              {expanded ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
            </button>
          )}
        </div>
      </div>
      {step.errorMessage && <p className="mt-1.5 text-[11px] text-status-red-text">{step.errorMessage}</p>}
      {expanded && hasToolCalls && (
        <div className="mt-2 space-y-1.5">
          {step.toolCalls.map((tc, i) => (
            <ToolCallRow key={`${tc.toolName}-${tc.attemptNo}-${i}`} toolCall={tc} />
          ))}
        </div>
      )}
    </div>
  )
}

function AttemptCard({ attempt, defaultOpen }) {
  const [open, setOpen] = useState(defaultOpen)

  return (
    <div className="rounded-lg border border-slate-border bg-surface-container-low">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full flex-wrap items-center justify-between gap-3 p-3.5 text-left"
      >
        <div className="flex items-center gap-3">
          <span className="flex h-7 w-7 items-center justify-center rounded-full bg-primary/10 font-mono text-xs font-bold text-primary">
            #{attempt.attemptNo}
          </span>
          <div>
            <div className="flex items-center gap-2">
              <span className="text-sm font-semibold text-on-surface">
                {attempt.selectedAgencyName || 'No agency selected yet'}
              </span>
              <StatusBadge status={attempt.status} />
            </div>
            <p className="text-[11px] text-on-surface-variant">
              Started {new Date(attempt.startedAt).toLocaleString()}
              {attempt.completedAt && ` · Completed ${new Date(attempt.completedAt).toLocaleString()}`}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-3">
          {attempt.proposedPrice != null && (
            <span className="font-mono text-sm font-bold text-primary">{formatCurrency(attempt.proposedPrice)}</span>
          )}
          {open ? <ChevronUp className="h-4 w-4 text-on-surface-variant" /> : <ChevronDown className="h-4 w-4 text-on-surface-variant" />}
        </div>
      </button>

      {open && (
        <div className="space-y-3 border-t border-slate-border p-3.5">
          {attempt.shipperMessage && (
            <p className="rounded-md bg-primary/5 p-2.5 text-xs italic text-on-surface-variant">
              "{attempt.shipperMessage}"
            </p>
          )}
          {attempt.decision && (
            <div className="flex items-start gap-2 rounded-md border border-slate-border bg-white p-2.5 text-xs">
              <RotateCcw className="h-3.5 w-3.5 shrink-0 mt-0.5 text-status-amber-text" />
              <div>
                <span className="font-semibold text-on-surface">Shipper decision: {attempt.decision}</span>
                {attempt.decisionReason && <p className="text-on-surface-variant">{attempt.decisionReason}</p>}
              </div>
            </div>
          )}
          <div className="space-y-2">
            {attempt.steps.map((step) => (
              <StepRow key={step.stepNo} step={step} />
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

export default function AgentCallHistory({ loadId }) {
  const [isOpen, setIsOpen] = useState(false)
  const historyQuery = useLoadMatchHistoryQuery(loadId, { enabled: isOpen })
  const attempts = historyQuery.data?.attempts || []

  return (
    <div className="rounded-lg border border-slate-border bg-surface-container-lowest shadow-soft">
      <button
        type="button"
        id="toggle-agent-call-history-btn"
        data-testid="toggle-agent-call-history-btn"
        onClick={() => setIsOpen((o) => !o)}
        className="flex w-full items-center justify-between gap-3 p-4"
      >
        <div className="flex items-center gap-2.5">
          <History className="h-5 w-5 text-secondary" />
          <div className="text-left">
            <h3 className="font-heading text-title-md font-semibold text-primary">Agent Call History</h3>
            <p className="text-body-sm text-on-surface-variant">
              Every attempt the 4-agent pipeline has made for this load, including all tool calls.
            </p>
          </div>
        </div>
        {isOpen ? <ChevronUp className="h-5 w-5 text-on-surface-variant" /> : <ChevronDown className="h-5 w-5 text-on-surface-variant" />}
      </button>

      {isOpen && (
        <div className="space-y-3 border-t border-slate-border p-4">
          {historyQuery.isLoading && <p className="text-body-sm text-on-surface-variant">Loading history…</p>}
          {historyQuery.isError && (
            <p className="text-body-sm text-status-red-text">
              {historyQuery.error?.message || 'Failed to load agent call history.'}
            </p>
          )}
          {!historyQuery.isLoading && attempts.length === 0 && (
            <p className="text-body-sm text-on-surface-variant">No matching attempts have been made yet.</p>
          )}
          {attempts.map((attempt, idx) => (
            <AttemptCard key={attempt.workflowRunId} attempt={attempt} defaultOpen={idx === 0} />
          ))}
        </div>
      )}
    </div>
  )
}
