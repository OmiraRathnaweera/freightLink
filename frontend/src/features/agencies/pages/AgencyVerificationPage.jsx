import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { CheckCircle2, ExternalLink, Loader2, XCircle } from 'lucide-react'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { ComplianceDocStatus, UserRole } from '../../../lib/enums.js'
import { getFileUrl } from '../../../lib/api/fileUrl.js'
import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import ViewComplianceDocsModal from '../components/ViewComplianceDocsModal.jsx'

import {
  useAgenciesQuery,
  useVerificationQueueQuery,
  useVerifyAgencyMutation,
  useActivateAgencyMutation,
  useSuspendAgencyMutation,
  useVerifyComplianceDocMutation,
  useRejectComplianceDocMutation,
} from '../api/agencyApi.js'
import { COMPLIANCE_DOC_TYPES, getComplianceDocTypeLabel } from '../lib/complianceDocTypes.js'
import { getAgencyStatusTone, getComplianceDocStatusTone } from '../lib/statusTone.js'

const MANDATORY_DOC_TYPE_IDS = COMPLIANCE_DOC_TYPES.filter((t) => t.mandatory).map((t) => t.id)

function countVerifiedMandatory(docs) {
  return MANDATORY_DOC_TYPE_IDS.filter((typeId) =>
    docs.some((d) => d.docType === typeId && d.status === ComplianceDocStatus.VERIFIED),
  ).length
}

export default function AgencyVerificationPage() {
  const role = useAppSelector((state) => state.auth.role)
  const queryClient = useQueryClient()

  const queueQuery = useVerificationQueueQuery()
  const verifiedQuery = useAgenciesQuery({ status: 'Verified' })

  const isLoading = queueQuery.isLoading || verifiedQuery.isLoading
  const isError = queueQuery.isError || verifiedQuery.isError

  const pendingItems = queueQuery.data ?? []
  const readyToActivate = verifiedQuery.data?.items ?? []

  function invalidateAgencyLists() {
    queryClient.invalidateQueries({ queryKey: ['agencies'] })
  }

  const verifyAgencyMutation = useVerifyAgencyMutation({
    onSuccess: () => {
      invalidateAgencyLists()
      toast.success('Agency successfully verified!')
    },
    onError: (err) => toast.error(`Failed to verify agency: ${err.message}`),
  })

  const activateMutation = useActivateAgencyMutation({
    onSuccess: () => {
      invalidateAgencyLists()
      toast.success('Agency successfully activated!')
    },
    onError: (err) => toast.error(`Failed to activate agency: ${err.message}`),
  })

  const suspendMutation = useSuspendAgencyMutation({
    onSuccess: () => {
      invalidateAgencyLists()
      toast.success('Agency successfully suspended!')
    },
    onError: (err) => toast.error(`Failed to suspend agency: ${err.message}`),
  })

  const [selectedAgencyForDocs, setSelectedAgencyForDocs] = useState(null)
  const [actionedDocIds, setActionedDocIds] = useState(() => new Set())

  const verifyDocMutation = useVerifyComplianceDocMutation({
    onSuccess: () => toast.success('Document verified.'),
    onError: (err, variables) => {
      if (variables?.docId) {
        setActionedDocIds((prev) => {
          const next = new Set(prev)
          next.delete(variables.docId)
          return next
        })
      }
      toast.error(`Failed to verify document: ${err.message}`)
    },
  })

  const rejectDocMutation = useRejectComplianceDocMutation({
    onSuccess: () => toast.success('Document rejected.'),
    onError: (err, variables) => {
      if (variables?.docId) {
        setActionedDocIds((prev) => {
          const next = new Set(prev)
          next.delete(variables.docId)
          return next
        })
      }
      toast.error(`Failed to reject document: ${err.message}`)
    },
  })

  function handleVerifyDoc(agencyId, docId) {
    setActionedDocIds((prev) => new Set(prev).add(docId))
    verifyDocMutation.mutate({ agencyId, docId })
  }

  function handleRejectDoc(agencyId, docId) {
    setActionedDocIds((prev) => new Set(prev).add(docId))
    rejectDocMutation.mutate({ agencyId, docId })
  }

  const agencyActionPending =
    verifyAgencyMutation.isPending || activateMutation.isPending || suspendMutation.isPending

  // Enforce Admin-only access
  if (role !== UserRole.ADMIN) {
    return <Navigate to="/unauthorized" replace />
  }

  return (
    <div className="space-y-8">
      <PageHeader
        title="Agency Verification Queue"
        subtitle="Review uploaded compliance documents, approve or reject each one, then verify and activate the agency."
      />

      {isLoading && <p className="text-slate-500">Loading agenciesâ€¦</p>}
      {isError && <p className="text-status-red-text">Failed to load the verification queue.</p>}

      {!isLoading && !isError && (
        <>
          <section className="space-y-4">
            <h2 className="text-title-md font-bold text-on-surface">
              Pending Verification
              <span className="ml-2 text-body-sm font-normal text-on-surface-variant">
                ({pendingItems.length})
              </span>
            </h2>

            {pendingItems.length === 0 ? (
              <Card className="p-6 text-center text-body-sm text-on-surface-variant">
                No agencies are currently awaiting verification.
              </Card>
            ) : (
              <div className="grid gap-6">
                {pendingItems.map(({ agency, complianceDocs }) => {
                  const verifiedMandatoryCount = countVerifiedMandatory(complianceDocs)

                  return (
                    <Card key={agency.agencyId} className="overflow-hidden p-0">
                      <Card.Header className="flex flex-wrap items-center justify-between gap-3 bg-slate-50/50">
                        <div>
                          <h3 className="text-title-sm font-bold text-slate-900">{agency.name}</h3>
                          <p className="text-body-xs text-slate-500">Reg No: {agency.businessRegNo}</p>
                        </div>
                        <div className="flex items-center gap-3">
                          <span className="text-body-xs font-medium text-slate-500">
                            {verifiedMandatoryCount} / {MANDATORY_DOC_TYPE_IDS.length} mandatory docs verified
                          </span>
                          <StatusBadge tone={getAgencyStatusTone(agency.status)}>{agency.status}</StatusBadge>
                      <Button
                        variant="secondary"
                        onClick={() => setSelectedAgencyForDocs(agency)}
                      >
                        <ExternalLink className="mr-1.5 h-4 w-4" />
                        View Docs
                      </Button>
                        </div>
                      </Card.Header>

                      <Card.Body className="space-y-3">
                        <h4 className="text-label-caps text-on-surface-variant">Compliance Documents</h4>

                        {complianceDocs.length === 0 ? (
                          <p className="text-body-sm italic text-slate-500">
                            No documents uploaded yet â€” this agency cannot be verified until its
                            mandatory documents are submitted.
                          </p>
                        ) : (
                          <div className="overflow-x-auto">
                            <table className="w-full text-left text-body-sm">
                              <thead className="text-on-surface-variant">
                                <tr>
                                  <th className="px-3 py-2 font-medium">Type</th>
                                  <th className="px-3 py-2 font-medium">Doc Number</th>
                                  <th className="px-3 py-2 font-medium">Expires On</th>
                                  <th className="px-3 py-2 font-medium">Status</th>
                                  <th className="px-3 py-2 font-medium text-right">Action</th>
                                </tr>
                              </thead>
                              <tbody className="divide-y divide-slate-border">
                                {complianceDocs.map((doc) => {
                                  const isThisDocPending = (mutation) =>
                                    mutation.isPending &&
                                    mutation.variables?.docId === doc.complianceDocId

                                  const isVerifyingThis = isThisDocPending(verifyDocMutation)
                                  const isRejectingThis = isThisDocPending(rejectDocMutation)
                                  const isUpdatingThis = isVerifyingThis || isRejectingThis
                                  const isActioned = actionedDocIds.has(doc.complianceDocId)
                                  const canAction = doc.status === ComplianceDocStatus.PENDING && !isActioned

                                  return (
                                    <tr key={doc.complianceDocId}>
                                      <td className="px-3 py-2.5 font-medium text-slate-900">
                                        {getComplianceDocTypeLabel(doc.docType)}
                                      </td>
                                      <td className="px-3 py-2.5 text-slate-600">{doc.docNumber}</td>
                                      <td className="px-3 py-2.5 text-slate-600">{doc.expiresOn ?? 'N/A'}</td>
                                      <td className="px-3 py-2.5">
                                        <StatusBadge tone={getComplianceDocStatusTone(doc.status)}>
                                          {doc.status}
                                        </StatusBadge>
                                      </td>
                                      <td className="px-3 py-2.5">
                                        <div className="flex items-center justify-end gap-2">
                                          {doc.storageKey && (
                                            <a
                                              href={getFileUrl(doc.storageKey)}
                                              target="popup"
                                              rel="noreferrer"
                                              onClick={(e) => {
                                                const url = getFileUrl(doc.storageKey)
                                                const popup = window.open(
                                                  url,
                                                  'popup',
                                                  'width=900,height=800,scrollbars=yes,resizable=yes',
                                                )
                                                if (popup) {
                                                  e.preventDefault()
                                                }
                                              }}
                                              className="inline-flex items-center gap-1 font-medium text-primary hover:underline"
                                            >
                                              View <ExternalLink className="h-3.5 w-3.5" />
                                            </a>
                                          )}

                                          {canAction ? (
                                            <>
                                              <Button
                                                variant="status"
                                                status="green"
                                                className="!px-2.5 !py-1 text-body-xs"
                                                onClick={() =>
                                                  handleVerifyDoc(agency.agencyId, doc.complianceDocId)
                                                }
                                              >
                                                <CheckCircle2 className="h-3.5 w-3.5" />
                                                Verify
                                              </Button>
                                              <Button
                                                variant="status"
                                                status="red"
                                                className="!px-2.5 !py-1 text-body-xs"
                                                onClick={() =>
                                                  handleRejectDoc(agency.agencyId, doc.complianceDocId)
                                                }
                                              >
                                                <XCircle className="h-3.5 w-3.5" />
                                                Reject
                                              </Button>
                                            </>
                                          ) : isUpdatingThis ? (
                                            <span className="inline-flex items-center gap-1.5 text-body-xs text-slate-500">
                                              <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" />
                                              Updatingâ€¦
                                            </span>
                                          ) : null}
                                        </div>
                                      </td>
                                    </tr>
                                  )
                                })}
                              </tbody>
                            </table>
                          </div>
                        )}
                      </Card.Body>

                      <Card.Footer className="flex items-center justify-end gap-3 bg-slate-50/50">
                        <Button
                          variant="status"
                          status="red"
                          disabled={agencyActionPending}
                          onClick={() => suspendMutation.mutate(agency.agencyId)}
                        >
                          {suspendMutation.isPending && suspendMutation.variables === agency.agencyId
                            ? 'Suspendingâ€¦'
                            : 'Suspend Agency'}
                        </Button>
                        <Button
                          variant="status"
                          status="amber"
                          disabled={agencyActionPending}
                          onClick={() => verifyAgencyMutation.mutate(agency.agencyId)}
                        >
                          {verifyAgencyMutation.isPending && verifyAgencyMutation.variables === agency.agencyId
                            ? 'Verifyingâ€¦'
                            : 'Verify Agency'}
                        </Button>
                      </Card.Footer>
                    </Card>
                  )
                })}
              </div>
            )}
          </section>

          <section className="space-y-4">
            <h2 className="text-title-md font-bold text-on-surface">
              Awaiting Activation
              <span className="ml-2 text-body-sm font-normal text-on-surface-variant">
                ({readyToActivate.length})
              </span>
            </h2>

            {readyToActivate.length === 0 ? (
              <Card className="p-6 text-center text-body-sm text-on-surface-variant">
                No verified agencies are currently awaiting activation.
              </Card>
            ) : (
              <div className="grid gap-4">
                {readyToActivate.map((agency) => (
                  <Card key={agency.agencyId} className="flex items-center justify-between gap-4">
                    <div>
                      <h3 className="text-title-sm font-bold text-slate-900">{agency.name}</h3>
                      <p className="text-body-xs text-slate-500">Reg No: {agency.businessRegNo}</p>
                    </div>
                    <div className="flex items-center gap-3">
                      <StatusBadge tone={getAgencyStatusTone(agency.status)}>{agency.status}</StatusBadge>
                      <Button
                        variant="secondary"
                        onClick={() => setSelectedAgencyForDocs(agency)}
                      >
                        <ExternalLink className="mr-1.5 h-4 w-4" />
                        View Docs
                      </Button>
                      <Button
                        variant="status"
                        status="red"
                        disabled={agencyActionPending}
                        onClick={() => suspendMutation.mutate(agency.agencyId)}
                      >
                        {suspendMutation.isPending && suspendMutation.variables === agency.agencyId
                          ? 'Suspendingâ€¦'
                          : 'Suspend'}
                      </Button>
                      <Button
                        variant="status"
                        status="green"
                        disabled={agencyActionPending}
                        onClick={() => activateMutation.mutate(agency.agencyId)}
                      >
                        {activateMutation.isPending && activateMutation.variables === agency.agencyId
                          ? 'Activatingâ€¦'
                          : 'Activate Agency'}
                      </Button>
                    </div>
                  </Card>
                ))}
              </div>
            )}
          </section>
        </>
      )}
          <ViewComplianceDocsModal
        isOpen={!!selectedAgencyForDocs}
        onClose={() => setSelectedAgencyForDocs(null)}
        agencyId={selectedAgencyForDocs?.agencyId}
        agencyName={selectedAgencyForDocs?.name}
      />
    </div>
  )
}
