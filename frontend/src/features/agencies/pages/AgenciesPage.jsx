import { Navigate } from 'react-router-dom'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { UserRole } from '../../../lib/enums.js'
import { useAgenciesQuery } from '../api/agencyApi.js'
import Card from '../../../components/Card.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'

function AdminAgenciesList() {
  const { data: pagedData, isLoading, isError } = useAgenciesQuery({})
  const agencies = pagedData?.items || []

  return (
    <div className="space-y-6">
      <PageHeader 
        title="All Agencies" 
        subtitle="Manage and view all registered agencies across the network." 
      />

      <Card className="overflow-hidden">
        {isLoading && <div className="p-8 text-center text-slate-500">Loading agencies...</div>}
        {isError && <div className="p-8 text-center text-status-red-text">Failed to load agencies.</div>}
        
        {!isLoading && !isError && agencies.length === 0 ? (
          <div className="p-8 text-center text-slate-500">No agencies found.</div>
        ) : (
          !isLoading && !isError && (
            <div className="overflow-x-auto">
              <table className="w-full text-sm text-left">
                <thead className="bg-slate-50 text-slate-600 border-b border-slate-200">
                  <tr>
                    <th className="px-6 py-3 font-medium">Agency Name</th>
                    <th className="px-6 py-3 font-medium">Business Reg No</th>
                    <th className="px-6 py-3 font-medium">Location</th>
                    <th className="px-6 py-3 font-medium">Registered On</th>
                    <th className="px-6 py-3 font-medium">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {agencies.map((agency) => (
                    <tr key={agency.agencyId} className="hover:bg-slate-50/50 transition-colors">
                      <td className="px-6 py-4 font-medium text-slate-900">{agency.name}</td>
                      <td className="px-6 py-4 text-slate-600 font-mono text-xs">{agency.businessRegNo}</td>
                      <td className="px-6 py-4 text-slate-600">
                        {agency.yardAddress}
                        <div className="text-xs text-slate-400 mt-0.5">
                          {agency.yardLat}, {agency.yardLng}
                        </div>
                      </td>
                      <td className="px-6 py-4 text-slate-600">
                        {new Date(agency.createdAt).toLocaleDateString()}
                      </td>
                      <td className="px-6 py-4">
                        <StatusBadge tone={
                          agency.status === 'Pending' ? 'amber' :
                          agency.status === 'Verified' ? 'blue' :
                          agency.status === 'Active' ? 'green' : 'red'
                        }>
                          {agency.status}
                        </StatusBadge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        )}
      </Card>
    </div>
  )
}

export default function AgenciesPage() {
  const role = useAppSelector((state) => state.auth.role)

  if (role === UserRole.ADMIN) {
    return <AdminAgenciesList />
  }

  if (role === UserRole.AGENCY_STAFF) {
    return (
      <div className="p-6">
        <h1 className="text-2xl font-bold mb-4">My Agency Profile</h1>
        <p className="text-slate-500">Agency profile view not implemented yet.</p>
      </div>
    )
  }

  return <Navigate to="/unauthorized" replace />
}
