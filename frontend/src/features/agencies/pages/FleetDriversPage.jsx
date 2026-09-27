import { useMemo, useState } from 'react'
import {
  CheckCircle,
  Filter,
  IdCard,
  Loader2,
  Plus,
  Search,
  UserX,
  UserCheck,
} from 'lucide-react'
import { toast } from 'sonner'
import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import Input from '../../../components/Input.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import { useCurrentUserQuery } from '../../auth/api/authApi.js'
import { useDriversQuery, useUpdateDriverStatusMutation } from '../api/agencyApi.js'
import { getAgencyErrorMessage } from '../lib/errorMessages.js'
import AddDriverDrawer from '../components/AddDriverDrawer.jsx'

function getStatusTone(status) {
  switch (status) {
    case 'Active':
      return 'green'
    case 'OnTrip':
      return 'blue'
    case 'Inactive':
      return 'red'
    default:
      return 'neutral'
  }
}

export default function FleetDriversPage() {
  const { data: user, isLoading: userLoading } = useCurrentUserQuery()
  const agencyId = user?.agencyId

  const { data: drivers = [], isLoading: driversLoading } = useDriversQuery(agencyId, {
    enabled: Boolean(agencyId),
  })

  const updateStatusMutation = useUpdateDriverStatusMutation({
    onSuccess: (_, variables) => {
      toast.success(
        variables.status === 'Inactive' ? 'Driver removed from active roster.' : 'Driver reinstated.',
      )
    },
    onError: (error) => {
      toast.error(getAgencyErrorMessage(error))
    },
  })
  const [pendingDriverId, setPendingDriverId] = useState(null)

  const [isAddDrawerOpen, setIsAddDrawerOpen] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')
  const [statusFilter, setStatusFilter] = useState('ALL')

  const stats = useMemo(() => {
    const total = drivers.length
    const active = drivers.filter((d) => d.status === 'Active').length
    const onTrip = drivers.filter((d) => d.status === 'OnTrip').length
    const inactive = drivers.filter((d) => d.status === 'Inactive').length
    return { total, active, onTrip, inactive }
  }, [drivers])

  const filteredDrivers = useMemo(() => {
    return drivers.filter((d) => {
      const matchesSearch =
        !searchQuery ||
        d.fullName?.toLowerCase().includes(searchQuery.toLowerCase()) ||
        d.email?.toLowerCase().includes(searchQuery.toLowerCase()) ||
        d.licenceNo?.toLowerCase().includes(searchQuery.toLowerCase())

      const matchesStatus = statusFilter === 'ALL' || d.status === statusFilter

      return matchesSearch && matchesStatus
    })
  }, [drivers, searchQuery, statusFilter])

  async function handleToggleStatus(driver) {
    const nextStatus = driver.status === 'Inactive' ? 'Active' : 'Inactive'
    setPendingDriverId(driver.driverId)
    try {
      await updateStatusMutation.mutateAsync({ agencyId, driverId: driver.driverId, status: nextStatus })
    } finally {
      setPendingDriverId(null)
    }
  }

  if (userLoading) {
    return <div className="p-8 text-center text-slate-500">Loading agency profile…</div>
  }

  if (!agencyId) {
    return (
      <div className="p-8 text-center text-on-surface-variant">
        Agency profile not found. Please ensure you are logged into an Agency Staff account.
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <PageHeader
          title="Fleet Drivers"
          subtitle="Onboard drivers, set their login credentials, and manage your active roster."
        />
        <Button
          onClick={() => setIsAddDrawerOpen(true)}
          className="inline-flex items-center gap-2 self-start text-body-sm font-semibold sm:self-auto"
        >
          <Plus className="h-4 w-4" />
          <span>Add Driver</span>
        </Button>
      </div>

      {/* Metrics Row */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <Card className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-body-xs font-medium text-on-surface-variant">Total Drivers</p>
              <p className="mt-1 text-headline-sm font-bold text-primary">{stats.total}</p>
            </div>
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
              <IdCard className="h-5 w-5" />
            </div>
          </div>
        </Card>

        <Card className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-body-xs font-medium text-on-surface-variant">Active</p>
              <p className="mt-1 text-headline-sm font-bold text-status-green-text">{stats.active}</p>
            </div>
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-status-green-bg text-status-green-text">
              <CheckCircle className="h-5 w-5" />
            </div>
          </div>
        </Card>

        <Card className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-body-xs font-medium text-on-surface-variant">On Trip</p>
              <p className="mt-1 text-headline-sm font-bold text-status-blue-text">{stats.onTrip}</p>
            </div>
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-status-blue-bg text-status-blue-text">
              <UserCheck className="h-5 w-5" />
            </div>
          </div>
        </Card>

        <Card className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-body-xs font-medium text-on-surface-variant">Inactive</p>
              <p className="mt-1 text-headline-sm font-bold text-status-red-text">{stats.inactive}</p>
            </div>
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-status-red-bg text-status-red-text">
              <UserX className="h-5 w-5" />
            </div>
          </div>
        </Card>
      </div>

      {/* Filter and Search Bar */}
      <Card className="p-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <Input
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search by name, email, or licence number…"
              className="pl-9 text-sm"
            />
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <div className="flex items-center space-x-1.5 text-xs text-on-surface-variant">
              <Filter className="h-3.5 w-3.5" />
              <span>Filters:</span>
            </div>

            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-700"
            >
              <option value="ALL">All Statuses</option>
              <option value="Active">Active</option>
              <option value="OnTrip">On Trip</option>
              <option value="Inactive">Inactive</option>
            </select>
          </div>
        </div>
      </Card>

      {/* Drivers Table / Content */}
      <Card className="overflow-hidden p-0">
        {driversLoading ? (
          <div className="p-12 text-center text-slate-500">Loading drivers…</div>
        ) : filteredDrivers.length === 0 ? (
          <div className="p-8">
            <EmptyState
              title={drivers.length === 0 ? 'No drivers onboarded yet' : 'No matching drivers'}
              description={
                drivers.length === 0
                  ? 'Add drivers to your agency so they can sign in and receive trip assignments.'
                  : 'Try clearing your search query or adjusting your filters.'
              }
              action={
                drivers.length === 0 ? (
                  <Button onClick={() => setIsAddDrawerOpen(true)}>
                    <Plus className="mr-1.5 h-4 w-4" />
                    <span>Add First Driver</span>
                  </Button>
                ) : undefined
              }
            />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-slate-border bg-slate-50 text-body-xs font-semibold uppercase text-slate-600">
                <tr>
                  <th className="px-6 py-3.5">Name</th>
                  <th className="px-6 py-3.5">Email</th>
                  <th className="px-6 py-3.5">Licence No</th>
                  <th className="px-6 py-3.5">Licence Expiry</th>
                  <th className="px-6 py-3.5">Status</th>
                  <th className="px-6 py-3.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredDrivers.map((driver) => {
                  const isOnTrip = driver.status === 'OnTrip'
                  const isPending = pendingDriverId === driver.driverId
                  return (
                    <tr key={driver.driverId} className="transition-colors hover:bg-slate-50/50">
                      <td className="px-6 py-4 font-medium text-slate-900">{driver.fullName}</td>
                      <td className="px-6 py-4 text-slate-600">{driver.email}</td>
                      <td className="px-6 py-4 font-mono text-slate-600">{driver.licenceNo}</td>
                      <td className="px-6 py-4 font-mono text-slate-600">
                        {driver.licenceExpiry ? new Date(driver.licenceExpiry).toLocaleDateString() : '—'}
                      </td>
                      <td className="px-6 py-4">
                        <StatusBadge tone={getStatusTone(driver.status)}>{driver.status}</StatusBadge>
                      </td>
                      <td className="px-6 py-4 text-right">
                        <Button
                          type="button"
                          variant="secondary"
                          disabled={isOnTrip || isPending}
                          title={isOnTrip ? 'Cannot change status while the driver is on a trip' : undefined}
                          onClick={() => handleToggleStatus(driver)}
                          className="inline-flex items-center gap-1.5 text-xs"
                        >
                          {isPending ? (
                            <Loader2 className="h-3.5 w-3.5 animate-spin" />
                          ) : driver.status === 'Inactive' ? (
                            <UserCheck className="h-3.5 w-3.5" />
                          ) : (
                            <UserX className="h-3.5 w-3.5" />
                          )}
                          {driver.status === 'Inactive' ? 'Reinstate' : 'Remove'}
                        </Button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* Slide-over Drawer for adding new driver */}
      <AddDriverDrawer
        agencyId={agencyId}
        isOpen={isAddDrawerOpen}
        onClose={() => setIsAddDrawerOpen(false)}
      />
    </div>
  )
}
