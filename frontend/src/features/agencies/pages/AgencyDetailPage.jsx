import { useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { ArrowLeft, Building2, CheckCircle2, ExternalLink, FileText, Loader2, MapPin, XCircle } from 'lucide-react'
import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { getFileUrl } from '../../../lib/api/fileUrl.js'
import { AgencyStatus, ComplianceDocStatus, UserRole } from '../../../lib/enums.js'
import {
  useAgencyFleetQuery,
  useAgencyQuery,
  useAgencyStatusHistoryQuery,
  useComplianceDocsQuery,
  useRejectComplianceDocMutation,
  useVerifyComplianceDocMutation,
} from '../api/agencyApi.js'
import AgencyStatusDialog from '../components/AgencyStatusDialog.jsx'
import { canTransitionAgency } from '../lib/agencyStatusTransitions.js'
import { COMPLIANCE_DOC_TYPES, getComplianceDocTypeLabel } from '../lib/complianceDocTypes.js'
import { getAgencyStatusTone, getComplianceDocStatusTone } from '../lib/statusTone.js'

const MANDATORY_DOC_TYPE_IDS = COMPLIANCE_DOC_TYPES.filter((t) => t.mandatory).map((t) => t.id)

function formatDate(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleDateString()
}

function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString()
}

function openFilePopup(event, storageKey) {
  const popup = window.open(
    getFileUrl(storageKey),
    'popup',
    'width=900,height=800,scrollbars=yes,resizable=yes',
  )
  if (popup) event.preventDefault()
}

function SectionMessage({ children }) {
  return <p className="px-6 py-8 text-center text-body-sm text-on-surface-variant">{children}</p>
}

// Admin-only read view of one agency: profile, every compliance document (including the verified
// ones, with a link to the stored file), fleet, and the status audit trail.
export default function AgencyDetailPage() {
  const role = useAppSelector((state) => state.auth.role)
  const { agencyId } = useParams()
  const isAdmin = role === UserRole.ADMIN

  const agencyQuery = useAgencyQuery(agencyId, { enabled: isAdmin })
  const docsQuery = useComplianceDocsQuery(agencyId, { enabled: isAdmin })
  const fleetQuery = useAgencyFleetQuery(agencyId, { enabled: isAdmin })
  const historyQuery = useAgencyStatusHistoryQuery(agencyId, { enabled: isAdmin })

  const verifyDocMutation = useVerifyComplianceDocMutation({
    onSuccess: () => toast.success('Document verified.'),
    onError: (err) => toast.error(`Failed to verify document: ${err.message}`),
  })
  const rejectDocMutation = useRejectComplianceDocMutation({
    onSuccess: () => toast.success('Document rejected.'),
    onError: (err) => toast.error(`Failed to reject document: ${err.message}`),
  })
  const reviewPending = verifyDocMutation.isPending || rejectDocMutation.isPending

  // The AgenciesPage-level role guard doesn't cover this nested route; ROLE_ALLOWED_PREFIXES lets
  // AgencyStaff reach every /agencies/* path, so this page enforces Admin itself.
  const [statusChange, setStatusChange] = useState(null)
  const [reviewedDocId, setReviewedDocId] = useState(null)
  if (!isAdmin) {
    return <Navigate to="/unauthorized" replace />
  }

  const agency = agencyQuery.data
  const docs = docsQuery.data ?? []
  const vehicles = fleetQuery.data?.vehicles ?? []
  const drivers = fleetQuery.data?.drivers ?? []
  const history = historyQuery.data ?? []

  const verifiedMandatory = MANDATORY_DOC_TYPE_IDS.filter((typeId) =>
    docs.some((d) => d.docType === typeId && d.status === ComplianceDocStatus.VERIFIED),
  ).length

  if (agencyQuery.isLoading) {
    return <p className="p-8 text-center text-slate-500">Loading agency…</p>
  }

  if (agencyQuery.isError || !agency) {
    return (
      <div className="space-y-4">
        <Link to="/agencies" className="inline-flex items-center gap-1 text-body-sm text-primary hover:underline">
          <ArrowLeft className="h-4 w-4" /> Back to agencies
        </Link>
        <Card className="p-6 text-center text-body-sm text-status-red-text">
          {agencyQuery.error?.status === 404
            ? 'This agency could not be found.'
            : 'Failed to load this agency. Please try again.'}
        </Card>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <Link to="/agencies" className="inline-flex items-center gap-1 text-body-sm text-primary hover:underline">
        <ArrowLeft className="h-4 w-4" /> Back to agencies
      </Link>

      <PageHeader
        title={agency.name}
        description={`Reg No: ${agency.businessRegNo || '—'}`}
        actions={
          <>
            <StatusBadge tone={getAgencyStatusTone(agency.status)}>{agency.status}</StatusBadge>
            {canTransitionAgency(agency.status, AgencyStatus.SUSPENDED) && (
              <Button
                variant="status"
                status="red"
                onClick={() => setStatusChange(AgencyStatus.SUSPENDED)}
              >
                Suspend
              </Button>
            )}
            {agency.status === AgencyStatus.SUSPENDED && (
              <Button
                variant="status"
                status="green"
                onClick={() => setStatusChange(AgencyStatus.ACTIVE)}
              >
                Reactivate
              </Button>
            )}
          </>
        }
      />

      {/* Profile */}
      <Card className="p-6">
        <div className="flex items-start gap-4">
          <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
            <Building2 className="h-7 w-7" />
          </div>
          <dl className="grid flex-1 grid-cols-1 gap-x-8 gap-y-3 text-body-sm sm:grid-cols-2 lg:grid-cols-3">
            <div>
              <dt className="text-label-caps text-on-surface-variant">Yard address</dt>
              <dd className="mt-0.5 flex items-center gap-1 text-slate-800">
                <MapPin className="h-3.5 w-3.5 shrink-0 text-slate-400" />
                {agency.yardAddress || 'Not set'}
              </dd>
            </div>
            <div>
              <dt className="text-label-caps text-on-surface-variant">Yard coordinates</dt>
              <dd className="mt-0.5 font-mono text-slate-800">
                {agency.yardLat != null && agency.yardLng != null
                  ? `${Number(agency.yardLat).toFixed(4)}, ${Number(agency.yardLng).toFixed(4)}`
                  : '—'}
              </dd>
            </div>
            <div>
              <dt className="text-label-caps text-on-surface-variant">Registered</dt>
              <dd className="mt-0.5 text-slate-800">{formatDate(agency.createdAt)}</dd>
            </div>
            <div>
              <dt className="text-label-caps text-on-surface-variant">Drivers</dt>
              <dd className="mt-0.5 text-slate-800">
                {agency.driverCount ?? 0} ({agency.activeDriverCount ?? 0} active)
              </dd>
            </div>
            <div>
              <dt className="text-label-caps text-on-surface-variant">Vehicles</dt>
              <dd className="mt-0.5 text-slate-800">{agency.vehicleCount ?? 0}</dd>
            </div>
            <div>
              <dt className="text-label-caps text-on-surface-variant">Verified mandatory documents</dt>
              <dd className="mt-0.5 font-semibold text-status-green-text">
                {verifiedMandatory} / {MANDATORY_DOC_TYPE_IDS.length}
              </dd>
            </div>
          </dl>
        </div>
      </Card>

      {/* Compliance documents */}
      <Card className="overflow-hidden p-0">
        <Card.Header>
          <h2 className="text-title-md font-bold text-on-surface">Compliance documents</h2>
        </Card.Header>
        {docsQuery.isLoading ? (
          <SectionMessage>Loading documents…</SectionMessage>
        ) : docsQuery.isError ? (
          <SectionMessage>Failed to load compliance documents.</SectionMessage>
        ) : docs.length === 0 ? (
          <SectionMessage>This agency hasn't uploaded any compliance documents yet.</SectionMessage>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-body-sm">
              <thead className="border-b border-slate-border text-on-surface-variant">
                <tr>
                  <th className="px-4 py-2 font-medium">Type</th>
                  <th className="px-4 py-2 font-medium">Doc number</th>
                  <th className="px-4 py-2 font-medium">Issued</th>
                  <th className="px-4 py-2 font-medium">Expires</th>
                  <th className="px-4 py-2 font-medium">Status</th>
                  <th className="px-4 py-2 text-right font-medium">File</th>
                  <th className="px-4 py-2 text-right font-medium">Review</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-border">
                {docs.map((doc) => (
                  <tr key={doc.complianceDocId}>
                    <td className="px-4 py-2.5 font-medium text-slate-900">
                      {getComplianceDocTypeLabel(doc.docType)}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-xs text-slate-600">{doc.docNumber || '—'}</td>
                    <td className="px-4 py-2.5 text-slate-600">{formatDate(doc.issuedOn)}</td>
                    <td className="px-4 py-2.5 text-slate-600">{formatDate(doc.expiresOn)}</td>
                    <td className="px-4 py-2.5">
                      <StatusBadge tone={getComplianceDocStatusTone(doc.status)}>{doc.status}</StatusBadge>
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      {doc.storageKey ? (
                        <a
                          href={getFileUrl(doc.storageKey)}
                          target="popup"
                          rel="noreferrer"
                          onClick={(e) => openFilePopup(e, doc.storageKey)}
                          className="inline-flex items-center gap-1 font-medium text-primary hover:underline"
                        >
                          <FileText className="h-3.5 w-3.5" /> View <ExternalLink className="h-3 w-3" />
                        </a>
                      ) : (
                        '—'
                      )}
                    </td>
                    <td className="px-4 py-2.5">
                      <div className="flex items-center justify-end gap-2">
                        {doc.status === ComplianceDocStatus.PENDING ? (
                          reviewPending && reviewedDocId === doc.complianceDocId ? (
                            <span className="inline-flex items-center gap-1.5 text-body-xs text-slate-500">
                              <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" />
                              Updating…
                            </span>
                          ) : (
                            <>
                              <Button
                                variant="status"
                                status="green"
                                className="!px-2.5 !py-1 text-body-xs"
                                disabled={reviewPending}
                                onClick={() => {
                                  setReviewedDocId(doc.complianceDocId)
                                  verifyDocMutation.mutate({ agencyId, docId: doc.complianceDocId })
                                }}
                              >
                                <CheckCircle2 className="h-3.5 w-3.5" />
                                Verify
                              </Button>
                              <Button
                                variant="status"
                                status="red"
                                className="!px-2.5 !py-1 text-body-xs"
                                disabled={reviewPending}
                                onClick={() => {
                                  setReviewedDocId(doc.complianceDocId)
                                  rejectDocMutation.mutate({ agencyId, docId: doc.complianceDocId })
                                }}
                              >
                                <XCircle className="h-3.5 w-3.5" />
                                Reject
                              </Button>
                            </>
                          )
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* Fleet */}
      <div className="grid gap-6 lg:grid-cols-2">
        <Card className="overflow-hidden p-0">
          <Card.Header>
            <h2 className="text-title-md font-bold text-on-surface">Vehicles ({vehicles.length})</h2>
          </Card.Header>
          {fleetQuery.isLoading ? (
            <SectionMessage>Loading fleet…</SectionMessage>
          ) : fleetQuery.isError ? (
            <SectionMessage>Failed to load the fleet.</SectionMessage>
          ) : vehicles.length === 0 ? (
            <SectionMessage>No vehicles registered.</SectionMessage>
          ) : (
            <ul className="divide-y divide-slate-border">
              {vehicles.map((v) => (
                <li key={v.vehicleId} className="flex items-center justify-between gap-3 px-4 py-3 text-body-sm">
                  <div>
                    <p className="font-mono font-medium text-slate-900">{v.registrationNo}</p>
                    <p className="text-xs text-slate-500">
                      {v.vehicleType} · {v.capacityKg} kg · {v.volumeM3} m³
                    </p>
                  </div>
                  <StatusBadge tone={v.status === 'Available' ? 'green' : 'amber'}>{v.status}</StatusBadge>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card className="overflow-hidden p-0">
          <Card.Header>
            <h2 className="text-title-md font-bold text-on-surface">Drivers ({drivers.length})</h2>
          </Card.Header>
          {fleetQuery.isLoading ? (
            <SectionMessage>Loading fleet…</SectionMessage>
          ) : fleetQuery.isError ? (
            <SectionMessage>Failed to load the fleet.</SectionMessage>
          ) : drivers.length === 0 ? (
            <SectionMessage>No drivers registered.</SectionMessage>
          ) : (
            <ul className="divide-y divide-slate-border">
              {drivers.map((d) => (
                <li key={d.driverId} className="flex items-center justify-between gap-3 px-4 py-3 text-body-sm">
                  <div>
                    <p className="font-medium text-slate-900">{d.fullName}</p>
                    <p className="text-xs text-slate-500">
                      {d.email} · Licence {d.licenceNo} (exp. {formatDate(d.licenceExpiry)})
                    </p>
                  </div>
                  <StatusBadge tone={d.status === 'Active' ? 'green' : 'neutral'}>{d.status}</StatusBadge>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      {/* Status history */}
      <Card className="overflow-hidden p-0">
        <Card.Header>
          <h2 className="text-title-md font-bold text-on-surface">Status history</h2>
        </Card.Header>
        {historyQuery.isLoading ? (
          <SectionMessage>Loading history…</SectionMessage>
        ) : historyQuery.isError ? (
          <SectionMessage>Failed to load the status history.</SectionMessage>
        ) : history.length === 0 ? (
          <SectionMessage>No status changes recorded.</SectionMessage>
        ) : (
          <ul className="divide-y divide-slate-border">
            {history.map((h) => (
              <li key={h.agencyStatusHistoryId} className="space-y-1 px-4 py-3 text-body-sm">
                <div className="flex flex-wrap items-center gap-2">
                  {h.fromStatus && (
                    <>
                      <StatusBadge tone={getAgencyStatusTone(h.fromStatus)}>{h.fromStatus}</StatusBadge>
                      <span aria-hidden="true">→</span>
                    </>
                  )}
                  <StatusBadge tone={getAgencyStatusTone(h.toStatus)}>{h.toStatus}</StatusBadge>
                  <span className="text-xs text-slate-500">
                    {formatDateTime(h.changedAt)} · {h.changedByName || 'Unknown user'}
                  </span>
                </div>
                {h.reason && <p className="text-slate-600">{h.reason}</p>}
              </li>
            ))}
          </ul>
        )}
      </Card>

      {statusChange && (
        <AgencyStatusDialog agency={agency} targetStatus={statusChange} onClose={() => setStatusChange(null)} />
      )}
    </div>
  )
}
