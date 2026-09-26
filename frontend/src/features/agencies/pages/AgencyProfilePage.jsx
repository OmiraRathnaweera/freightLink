import { useMemo, useState } from 'react'
import {
  Building2,
  CheckCircle2,
  Clock,
  ExternalLink,
  FileCheck2,
  FileText,
  MapPin,
  ShieldCheck,
  Truck,
  UploadCloud,
} from 'lucide-react'
import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import { useCurrentUserQuery } from '../../auth/api/authApi.js'
import {
  useAgencyQuery,
  useComplianceDocsQuery,
} from '../api/agencyApi.js'
import UploadComplianceDocModal from '../components/UploadComplianceDocModal.jsx'

const COMPLIANCE_DOC_TYPES = [
  {
    id: 'BusinessRegistration',
    title: 'Business Registration',
    shortTitle: 'Business Reg',
    description: 'Certificate of Incorporation or Business Registration license.',
    icon: Building2,
    mandatory: true,
  },
  {
    id: 'VehicleInsurance',
    title: 'Fleet Insurance Policy',
    shortTitle: 'Insurance',
    description: 'Comprehensive commercial vehicle & goods-in-transit liability coverage.',
    icon: ShieldCheck,
    mandatory: true,
  },
  {
    id: 'RevenueLicence',
    title: 'Revenue Licence',
    shortTitle: 'Revenue Licence',
    description: 'Annual valid commercial motor traffic revenue license permit.',
    icon: FileCheck2,
    mandatory: true,
  },
  {
    id: 'GoodsTransportPermit',
    title: 'Goods Transport Permit',
    shortTitle: 'Transport Permit',
    description: 'Provincial / national freight transportation and carriage permit.',
    icon: Truck,
    mandatory: true,
  },
  {
    id: 'Other',
    title: 'Additional Documentation',
    shortTitle: 'Additional Doc',
    description: 'Tax clearance certificates, environmental permits, or safety records.',
    icon: FileText,
    mandatory: false,
  },
]

export default function AgencyProfilePage() {
  const { data: user, isLoading: userLoading } = useCurrentUserQuery()
  const agencyId = user?.agencyId

  const { data: agency } = useAgencyQuery(agencyId, {
    enabled: Boolean(agencyId),
  })

  const {
    data: docs = [],
    isLoading: docsLoading,
  } = useComplianceDocsQuery(agencyId, {
    enabled: Boolean(agencyId),
  })

  // State for upload document modal
  const [activeUploadDocType, setActiveUploadDocType] = useState(null)
  const [activeExistingDoc, setActiveExistingDoc] = useState(null)

  // Compliance calculations
  const complianceStats = useMemo(() => {
    const mandatoryTypes = COMPLIANCE_DOC_TYPES.filter((t) => t.mandatory).map((t) => t.id)
    const uploadedMandatory = mandatoryTypes.filter((typeId) =>
      docs.some((d) => d.docType === typeId),
    ).length
    const verifiedMandatory = mandatoryTypes.filter((typeId) =>
      docs.some((d) => d.docType === typeId && (d.status === 'Verified' || d.status === 'Valid')),
    ).length

    return {
      totalMandatory: mandatoryTypes.length,
      uploadedCount: uploadedMandatory,
      verifiedCount: verifiedMandatory,
      isFullyUploaded: uploadedMandatory === mandatoryTypes.length,
    }
  }, [docs])

  if (userLoading) {
    return <div className="p-8 text-center text-slate-500">Loading agency profile…</div>
  }

  if (!agencyId) {
    return (
      <div className="p-8 text-center text-on-surface-variant">
        Agency profile not found. Please log in with an authorized Agency Staff account.
      </div>
    )
  }

  function handleOpenUpload(docType, existingDoc = null) {
    setActiveUploadDocType(docType)
    setActiveExistingDoc(existingDoc)
  }

  function handleCloseUpload() {
    setActiveUploadDocType(null)
    setActiveExistingDoc(null)
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Agency Profile"
        subtitle="Manage compliance credentials and track regulatory verification status."
      />

      {/* Agency Identity & Overview Banner */}
      <Card className="p-6">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex items-start gap-4">
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Building2 className="h-7 w-7" />
            </div>
            <div className="space-y-1">
              <div className="flex flex-wrap items-center gap-2.5">
                <h2 className="text-headline-md font-bold text-slate-900">
                  {agency?.name || user?.agencyName || user?.name || 'Registered Agency'}
                </h2>
                <StatusBadge
                  tone={
                    agency?.status === 'Active'
                      ? 'green'
                      : agency?.status === 'Verified'
                        ? 'blue'
                        : agency?.status === 'Pending'
                          ? 'amber'
                          : 'neutral'
                  }
                >
                  {agency?.status || 'Active Member'}
                </StatusBadge>
              </div>

              <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-body-xs text-on-surface-variant">
                <span>
                  Reg No:{' '}
                  <strong className="font-mono text-slate-700">
                    {agency?.businessRegNo || user?.businessRegNo || 'BR-PENDING'}
                  </strong>
                </span>

                {(agency?.yardAddress || user?.yardAddress) && (
                  <span className="inline-flex items-center gap-1 text-slate-600">
                    <MapPin className="h-3.5 w-3.5 text-slate-400" />
                    <span>{agency?.yardAddress || user?.yardAddress}</span>
                  </span>
                )}
              </div>
            </div>
          </div>

          {/* Quick Compliance Metrics */}
          <div className="flex flex-wrap items-center gap-3 border-t border-slate-border pt-4 sm:gap-4 lg:border-t-0 lg:pt-0">
            <div className="rounded-lg bg-slate-50 px-4 py-2.5 border border-slate-border">
              <p className="text-body-xs font-medium text-slate-500">Submitted Documents</p>
              <div className="mt-0.5 flex items-center gap-2">
                <span className="text-title-md font-bold text-slate-900">
                  {complianceStats.uploadedCount} / {complianceStats.totalMandatory}
                </span>
                {complianceStats.isFullyUploaded ? (
                  <CheckCircle2 className="h-4 w-4 text-status-green-text" />
                ) : (
                  <Clock className="h-4 w-4 text-status-amber-text" />
                )}
              </div>
            </div>

            <div className="rounded-lg bg-slate-50 px-4 py-2.5 border border-slate-border">
              <p className="text-body-xs font-medium text-slate-500">Verified Documents</p>
              <div className="mt-0.5 flex items-center gap-2">
                <span className="text-title-md font-bold text-status-green-text">
                  {complianceStats.verifiedCount} / {complianceStats.totalMandatory}
                </span>
              </div>
            </div>
          </div>
        </div>
      </Card>

      {/* Main Section: Compliance Documents */}
      <div className="space-y-6">
        {/* Compliance Section Header */}
        <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 className="text-headline-sm font-bold text-on-surface">Compliance Documents</h3>
            <p className="text-body-sm text-on-surface-variant">
              Upload credentials and regulatory documentation. Each document is verified for freight dispatch approval.
            </p>
          </div>

          <div className="text-body-xs font-medium text-slate-500">
            Mandatory Documents:{' '}
            <span
              className={
                complianceStats.isFullyUploaded
                  ? 'font-bold text-status-green-text'
                  : 'font-bold text-status-amber-text'
              }
            >
              {complianceStats.uploadedCount} of {complianceStats.totalMandatory} Submitted
            </span>
          </div>
        </div>

        {/* Compliance Cards Grid */}
        {docsLoading ? (
          <div className="p-8 text-center text-slate-500">Loading compliance documents…</div>
        ) : (
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-3">
            {COMPLIANCE_DOC_TYPES.map((docType) => {
              const Icon = docType.icon
              const matchingDoc = docs?.find((d) => d.docType === docType.id)
              const isUploaded = Boolean(matchingDoc)

              return (
                <Card
                  key={docType.id}
                  className="flex flex-col justify-between border-slate-border transition-shadow hover:shadow-md"
                >
                  <div>
                    {/* Card Header */}
                    <div className="flex items-start justify-between p-5 pb-3">
                      <div className="flex items-center gap-3">
                        <div
                          className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${
                            isUploaded ? 'bg-primary/10 text-primary' : 'bg-slate-100 text-slate-500'
                          }`}
                        >
                          <Icon className="h-5 w-5" />
                        </div>
                        <div>
                          <h4 className="text-title-sm font-bold text-slate-900">
                            {docType.title}
                          </h4>
                          <div className="flex items-center gap-2 mt-0.5">
                            {docType.mandatory ? (
                              <span className="text-[11px] font-semibold uppercase tracking-wider text-primary">
                                Mandatory
                              </span>
                            ) : (
                              <span className="text-[11px] font-medium text-slate-500">
                                Optional
                              </span>
                            )}
                          </div>
                        </div>
                      </div>

                      <StatusBadge
                        tone={
                          !isUploaded
                            ? 'neutral'
                            : matchingDoc.status === 'Verified' || matchingDoc.status === 'Valid'
                              ? 'green'
                              : matchingDoc.status === 'Pending'
                                ? 'amber'
                                : 'red'
                        }
                      >
                        {!isUploaded
                          ? 'Not Uploaded'
                          : matchingDoc.status || 'Pending'}
                      </StatusBadge>
                    </div>

                    {/* Card Body */}
                    <div className="px-5 py-2 text-body-xs text-slate-600">
                      <p className="leading-relaxed">{docType.description}</p>

                      {isUploaded ? (
                        <div className="mt-3 rounded-lg border border-slate-border bg-slate-50 p-3 space-y-1.5">
                          <div className="flex items-center justify-between text-body-xs">
                            <span className="text-slate-500">Doc Number:</span>
                            <span className="font-mono font-bold text-slate-800">
                              {matchingDoc.docNumber}
                            </span>
                          </div>

                          {(matchingDoc.issuedOn || matchingDoc.createdAt) && (
                            <div className="flex items-center justify-between text-[11px] text-slate-500">
                              <span>Uploaded On:</span>
                              <span>
                                {new Date(
                                  matchingDoc.issuedOn || matchingDoc.createdAt,
                                ).toLocaleDateString()}
                              </span>
                            </div>
                          )}

                          {(matchingDoc.storageKey || matchingDoc.publicId) && (
                            <div className="pt-1 text-right">
                              <a
                                href={matchingDoc.storageKey || `#`}
                                target="_blank"
                                rel="noreferrer"
                                className="inline-flex items-center gap-1 text-[11px] font-semibold text-primary hover:underline"
                              >
                                <span>View File</span>
                                <ExternalLink className="h-3 w-3" />
                              </a>
                            </div>
                          )}
                        </div>
                      ) : (
                        <div className="mt-3 rounded-lg border border-dashed border-slate-300 bg-slate-50/50 p-3 text-center text-body-xs text-slate-400">
                          No file uploaded yet.
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Card Action */}
                  <div className="border-t border-slate-border p-4 bg-slate-50/30">
                    <Button
                      variant={isUploaded ? 'secondary' : 'primary'}
                      className="w-full inline-flex items-center justify-center gap-2 text-body-xs font-semibold"
                      onClick={() => handleOpenUpload(docType, matchingDoc)}
                    >
                      <UploadCloud className="h-4 w-4" />
                      <span>{isUploaded ? 'Replace / Update' : 'Upload Document'}</span>
                    </Button>
                  </div>
                </Card>
              )
            })}
          </div>
        )}
      </div>

      {/* Upload Document Modal with Dropzone */}
      <UploadComplianceDocModal
        isOpen={Boolean(activeUploadDocType)}
        onClose={handleCloseUpload}
        agencyId={agencyId}
        docTypeInfo={activeUploadDocType}
        existingDoc={activeExistingDoc}
      />
    </div>
  )
}
