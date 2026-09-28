import { useEffect, useRef, useState } from 'react'
import { useSearchParams, Link } from 'react-router-dom'
import {
  Sparkles,
  ArrowLeft,
  CheckCircle2,
  AlertTriangle,
} from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Skeleton from '../../../components/Skeleton.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import { useLoadsQuery, useLoadDetailQuery } from '../../loads/api/loadsApi.js'
import {
  useLoadMatchQuery,
  useTriggerMatchMutation,
  useConfirmMatchMutation,
  useRejectMatchMutation,
  useReviseMatchMutation,
} from '../api/agentWorkflowsApi.js'
import WorkflowStepper from '../components/WorkflowStepper.jsx'
import MatchRecommendationCard from '../components/MatchRecommendationCard.jsx'
import MatchDecisionDialog from '../components/MatchDecisionDialog.jsx'
import ValidationChecklist from '../components/ValidationChecklist.jsx'
import AlternateCandidatesList from '../components/AlternateCandidatesList.jsx'
import LoadSelectorBar from '../components/LoadSelectorBar.jsx'
import FormattedAiText from '../components/FormattedAiText.jsx'

export default function AgentWorkflowConsolePage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const loadIdParam = searchParams.get('loadId')

  // Fetch loads for selection (Posted and Matched loads are primary candidates)
  const loadsQuery = useLoadsQuery({ page: 1, pageSize: 50 })
  const availableLoads = loadsQuery.data?.items || []

  // Determine active load id: from query param or fallback to first available load
  const activeLoadId = loadIdParam || availableLoads[0]?.loadId

  // Fetch full details of the active load
  const loadDetailQuery = useLoadDetailQuery(activeLoadId, {
    enabled: Boolean(activeLoadId),
  })
  const currentLoad = loadDetailQuery.data

  // Fetch AI match recommendation for active load
  const matchQuery = useLoadMatchQuery(activeLoadId, {
    enabled: Boolean(activeLoadId),
  })
  const matchData = matchQuery.data

  // Trigger/confirm/reject/revise match mutations
  const triggerMutation = useTriggerMatchMutation()
  const confirmMutation = useConfirmMatchMutation()
  const rejectMutation = useRejectMatchMutation()
  const reviseMutation = useReviseMatchMutation()

  // Track candidate selection override
  const [selectedAgencyId, setSelectedAgencyId] = useState(null)
  const [actionSuccessMessage, setActionSuccessMessage] = useState(null)
  const [actionErrorMessage, setActionErrorMessage] = useState(null)
  const [pendingDecisionType, setPendingDecisionType] = useState(null)

  const activeSelectedAgencyId = selectedAgencyId || matchData?.recommendedAgency?.agencyId

  // Matching is a deliberate, explicit action (POST /match/trigger), never a side effect of
  // viewing this page — but the console still starts matching automatically the first time a
  // load with no prior run is opened, as two distinct calls (the GET above, then this trigger)
  // rather than one GET silently mutating state. triggeredLoadIdsRef guards against re-firing on
  // every refetch/re-render for the same load.
  const triggeredLoadIdsRef = useRef(new Set())
  useEffect(() => {
    if (
      activeLoadId &&
      matchData?.workflowStatus === 'NotStarted' &&
      !triggeredLoadIdsRef.current.has(activeLoadId) &&
      !triggerMutation.isPending
    ) {
      triggeredLoadIdsRef.current.add(activeLoadId)
      triggerMutation.mutate(activeLoadId)
    }
  }, [activeLoadId, matchData?.workflowStatus, triggerMutation])

  // Handle switching active load
  const handleSelectLoad = (newLoadId) => {
    setActionSuccessMessage(null)
    setActionErrorMessage(null)
    setSelectedAgencyId(null)
    setSearchParams({ loadId: newLoadId })
  }

  // Handle match confirmation
  const handleApproveMatch = async ({ loadId, agencyId }) => {
    setActionErrorMessage(null)
    setActionSuccessMessage(null)

    try {
      await confirmMutation.mutateAsync({ loadId, agencyId })
      setActionSuccessMessage(
        'Match approved successfully! An operational assignment has been proposed and sent to the carrier.'
      )
    } catch (err) {
      const msg =
        err?.response?.data?.message ||
        err?.message ||
        'Failed to confirm match. The assignment may already have been accepted or modified.'
      setActionErrorMessage(msg)
    }
  }

  // Handle retry match: an explicit command (POST /match/trigger), not a side effect of a GET
  const handleRetryMatch = async () => {
    setActionErrorMessage(null)
    setActionSuccessMessage(null)
    try {
      if (activeLoadId) {
        await triggerMutation.mutateAsync(activeLoadId)
      }
    } catch (err) {
      setActionErrorMessage(
        err?.message || 'Failed to trigger matching. Please try again shortly.'
      )
    }
  }

  // Handle a Reject/Revise decision submitted through MatchDecisionDialog
  const handleMatchDecided = async (decisionType) => {
    setPendingDecisionType(null)
    setActionErrorMessage(null)
    setActionSuccessMessage(
      decisionType === 'reject'
        ? 'Match recommendation rejected.'
        : 'Revision requested — fetching a new recommendation.'
    )

    if (decisionType === 'revise' && activeLoadId) {
      try {
        await triggerMutation.mutateAsync(activeLoadId)
        return
      } catch {
        // fall through to refetch below regardless
      }
    }
    await matchQuery.refetch()
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <Link
            to="/loads"
            className="mb-2 inline-flex items-center gap-1 text-sm font-medium text-secondary hover:text-primary transition-colors"
          >
            <ArrowLeft className="h-4 w-4" /> Back to My Loads
          </Link>
          <div className="flex items-center gap-3">
            <h1 className="font-heading text-headline-lg font-bold text-primary">
              AI Agent Matching & Approval Console
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-primary/10 px-3 py-1 text-xs font-bold text-primary">
              <Sparkles className="h-3.5 w-3.5 fill-primary/30" />
              LangGraph Multi-Agent
            </span>
          </div>
          <p className="mt-1 text-body-md text-on-surface-variant">
            Review Agent 3's optimal carrier match recommendation, verify safety compliance, and confirm dispatch.
          </p>
        </div>
      </div>

      {/* Load Selector Bar */}
      <LoadSelectorBar
        currentLoad={currentLoad}
        availableLoads={availableLoads}
        onSelectLoad={handleSelectLoad}
        isLoading={loadsQuery.isLoading}
      />

      {/* Notifications Banner */}
      {actionSuccessMessage && (
        <div
          id="action-success-banner"
          data-testid="action-success-banner"
          className="flex items-start gap-3 rounded-lg border border-status-green-text/30 bg-status-green-bg p-4 text-status-green-text shadow-sm"
        >
          <CheckCircle2 className="h-5 w-5 shrink-0 mt-0.5" />
          <div className="flex-1">
            <p className="text-sm font-bold">Proposal Approved</p>
            <p className="text-xs">{actionSuccessMessage}</p>
          </div>
        </div>
      )}

      {actionErrorMessage && (
        <div
          id="action-error-banner"
          data-testid="action-error-banner"
          className="flex items-start gap-3 rounded-lg border border-status-red-text/30 bg-status-red-bg p-4 text-status-red-text shadow-sm"
        >
          <AlertTriangle className="h-5 w-5 shrink-0 mt-0.5" />
          <div className="flex-1">
            <p className="text-sm font-bold">Action Failed</p>
            <p className="text-xs">{actionErrorMessage}</p>
          </div>
        </div>
      )}

      {/* Loading State */}
      {matchQuery.isLoading && (
        <div className="space-y-6">
          <Skeleton className="h-32 w-full rounded-lg" />
          <Skeleton className="h-96 w-full rounded-lg" />
        </div>
      )}

      {/* Error State */}
      {matchQuery.isError && (
        <Card>
          <ErrorState
            title="Failed to Load AI Recommendations"
            description={
              matchQuery.error?.response?.data?.message ||
              matchQuery.error?.message ||
              'Could not retrieve agent matching results for this load.'
            }
            onRetry={matchQuery.refetch}
          />
        </Card>
      )}

      {/* Empty State: No active load found */}
      {!activeLoadId && !loadsQuery.isLoading && (
        <Card>
          <EmptyState
            title="No Active Loads Found"
            description="Create or publish a load in your account to trigger autonomous agent matching."
            actionLabel="Create New Load"
            onAction={() => {
              window.location.href = '/loads/new'
            }}
          />
        </Card>
      )}

      {/* Main Content: Workflow Pipeline, Recommendation, and Validation */}
      {matchData && (
        <div className="space-y-6">
          {/* 0. Agent 1's conversational message to the shipper */}
          {matchData.shipperMessage && (
            <div
              data-testid="shipper-message-banner"
              className="flex items-start gap-3 rounded-lg border border-primary/20 bg-primary/5 p-4 text-on-surface shadow-sm"
            >
              <Sparkles className="h-5 w-5 shrink-0 mt-0.5 text-primary" />
              <div className="flex-1">
                <p className="text-xs font-bold text-primary">Agent 1</p>
                <FormattedAiText text={matchData.shipperMessage} className="text-sm text-on-surface-variant" />
              </div>
            </div>
          )}

          {/* 1. 4-Agent LangGraph Stepper */}
          <WorkflowStepper
            steps={matchData.steps}
            workflowStatus={matchData.workflowStatus}
          />

          {/* 2. Spotlight Recommendation Card (Agent 3) */}
          <MatchRecommendationCard
            loadId={activeLoadId}
            loadStatus={matchData.loadStatus}
            recommendedAgency={matchData.recommendedAgency}
            selectedAgencyId={activeSelectedAgencyId}
            alternateCandidates={matchData.alternateCandidates}
            onResetSelectedAgency={() => setSelectedAgencyId(null)}
            existingAssignment={matchData.existingAssignment}
            onApproveMatch={handleApproveMatch}
            onRetryMatch={handleRetryMatch}
            onRejectMatch={() => setPendingDecisionType('reject')}
            onReviseMatch={() => setPendingDecisionType('revise')}
            isApproving={confirmMutation.isPending}
            isRetrying={triggerMutation.isPending}
            isRejecting={rejectMutation.isPending}
            isRevising={reviseMutation.isPending}
          />

          {pendingDecisionType && (
            <MatchDecisionDialog
              loadId={activeLoadId}
              decisionType={pendingDecisionType}
              onClose={() => setPendingDecisionType(null)}
              onDecided={() => handleMatchDecided(pendingDecisionType)}
            />
          )}

          {/* 3. Side-by-Side: Agent 4 Safety Gate & Alternate Candidates */}
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            {/* Agent 4 Validation Checklist */}
            <ValidationChecklist validation={matchData.validation} />

            {/* Alternate Candidates List */}
            <AlternateCandidatesList
              candidates={matchData.alternateCandidates}
              selectedAgencyId={activeSelectedAgencyId}
              onSelectAgency={(agencyId) => setSelectedAgencyId(agencyId)}
              isMatched={matchData.loadStatus === 'Matched' || Boolean(matchData.existingAssignment)}
            />
          </div>
        </div>
      )}
    </div>
  )
}
