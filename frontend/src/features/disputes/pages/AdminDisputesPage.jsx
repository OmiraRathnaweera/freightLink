import { useState, useMemo } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { toast } from 'sonner'
import {
  Search,
  Filter,
  AlertTriangle,
  Clock,
  CheckCircle2,
  FileText,
  RotateCcw,
  Truck,
  ExternalLink,
  ChevronDown,
  ChevronUp,
  Scale,
  ShieldAlert,
  Loader2,
  Info,
} from 'lucide-react'

import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { UserRole } from '../../../lib/enums.js'
import PageHeader from '../../../components/PageHeader.jsx'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import Input from '../../../components/Input.jsx'

import DisputeStatusBadge from '../components/DisputeStatusBadge.jsx'
import DisputeCategoryBadge from '../components/DisputeCategoryBadge.jsx'
import ResolutionModal from '../components/ResolutionModal.jsx'
import ViewResolutionModal from '../components/ViewResolutionModal.jsx'
import DisputeDetailModal from '../components/DisputeDetailModal.jsx'

import {
  useDisputesQuery,
  useStartReviewMutation,
  useResolveDisputeMutation,
  useResetDisputesMutation,
} from '../api/disputesApi.js'
import {
  DisputeStatus,
  canStartReview,
  canResolveDispute,
  isDisputeResolved,
} from '../lib/disputeRules.js'

export default function AdminDisputesPage() {
  const role = useAppSelector((state) => state.auth?.role)

  // Filtering and search state
  const [activeTab, setActiveTab] = useState('All') // 'All' | 'Raised' | 'UnderReview' | 'Resolved'
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedCategory, setSelectedCategory] = useState('All')

  // Expanded row state for quick reading
  const [expandedRowId, setExpandedRowId] = useState(null)

  // Modals state
  const [resolvingDispute, setResolvingDispute] = useState(null)
  const [viewingResolutionDispute, setViewingResolutionDispute] = useState(null)
  const [inspectingDispute, setInspectingDispute] = useState(null)

  // API queries & mutations
  const { data: disputes = [], isLoading, isError, refetch } = useDisputesQuery({
    status: activeTab,
    search: searchQuery,
    category: selectedCategory,
  })

  // Full dataset query for counting tab stats
  const { data: allDisputes = [] } = useDisputesQuery({ status: 'All', search: '', category: 'All' })

  const startReviewMutation = useStartReviewMutation()
  const resolveMutation = useResolveDisputeMutation()
  const resetMutation = useResetDisputesMutation()

  // Calculate live metric counters
  const counts = useMemo(() => {
    const total = allDisputes.length
    const raised = allDisputes.filter((d) => d.status === DisputeStatus.RAISED).length
    const underReview = allDisputes.filter((d) => d.status === DisputeStatus.UNDER_REVIEW).length
    const resolved = allDisputes.filter((d) => d.status === DisputeStatus.RESOLVED).length
    return { total, raised, underReview, resolved }
  }, [allDisputes])

  // Handlers
  const handleStartReview = async (disputeId) => {
    try {
      const updated = await startReviewMutation.mutateAsync(disputeId)
      toast.success(`Dispute #${updated.displayId} moved to Under Review`, {
        description: 'You can now adjudicate and resolve this dispute.',
      })
    } catch (err) {
      toast.error('Failed to transition dispute', {
        description: err.message || 'An unexpected error occurred.',
      })
    }
  }

  const handleConfirmResolve = async ({ disputeId, outcome, resolutionNote }) => {
    try {
      const updated = await resolveMutation.mutateAsync({
        disputeId,
        outcome,
        resolutionNote,
      })
      setResolvingDispute(null)
      toast.success(`Dispute #${updated.displayId} successfully Resolved`, {
        description: `Adjudication outcome marked as ${outcome}.`,
      })
    } catch (err) {
      toast.error('Resolution failed', {
        description: err.message || 'Resolution note is required.',
      })
    }
  }

  const handleResetData = async () => {
    try {
      await resetMutation.mutateAsync()
      toast.info('Dispute records reset to initial demo state')
    } catch {
      toast.error('Failed to reset records')
    }
  }

  const toggleRowExpanded = (id) => {
    setExpandedRowId((prev) => (prev === id ? null : id))
  }

  // Enforce Admin-only access according to business rules
  if (role && role !== UserRole.ADMIN) {
    return <Navigate to="/unauthorized" replace />
  }

  return (
    <div className="space-y-6 pb-12">
      {/* Page Header */}
      <PageHeader
        eyebrow="Operations & Compliance"
        title="Admin Dispute Management"
        description="Adjudicate and resolve freight claims adhering strictly to ticket Y3S01-81 lifecycle rules (Raised ➔ UnderReview ➔ Resolved)."
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant="secondary"
              onClick={handleResetData}
              disabled={resetMutation.isPending}
              className="text-xs"
              title="Reset mock data to initial demo state"
            >
              <RotateCcw className="h-3.5 w-3.5" />
              Reset Demo Records
            </Button>
          </div>
        }
      />

      {/* Lifecycle Rules Info Banner */}
      <div className="rounded-lg border border-slate-200 bg-slate-50 p-4 shadow-soft">
        <div className="flex items-start gap-3">
          <div className="rounded-md bg-primary p-2 text-white shrink-0 mt-0.5">
            <Scale className="h-4 w-4" />
          </div>
          <div className="flex-1 text-sm">
            <h4 className="font-heading font-bold text-on-surface">Ticket Y3S01-81 Lifecycle Protocol</h4>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-on-surface-variant font-medium">
              <span className="inline-flex items-center gap-1 font-semibold text-status-amber-text bg-status-amber-bg px-2 py-0.5 rounded border border-amber-200">
                1. Raised
              </span>
              <span>➔</span>
              <span className="inline-flex items-center gap-1 font-semibold text-status-blue-text bg-status-blue-bg px-2 py-0.5 rounded border border-blue-200">
                2. Under Review
              </span>
              <span>➔</span>
              <span className="inline-flex items-center gap-1 font-semibold text-status-green-text bg-status-green-bg px-2 py-0.5 rounded border border-green-200">
                3. Resolved
              </span>
              <span className="text-slate-500 italic ml-2">
                (Strict sequential progression; non-empty resolution note strictly required to close).
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* KPI / Metric Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div
          onClick={() => setActiveTab('All')}
          className={`cursor-pointer rounded-lg border p-4 transition-all bg-surface-container-lowest shadow-soft ${
            activeTab === 'All' ? 'border-primary ring-2 ring-primary/20' : 'border-slate-border hover:border-slate-400'
          }`}
        >
          <div className="flex items-center justify-between text-on-surface-variant text-xs font-bold uppercase tracking-wider">
            <span>Total Disputes</span>
            <FileText className="h-4 w-4 text-slate-500" />
          </div>
          <div className="mt-2 text-2xl font-bold font-heading text-on-surface">{counts.total}</div>
          <div className="mt-1 text-[11px] text-on-surface-variant">All lifetime claims</div>
        </div>

        <div
          onClick={() => setActiveTab('Raised')}
          className={`cursor-pointer rounded-lg border p-4 transition-all bg-surface-container-lowest shadow-soft ${
            activeTab === 'Raised' ? 'border-status-amber-text ring-2 ring-status-amber-text/20' : 'border-slate-border hover:border-slate-400'
          }`}
        >
          <div className="flex items-center justify-between text-status-amber-text text-xs font-bold uppercase tracking-wider">
            <span>Needs Review</span>
            <AlertTriangle className="h-4 w-4" />
          </div>
          <div className="mt-2 text-2xl font-bold font-heading text-status-amber-text">{counts.raised}</div>
          <div className="mt-1 text-[11px] text-on-surface-variant">Pending Admin inspection</div>
        </div>

        <div
          onClick={() => setActiveTab('UnderReview')}
          className={`cursor-pointer rounded-lg border p-4 transition-all bg-surface-container-lowest shadow-soft ${
            activeTab === 'UnderReview' ? 'border-status-blue-text ring-2 ring-status-blue-text/20' : 'border-slate-border hover:border-slate-400'
          }`}
        >
          <div className="flex items-center justify-between text-status-blue-text text-xs font-bold uppercase tracking-wider">
            <span>Under Review</span>
            <Clock className="h-4 w-4" />
          </div>
          <div className="mt-2 text-2xl font-bold font-heading text-status-blue-text">{counts.underReview}</div>
          <div className="mt-1 text-[11px] text-on-surface-variant">In active adjudication</div>
        </div>

        <div
          onClick={() => setActiveTab('Resolved')}
          className={`cursor-pointer rounded-lg border p-4 transition-all bg-surface-container-lowest shadow-soft ${
            activeTab === 'Resolved' ? 'border-status-green-text ring-2 ring-status-green-text/20' : 'border-slate-border hover:border-slate-400'
          }`}
        >
          <div className="flex items-center justify-between text-status-green-text text-xs font-bold uppercase tracking-wider">
            <span>Resolved</span>
            <CheckCircle2 className="h-4 w-4" />
          </div>
          <div className="mt-2 text-2xl font-bold font-heading text-status-green-text">{counts.resolved}</div>
          <div className="mt-1 text-[11px] text-on-surface-variant">Closed with note</div>
        </div>
      </div>

      {/* Main Filter & Content Card */}
      <Card className="p-0 overflow-hidden">
        {/* Filter Bar & Tabs Header */}
        <div className="border-b border-slate-border bg-slate-50/70 p-4 space-y-4">
          <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
            {/* Filter Tabs */}
            <div className="flex items-center gap-1.5 overflow-x-auto pb-1 sm:pb-0" role="tablist">
              {[
                { id: 'All', label: 'All Disputes', count: counts.total },
                { id: 'Raised', label: 'Raised', count: counts.raised, tone: 'amber' },
                { id: 'UnderReview', label: 'Under Review', count: counts.underReview, tone: 'blue' },
                { id: 'Resolved', label: 'Resolved', count: counts.resolved, tone: 'green' },
              ].map((tab) => {
                const isActive = activeTab === tab.id
                return (
                  <button
                    key={tab.id}
                    type="button"
                    role="tab"
                    aria-selected={isActive}
                    onClick={() => setActiveTab(tab.id)}
                    className={`flex items-center gap-2 px-3 py-1.5 rounded-md text-xs font-bold transition-all whitespace-nowrap ${
                      isActive
                        ? 'bg-primary text-on-primary shadow-xs'
                        : 'bg-white text-on-surface-variant border border-slate-200 hover:bg-slate-100 hover:text-on-surface'
                    }`}
                  >
                    <span>{tab.label}</span>
                    <span
                      className={`px-1.5 py-0.2 rounded-full text-[11px] font-mono ${
                        isActive ? 'bg-primary-container text-white' : 'bg-slate-200 text-slate-700'
                      }`}
                    >
                      {tab.count}
                    </span>
                  </button>
                )
              })}
            </div>

            {/* Category Filter Dropdown */}
            <div className="flex items-center gap-2 shrink-0">
              <label htmlFor="category-select" className="text-xs font-semibold text-slate-500 whitespace-nowrap">
                Category:
              </label>
              <select
                id="category-select"
                value={selectedCategory}
                onChange={(e) => setSelectedCategory(e.target.value)}
                className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 focus:border-primary focus:outline-none"
              >
                <option value="All">All Categories</option>
                <option value="Damage">Damage</option>
                <option value="Delay">Delay</option>
                <option value="Payment Issue">Payment Issue</option>
                <option value="Other">Other</option>
              </select>
            </div>
          </div>

          {/* Search Input */}
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search by Dispute ID (#DISP-1042), Linked Trip (#TRP-8841), Claimant Name, or Route..."
              className="w-full rounded-md border border-slate-300 bg-white py-2 pl-9 pr-4 text-sm placeholder:text-slate-400 focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
            />
            {searchQuery && (
              <button
                type="button"
                onClick={() => setSearchQuery('')}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-slate-400 hover:text-slate-600"
              >
                Clear
              </button>
            )}
          </div>
        </div>

        {/* Loading State */}
        {isLoading && (
          <div className="flex flex-col items-center justify-center p-16 gap-3">
            <Loader2 className="h-7 w-7 animate-spin text-primary" />
            <p className="text-sm text-on-surface-variant font-medium">Fetching disputes queue…</p>
          </div>
        )}

        {/* Error State */}
        {isError && (
          <div className="p-8 text-center">
            <p className="text-status-red-text text-sm font-semibold">Failed to load dispute records.</p>
            <Button variant="secondary" onClick={() => refetch()} className="mt-3 text-xs">
              Retry
            </Button>
          </div>
        )}

        {/* Empty State */}
        {!isLoading && !isError && disputes.length === 0 && (
          <EmptyState
            icon={Scale}
            title="No disputes found"
            description={
              searchQuery || selectedCategory !== 'All' || activeTab !== 'All'
                ? 'Try adjusting your search query, status tab, or category filters.'
                : 'There are currently no dispute cases matching this view.'
            }
            action={
              (searchQuery || selectedCategory !== 'All' || activeTab !== 'All') && (
                <Button
                  variant="secondary"
                  onClick={() => {
                    setActiveTab('All')
                    setSearchQuery('')
                    setSelectedCategory('All')
                  }}
                  className="text-xs"
                >
                  Reset all filters
                </Button>
              )
            }
          />
        )}

        {/* Dispute Records Table */}
        {!isLoading && !isError && disputes.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-sm">
              <thead className="border-b border-slate-border bg-slate-50 text-[11px] font-heading font-bold uppercase tracking-wider text-slate-600">
                <tr>
                  <th className="px-4 py-3">Dispute ID & Date</th>
                  <th className="px-4 py-3">Raised By</th>
                  <th className="px-4 py-3">Linked Trip</th>
                  <th className="px-4 py-3">Reason / Description</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 font-sans">
                {disputes.map((dispute) => {
                  const isExpanded = expandedRowId === dispute.disputeId
                  const formattedDate = new Date(dispute.raisedDate).toLocaleDateString('en-US', {
                    month: 'short',
                    day: 'numeric',
                    year: 'numeric',
                  })
                  const formattedTime = new Date(dispute.raisedDate).toLocaleTimeString('en-US', {
                    hour: '2-digit',
                    minute: '2-digit',
                  })

                  const isReviewingThis =
                    startReviewMutation.isPending &&
                    startReviewMutation.variables === dispute.disputeId

                  return (
                    <tr
                      key={dispute.disputeId}
                      className="group transition-colors hover:bg-slate-50/70"
                    >
                      {/* Dispute ID & Raised Date */}
                      <td className="px-4 py-3.5 align-top">
                        <div className="flex items-center gap-1.5">
                          <button
                            type="button"
                            onClick={() => setInspectingDispute(dispute)}
                            className="font-mono font-bold text-primary hover:underline hover:text-primary-container"
                            title="Click to view complete dispute file"
                          >
                            #{dispute.displayId || dispute.disputeId?.slice(0, 8)}
                          </button>
                        </div>
                        <div className="text-xs text-slate-500 font-mono mt-0.5">
                          <span>{formattedDate}</span>
                          <span className="mx-1">•</span>
                          <span>{formattedTime}</span>
                        </div>
                      </td>

                      {/* Raised By */}
                      <td className="px-4 py-3.5 align-top">
                        <div className="flex items-center gap-1.5">
                          <span className="font-semibold text-slate-900">{dispute.raisedByUser?.name}</span>
                          <span
                            className={`rounded px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wider ${
                              dispute.raisedByUser?.role === 'Shipper'
                                ? 'bg-amber-100 text-amber-900 border border-amber-200'
                                : 'bg-indigo-100 text-indigo-900 border border-indigo-200'
                            }`}
                          >
                            {dispute.raisedByUser?.role}
                          </span>
                        </div>
                        <div className="text-xs text-slate-500 truncate max-w-[200px]" title={dispute.raisedByUser?.company}>
                          {dispute.raisedByUser?.company}
                        </div>
                        <div className="text-xs text-slate-400 font-mono truncate max-w-[200px]">
                          {dispute.raisedByUser?.email}
                        </div>
                      </td>

                      {/* Linked Trip */}
                      <td className="px-4 py-3.5 align-top">
                        <Link
                          to={`/trips/${dispute.trip?.tripId}`}
                          className="font-mono font-bold text-primary hover:underline inline-flex items-center gap-1 text-xs"
                          title="Open trip details"
                        >
                          <span>#{dispute.trip?.tripId}</span>
                          <ExternalLink className="h-3 w-3 opacity-60" />
                        </Link>
                        <div className="text-xs font-medium text-slate-800 flex items-center gap-1 mt-0.5">
                          <Truck className="h-3 w-3 text-slate-400 shrink-0" />
                          <span>{dispute.trip?.routeSummary}</span>
                        </div>
                        <div className="text-[11px] text-slate-400 truncate max-w-[180px]">
                          {dispute.trip?.carrierAgency}
                        </div>
                      </td>

                      {/* Reason / Description */}
                      <td className="px-4 py-3.5 align-top max-w-xs">
                        <div className="mb-1.5">
                          <DisputeCategoryBadge category={dispute.category} />
                        </div>
                        <p
                          className={`text-xs text-slate-700 leading-relaxed ${
                            isExpanded ? '' : 'line-clamp-2'
                          }`}
                        >
                          {dispute.description}
                        </p>
                        <button
                          type="button"
                          onClick={() => toggleRowExpanded(dispute.disputeId)}
                          className="mt-1 text-[11px] font-semibold text-primary hover:underline inline-flex items-center gap-0.5"
                        >
                          {isExpanded ? (
                            <>
                              Show less <ChevronUp className="h-3 w-3" />
                            </>
                          ) : (
                            <>
                              Read more <ChevronDown className="h-3 w-3" />
                            </>
                          )}
                        </button>
                      </td>

                      {/* Current Status Badge */}
                      <td className="px-4 py-3.5 align-top whitespace-nowrap">
                        <DisputeStatusBadge status={dispute.status} />
                        {dispute.status === DisputeStatus.RESOLVED && dispute.resolution?.outcome && (
                          <div className="text-[11px] font-mono text-emerald-700 font-semibold mt-1">
                            [{dispute.resolution.outcome}]
                          </div>
                        )}
                      </td>

                      {/* Actions Column */}
                      <td className="px-4 py-3.5 align-top text-right whitespace-nowrap">
                        {/* If Raised: "Start Review" */}
                        {canStartReview(dispute.status) && (
                          <Button
                            variant="status"
                            status="blue"
                            onClick={() => handleStartReview(dispute.disputeId)}
                            disabled={isReviewingThis}
                            className="text-xs py-1.5 px-3"
                          >
                            {isReviewingThis ? (
                              <>
                                <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
                                Starting Review...
                              </>
                            ) : (
                              <>
                                <Clock className="h-3.5 w-3.5 mr-1" />
                                Start Review
                              </>
                            )}
                          </Button>
                        )}

                        {/* If UnderReview: "Resolve Dispute" */}
                        {canResolveDispute(dispute.status) && (
                          <Button
                            variant="status"
                            status="green"
                            onClick={() => setResolvingDispute(dispute)}
                            className="text-xs py-1.5 px-3"
                          >
                            <CheckCircle2 className="h-3.5 w-3.5 mr-1" />
                            Resolve Dispute
                          </Button>
                        )}

                        {/* If Resolved: "View Resolution" */}
                        {isDisputeResolved(dispute.status) && (
                          <Button
                            variant="secondary"
                            onClick={() => setViewingResolutionDispute(dispute)}
                            className="text-xs py-1.5 px-3 bg-emerald-50/50 border-emerald-200 text-emerald-800 hover:bg-emerald-100/60"
                          >
                            <FileText className="h-3.5 w-3.5 mr-1 text-emerald-700" />
                            View Resolution
                          </Button>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* Resolution Modal (Ticket Y3S01-81) */}
      <ResolutionModal
        dispute={resolvingDispute}
        isOpen={Boolean(resolvingDispute)}
        onClose={() => setResolvingDispute(null)}
        onConfirm={handleConfirmResolve}
        isPending={resolveMutation.isPending}
      />

      {/* View Resolution Modal (Read-only for Resolved state) */}
      <ViewResolutionModal
        dispute={viewingResolutionDispute}
        isOpen={Boolean(viewingResolutionDispute)}
        onClose={() => setViewingResolutionDispute(null)}
      />

      {/* Full Dispute Claim Inspector Modal */}
      <DisputeDetailModal
        dispute={inspectingDispute}
        isOpen={Boolean(inspectingDispute)}
        onClose={() => setInspectingDispute(null)}
        onStartReview={handleStartReview}
        onOpenResolve={(d) => setResolvingDispute(d)}
        onOpenResolution={(d) => setViewingResolutionDispute(d)}
        isReviewPending={startReviewMutation.isPending}
      />
    </div>
  )
}
