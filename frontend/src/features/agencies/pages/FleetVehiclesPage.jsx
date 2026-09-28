import { useMemo, useState } from 'react'
import {
  Ban,
  CheckCircle,
  Filter,
  Loader2,
  Pencil,
  Plus,
  Search,
  Truck,
  Wrench,
} from 'lucide-react'
import { toast } from 'sonner'
import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import Input from '../../../components/Input.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { useEscapeKey } from '../../../hooks/useEscapeKey.js'
import { UserRole } from '../../../lib/enums.js'
import { useCurrentUserQuery } from '../../auth/api/authApi.js'
import { useUpdateVehicleStatusMutation, useVehiclesQuery } from '../api/agencyApi.js'
import AddVehicleDrawer from '../components/AddVehicleDrawer.jsx'
import { getAgencyErrorMessage } from '../lib/errorMessages.js'

function getVehicleTypeBadge(type) {
  switch (type) {
    case 'Lorry':
      return { label: 'Lorry', color: 'blue' }
    case 'Container':
      return { label: 'Container', color: 'primary' }
    case 'Refrigerated':
      return { label: 'Refrigerated', color: 'cyan' }
    case 'FlatBed':
      return { label: 'Flatbed', color: 'purple' }
    case 'Tipper':
      return { label: 'Tipper', color: 'amber' }
    case 'MiniTruck':
      return { label: 'Mini Truck', color: 'blue' }
    case 'MediumLorry':
      return { label: 'Medium Lorry', color: 'purple' }
    case 'ContainerTruck':
      return { label: 'Container Truck', color: 'primary' }
    default:
      return { label: type, color: 'slate' }
  }
}

export default function FleetVehiclesPage() {
  const role = useAppSelector((state) => state.auth.role)
  const isAgencyStaff = role === UserRole.AGENCY_STAFF
  const { data: user, isLoading: userLoading } = useCurrentUserQuery()
  const agencyId = user?.agencyId

  const { data: vehicles = [], isLoading: vehiclesLoading } = useVehiclesQuery(agencyId, {
    enabled: Boolean(agencyId),
  })

  const [isAddDrawerOpen, setIsAddDrawerOpen] = useState(false)
  const [editingVehicle, setEditingVehicle] = useState(null)
  const [retiringVehicle, setRetiringVehicle] = useState(null)
  const [pendingVehicleId, setPendingVehicleId] = useState(null)
  const [searchQuery, setSearchQuery] = useState('')
  const [typeFilter, setTypeFilter] = useState('ALL')
  const [statusFilter, setStatusFilter] = useState('ALL')
  const updateStatusMutation = useUpdateVehicleStatusMutation()
  useEscapeKey(Boolean(retiringVehicle) && pendingVehicleId !== retiringVehicle?.vehicleId,
    () => setRetiringVehicle(null))

  // Metrics
  const stats = useMemo(() => {
    const total = vehicles.length
    const available = vehicles.filter((v) => v.status === 'Available').length
    const onTrip = vehicles.filter((v) => v.status === 'OnTrip').length
    const maintenance = vehicles.filter((v) => v.status === 'Maintenance').length
    const retired = vehicles.filter((v) => v.status === 'Retired').length
    return { total, available, onTrip, maintenance, retired }
  }, [vehicles])

  // Filtered list
  const filteredVehicles = useMemo(() => {
    return vehicles.filter((v) => {
      const matchesSearch =
        !searchQuery ||
        v.registrationNo?.toLowerCase().includes(searchQuery.toLowerCase()) ||
        v.vehicleType?.toLowerCase().includes(searchQuery.toLowerCase())

      const matchesType = typeFilter === 'ALL' || v.vehicleType === typeFilter
      const matchesStatus = statusFilter === 'ALL' || v.status === statusFilter

      return matchesSearch && matchesType && matchesStatus
    })
  }, [vehicles, searchQuery, typeFilter, statusFilter])

  async function applyStatusChange(vehicle, status) {
    setPendingVehicleId(vehicle.vehicleId)
    try {
      await updateStatusMutation.mutateAsync({ agencyId, vehicleId: vehicle.vehicleId, status })
      toast.success(`Vehicle ${vehicle.registrationNo} is now ${status.toLowerCase()}.`)
      setRetiringVehicle(null)
    } catch (error) {
      toast.error(getAgencyErrorMessage(error))
    } finally {
      setPendingVehicleId(null)
    }
  }

  function requestStatusChange(vehicle, status) {
    if (status === vehicle.status) return
    if (status === 'Retired') {
      setRetiringVehicle(vehicle)
      return
    }
    void applyStatusChange(vehicle, status)
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
          title="Fleet Vehicles"
          description="Manage your transportation fleet, monitor vehicle status, and register new vehicles."
        />
        {isAgencyStaff && (
          <Button
            onClick={() => setIsAddDrawerOpen(true)}
            className="inline-flex items-center gap-2 self-start text-body-sm font-semibold sm:self-auto"
          >
            <Plus className="h-4 w-4" />
            <span>Add New Vehicle</span>
          </Button>
        )}
      </div>

      {/* Metrics Row */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
        <Card className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-body-xs font-medium text-on-surface-variant">Total Fleet</p>
              <p className="mt-1 text-headline-sm font-bold text-primary">{stats.total}</p>
            </div>
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
              <Truck className="h-5 w-5" />
            </div>
          </div>
        </Card>

        <Card className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-body-xs font-medium text-on-surface-variant">Available</p>
              <p className="mt-1 text-headline-sm font-bold text-status-green-text">{stats.available}</p>
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
              <Truck className="h-5 w-5" />
            </div>
          </div>
        </Card>

        <Card className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-body-xs font-medium text-on-surface-variant">Maintenance</p>
              <p className="mt-1 text-headline-sm font-bold text-status-amber-text">{stats.maintenance}</p>
            </div>
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-status-amber-bg text-status-amber-text">
              <Wrench className="h-5 w-5" />
            </div>
          </div>
        </Card>

        <Card className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-body-xs font-medium text-on-surface-variant">Retired</p>
              <p className="mt-1 text-headline-sm font-bold text-status-red-text">{stats.retired}</p>
            </div>
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-status-red-bg text-status-red-text">
              <Ban className="h-5 w-5" />
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
              placeholder="Search by registration number or class…"
              className="pl-9 text-sm"
            />
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <div className="flex items-center space-x-1.5 text-xs text-on-surface-variant">
              <Filter className="h-3.5 w-3.5" />
              <span>Filters:</span>
            </div>

            <select
              value={typeFilter}
              onChange={(e) => setTypeFilter(e.target.value)}
              className="rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-700"
            >
              <option value="ALL">All Types</option>
              <option value="Lorry">Lorry</option>
              <option value="Container">Container</option>
              <option value="Refrigerated">Refrigerated</option>
              <option value="FlatBed">Flatbed</option>
              <option value="Tipper">Tipper</option>
            </select>

            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-700"
            >
              <option value="ALL">All Statuses</option>
              <option value="Available">Available</option>
              <option value="OnTrip">On Trip</option>
              <option value="Maintenance">Maintenance</option>
              <option value="Retired">Retired</option>
            </select>
          </div>
        </div>
      </Card>

      {/* Vehicles Table / Content */}
      <Card className="overflow-hidden p-0">
        {vehiclesLoading ? (
          <div className="p-12 text-center text-slate-500">Loading vehicles…</div>
        ) : filteredVehicles.length === 0 ? (
          <div className="p-8">
            <EmptyState
              title={vehicles.length === 0 ? 'No vehicles registered yet' : 'No matching vehicles'}
              description={
                vehicles.length === 0
                  ? 'Add your agency trucks, lorries, and containers to start receiving load dispatch proposals.'
                  : 'Try clearing your search query or adjusting your filters.'
              }
              action={
                vehicles.length === 0 && isAgencyStaff ? (
                  <Button onClick={() => setIsAddDrawerOpen(true)}>
                    <Plus className="mr-1.5 h-4 w-4" />
                    <span>Register First Vehicle</span>
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
                  <th className="px-6 py-3.5">Registration No</th>
                  <th className="px-6 py-3.5">Vehicle Type</th>
                  <th className="px-6 py-3.5">Payload Capacity</th>
                  <th className="px-6 py-3.5">Cargo Volume</th>
                  <th className="px-6 py-3.5">Status</th>
                  <th className="px-6 py-3.5">Added Date</th>
                  {isAgencyStaff && <th className="px-6 py-3.5 text-right">Actions</th>}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredVehicles.map((vehicle) => {
                  const typeBadge = getVehicleTypeBadge(vehicle.vehicleType)
                  const isLocked = vehicle.status === 'OnTrip' || vehicle.status === 'Retired'
                  const isPending = pendingVehicleId === vehicle.vehicleId
                  return (
                    <tr key={vehicle.vehicleId} className="transition-colors hover:bg-slate-50/50">
                      <td className="px-6 py-4 font-mono font-bold text-slate-900">
                        {vehicle.registrationNo}
                      </td>
                      <td className="px-6 py-4">
                        <span className="inline-flex items-center gap-1.5 font-medium text-slate-700">
                          <Truck className="h-4 w-4 text-slate-500" />
                          <span>{typeBadge.label}</span>
                        </span>
                      </td>
                      <td className="px-6 py-4 font-mono text-slate-600">
                        {Number(vehicle.capacityKg).toLocaleString()} kg
                      </td>
                      <td className="px-6 py-4 font-mono text-slate-600">
                        {vehicle.volumeM3} m³
                      </td>
                      <td className="px-6 py-4">
                        <StatusBadge
                          tone={
                            vehicle.status === 'Available'
                              ? 'green'
                              : vehicle.status === 'OnTrip'
                                ? 'blue'
                                : vehicle.status === 'Maintenance'
                                  ? 'amber'
                                  : 'red'
                          }
                        >
                          {vehicle.status === 'OnTrip' ? 'On Trip' : vehicle.status}
                        </StatusBadge>
                      </td>
                      <td className="px-6 py-4 text-slate-500">
                        {vehicle.createdAt
                          ? new Date(vehicle.createdAt).toLocaleDateString()
                          : '—'}
                      </td>
                      {isAgencyStaff && (
                        <td className="px-6 py-4 text-right">
                          <div className="inline-flex items-center gap-2">
                            <Button
                              type="button"
                              variant="secondary"
                              onClick={() => setEditingVehicle(vehicle)}
                              disabled={isLocked || isPending}
                              title={isLocked ? 'Vehicles on a trip or retired cannot be edited' : undefined}
                              className="inline-flex items-center gap-1.5 text-xs"
                            >
                              <Pencil className="h-3.5 w-3.5" /> Edit
                            </Button>
                            <select
                              value={vehicle.status}
                              aria-label={`Change status for ${vehicle.registrationNo}`}
                              disabled={isLocked || isPending}
                              title={isLocked ? 'OnTrip is system-managed and Retired is final' : undefined}
                              onChange={(event) => requestStatusChange(vehicle, event.target.value)}
                              className="rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-700 disabled:cursor-not-allowed disabled:opacity-60"
                            >
                              <option value="Available">Available</option>
                              <option value="Maintenance">Maintenance</option>
                              <option value="Retired">Retired</option>
                              {vehicle.status === 'OnTrip' && <option value="OnTrip">On Trip</option>}
                            </select>
                            {isPending && <Loader2 aria-label="Updating vehicle" className="h-4 w-4 animate-spin text-primary" />}
                          </div>
                        </td>
                      )}
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* Slide-over Drawer for adding new vehicle */}
      <AddVehicleDrawer
        agencyId={agencyId}
        isOpen={isAddDrawerOpen || Boolean(editingVehicle)}
        vehicle={editingVehicle}
        onClose={() => {
          setIsAddDrawerOpen(false)
          setEditingVehicle(null)
        }}
      />
      {retiringVehicle && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-primary/40 p-4">
          <div role="alertdialog" aria-modal="true" aria-labelledby="retire-vehicle-title" className="w-full max-w-md rounded-lg bg-white p-6 shadow-2xl">
            <h2 id="retire-vehicle-title" className="text-lg font-bold text-on-surface">Retire vehicle?</h2>
            <p className="mt-2 text-sm text-on-surface-variant">
              {retiringVehicle.registrationNo} will be removed from matching. Retirement is final.
            </p>
            <div className="mt-6 flex justify-end gap-3">
              <Button type="button" variant="secondary" autoFocus onClick={() => setRetiringVehicle(null)} disabled={pendingVehicleId === retiringVehicle.vehicleId}>Cancel</Button>
              <Button type="button" onClick={() => void applyStatusChange(retiringVehicle, 'Retired')} disabled={pendingVehicleId === retiringVehicle.vehicleId}>
                {pendingVehicleId === retiringVehicle.vehicleId ? 'Retiring...' : 'Retire vehicle'}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
