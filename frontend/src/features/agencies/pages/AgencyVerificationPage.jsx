import { Navigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { UserRole } from '../../../lib/enums.js'
import { useAgenciesQuery, useVerifyAgencyMutation, useSuspendAgencyMutation, useActivateAgencyMutation } from '../api/agencyApi.js'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'

export default function AgencyVerificationPage() {
  const role = useAppSelector((state) => state.auth.role)
  const queryClient = useQueryClient()

  const pendingQuery = useAgenciesQuery({ status: 'Pending' })
  const verifiedQuery = useAgenciesQuery({ status: 'Verified' })

  const isLoading = pendingQuery.isLoading || verifiedQuery.isLoading
  const isError = pendingQuery.isError || verifiedQuery.isError

  const agencies = [
    ...(pendingQuery.data?.items || []),
    ...(verifiedQuery.data?.items || [])
  ]

  const verifyMutation = useVerifyAgencyMutation({
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['agencies'] })
      alert('Agency successfully verified!')
    },
    onError: (err) => {
      alert(`Failed to verify agency: ${err.message}`)
    }
  })

  const activateMutation = useActivateAgencyMutation({
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['agencies'] })
      alert('Agency successfully activated!')
    },
    onError: (err) => {
      alert(`Failed to activate agency: ${err.message}`)
    }
  })

  const suspendMutation = useSuspendAgencyMutation({
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['agencies'] })
      alert('Agency successfully suspended!')
    },
    onError: (err) => {
      alert(`Failed to suspend agency: ${err.message}`)
    }
  })

  const handleApprove = (agencyId) => {
    verifyMutation.mutate(agencyId)
  }

  const handleActivate = (agencyId) => {
    activateMutation.mutate(agencyId)
  }

  const handleSuspend = (agencyId) => {
    suspendMutation.mutate(agencyId)
  }

  // Enforce Admin-only access
  if (role !== UserRole.ADMIN) {
    return <Navigate to="/unauthorized" replace />
  }

  return (
    <div className="space-y-6">
      <PageHeader 
        title="Agency Verification Queue" 
        subtitle="Review, approve, and activate agencies." 
      />

      {isLoading && <p className="text-slate-500">Loading agencies...</p>}
      {isError && <p className="text-red-500">Failed to load agencies.</p>}

      {!isLoading && !isError && agencies.length === 0 ? (
        <p className="text-slate-500">No agencies pending verification or activation.</p>
      ) : (
        <div className="grid gap-6">
          {agencies.map((agency) => (
            <Card key={agency.agencyId} className="p-0 overflow-hidden">
              <Card.Header className="flex items-center justify-between border-b border-slate-200 bg-slate-50/50 p-4">
                <div>
                  <h3 className="text-lg font-semibold text-slate-900">{agency.name}</h3>
                  <p className="text-sm text-slate-500">Reg No: {agency.businessRegNo}</p>
                </div>
                <StatusBadge tone={agency.status === 'Pending' ? 'amber' : agency.status === 'Verified' ? 'blue' : 'neutral'}>{agency.status}</StatusBadge>
              </Card.Header>
              
              <Card.Body className="p-4 space-y-4">
                <h4 className="text-sm font-medium text-slate-700 uppercase tracking-wider">Compliance Documents</h4>
                {!agency.complianceDocs || agency.complianceDocs.length === 0 ? (
                  <p className="text-sm text-slate-500 italic">No documents uploaded.</p>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm text-left">
                      <thead className="bg-slate-50 text-slate-600">
                        <tr>
                          <th className="px-4 py-2 font-medium">Type</th>
                          <th className="px-4 py-2 font-medium">Doc Number</th>
                          <th className="px-4 py-2 font-medium">Expires On</th>
                          <th className="px-4 py-2 font-medium">Status</th>
                          <th className="px-4 py-2 font-medium text-right">Action</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {agency.complianceDocs.map((doc) => (
                          <tr key={doc.complianceDocId} className="hover:bg-slate-50/50">
                            <td className="px-4 py-3 text-slate-900 font-medium">{doc.docType}</td>
                            <td className="px-4 py-3 text-slate-600">{doc.docNumber}</td>
                            <td className="px-4 py-3 text-slate-600">{doc.expiresOn ?? 'N/A'}</td>
                            <td className="px-4 py-3">
                              <StatusBadge tone={doc.status === 'Valid' ? 'green' : doc.status === 'Expiring' ? 'amber' : 'red'}>{doc.status}</StatusBadge>
                            </td>
                            <td className="px-4 py-3 text-right">
                              <a
                                href={doc.storageKey}
                                target="_blank"
                                rel="noreferrer"
                                className="text-primary hover:underline font-medium"
                              >
                                View Document &nearr;
                              </a>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </Card.Body>

              <Card.Footer className="flex items-center justify-end gap-3 border-t border-slate-200 p-4 bg-slate-50/50">
                <Button 
                  variant="status" 
                  status="red" 
                  onClick={() => handleSuspend(agency.agencyId)}
                  disabled={suspendMutation.isPending || verifyMutation.isPending || activateMutation.isPending}
                >
                  {suspendMutation.isPending && suspendMutation.variables === agency.agencyId ? 'Suspending...' : 'Suspend Agency'}
                </Button>
                
                {agency.status === 'Pending' && (
                  <Button 
                    variant="status" 
                    status="yellow" 
                    onClick={() => handleApprove(agency.agencyId)}
                    disabled={suspendMutation.isPending || verifyMutation.isPending || activateMutation.isPending}
                  >
                    {verifyMutation.isPending && verifyMutation.variables === agency.agencyId ? 'Approving...' : 'Approve Agency'}
                  </Button>
                )}
                
                {agency.status === 'Verified' && (
                  <Button 
                    variant="status" 
                    status="green" 
                    onClick={() => handleActivate(agency.agencyId)}
                    disabled={suspendMutation.isPending || verifyMutation.isPending || activateMutation.isPending}
                  >
                    {activateMutation.isPending && activateMutation.variables === agency.agencyId ? 'Activating...' : 'Activate Agency'}
                  </Button>
                )}
              </Card.Footer>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}


