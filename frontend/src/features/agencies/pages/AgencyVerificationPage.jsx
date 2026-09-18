import { Navigate } from 'react-router-dom'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { UserRole } from '../../../lib/enums.js'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'

// Hardcoded mock data to fulfill UI-only Sprint 3 requirements.
// The real backend wiring (Y3S01-74, 77) happens in Sprint 4.
const mockAgencies = [
  {
    agencyId: 'a1',
    name: 'Fast Track Logistics',
    businessRegNo: 'BR-901234',
    status: 'Pending',
    complianceDocs: [
      {
        complianceDocId: 'd1',
        docType: 'Verification',
        docNumber: 'V-10023',
        storageKey: 'https://placehold.co/600x400/png?text=Verification+Doc+1',
        status: 'Pending',
        expiresOn: '2027-01-01',
      },
    ],
  },
  {
    agencyId: 'a2',
    name: 'Lanka Freight Movers',
    businessRegNo: 'BR-882119',
    status: 'Pending',
    complianceDocs: [
      {
        complianceDocId: 'd2',
        docType: 'Verification',
        docNumber: 'V-99882',
        storageKey: 'https://placehold.co/600x400/png?text=Verification+Doc+2',
        status: 'Pending',
        expiresOn: '2028-05-12',
      },
      {
        complianceDocId: 'd3',
        docType: 'Insurance',
        docNumber: 'INS-4455',
        storageKey: 'https://placehold.co/600x400/png?text=Insurance+Policy',
        status: 'Verified',
        expiresOn: '2026-12-31',
      },
    ],
  },
]

export default function AgencyVerificationPage() {
  const role = useAppSelector((state) => state.auth.role)

  // Enforce Admin-only access exactly as requested.
  if (role !== UserRole.ADMIN) {
    return <Navigate to="/unauthorized" replace />
  }

  const handleApprove = (agencyId) => {
    console.log(`Approve clicked for agency: ${agencyId}`)
    alert(`Agency ${agencyId} approved (UI only)`)
  }

  const handleSuspend = (agencyId) => {
    console.log(`Suspend clicked for agency: ${agencyId}`)
    alert(`Agency ${agencyId} suspended (UI only)`)
  }

  return (
    <div className="space-y-6">
      <PageHeader 
        title="Agency Verification Queue" 
        subtitle="Review and approve agencies pending verification." 
      />

      {mockAgencies.length === 0 ? (
        <p className="text-slate-500">No agencies pending verification.</p>
      ) : (
        <div className="grid gap-6">
          {mockAgencies.map((agency) => (
            <Card key={agency.agencyId} className="p-0 overflow-hidden">
              <Card.Header className="flex items-center justify-between border-b border-slate-200 bg-slate-50/50 p-4">
                <div>
                  <h3 className="text-lg font-semibold text-slate-900">{agency.name}</h3>
                  <p className="text-sm text-slate-500">Reg No: {agency.businessRegNo}</p>
                </div>
                <StatusBadge status={agency.status} />
              </Card.Header>
              
              <Card.Body className="p-4 space-y-4">
                <h4 className="text-sm font-medium text-slate-700 uppercase tracking-wider">Compliance Documents</h4>
                {agency.complianceDocs.length === 0 ? (
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
                              <StatusBadge status={doc.status} />
                            </td>
                            <td className="px-4 py-3 text-right">
                              <a
                                href={doc.storageKey}
                                target="_blank"
                                rel="noreferrer"
                                className="text-primary hover:underline font-medium"
                              >
                                View Document ↗
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
                >
                  Suspend Agency
                </Button>
                <Button 
                  variant="status" 
                  status="green" 
                  onClick={() => handleApprove(agency.agencyId)}
                >
                  Approve Agency
                </Button>
              </Card.Footer>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}



