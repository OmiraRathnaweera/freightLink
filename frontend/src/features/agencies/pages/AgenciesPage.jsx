import { useEffect, useRef, useState } from 'react'
import { Navigate, useSearchParams } from 'react-router-dom'
import {
  Building2,
  CheckCircle2,
  Filter,
  MapPin,
  RefreshCw,
  Search,
  ShieldCheck,
  Truck,
  Users,
} from 'lucide-react'
import PageHeader from '../../../components/PageHeader.jsx'
import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import Skeleton from '../../../components/Skeleton.jsx'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { UserRole } from '../../../lib/enums.js'
import { cx } from '../../../lib/cx.js'
import { useAgenciesQuery, useAgenciesSummaryQuery, useAgencyFleetQuery } from '../api/agencyApi.js'
// Pagination and its debounce hook already live under features/loads and are reused here rather
// than duplicated — TripsPage does the same for Pagination (frontend/CLAUDE.md's feature-folder
// isolation is about not scattering business logic across features, not about single generic UI
// widgets like this one).
import Pagination from '../../loads/components/Pagination.jsx'
import { useDebouncedValue } from '../../loads/hooks/useDebouncedValue.js'
import AgencyProfilePage from './AgencyProfilePage.jsx'

const DEFAULT_PAGE_SIZE = 20

function getAgencyStatusTone(status) {
  switch (status) {
    case 'Active':
      return 'green'
    case 'Suspended':
      return 'red'
    case 'Verified':
      return 'blue'
    case 'Pending':
      return 'amber'
    default:
      return 'gray'
  }
}

function formatDate(isoString) {
  if (!isoString) return '—'
  try {
    return new Date(isoString).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    })
  } catch {
    return isoString
  }
}

function AdminAgenciesDashboard() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [selectedAgencyId, setSelectedAgencyId] = useState(null)

  const page = Number(searchParams.get('page') ?? '1')
  const pageSize = Number(searchParams.get('pageSize') ?? String(DEFAULT_PAGE_SIZE))
  const statusFilter = searchParams.get('status') ?? 'ALL'
  const search = searchParams.get('search') ?? ''

  // Local input state, debounced before it ever reaches the URL/API — typing shouldn't fire a
  // GET /agencies on every keystroke (issue #45's "consider debouncing search input"). Mirrors
  // LoadFilterBar's debounce-then-commit pattern (frontend/src/features/loads/components/
  // LoadFilterBar.jsx): lastEmittedRef distinguishes "search changed because our own debounce
  // just committed" from "search changed for some other reason" (browser back/forward, a filter
  // reset elsewhere), so the two effects below never fight each other or loop.
  const [searchInput, setSearchInput] = useState(search)
  const debouncedSearch = useDebouncedValue(searchInput, 400)
  const lastEmittedRef = useRef(search)

  function updateParams(patch, { resetPage = true } = {}) {
    const next = new URLSearchParams(searchParams)
    Object.entries(patch).forEach(([key, value]) => {
      if (value === undefined || value === null || value === '') {
        next.delete(key)
      } else {
        next.set(key, String(value))
      }
    })
    if (resetPage) next.delete('page')
    setSearchParams(next)
  }

  useEffect(() => {
    if (debouncedSearch !== search) {
      lastEmittedRef.current = debouncedSearch
      updateParams({ search: debouncedSearch || undefined })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedSearch])

  useEffect(() => {
    if (search !== lastEmittedRef.current) {
      lastEmittedRef.current = search
      setSearchInput(search)
    }
  }, [search])

  const agenciesQuery = useAgenciesQuery(
    {
      page,
      pageSize,
      search: search || undefined,
      status: statusFilter === 'ALL' ? undefined : statusFilter,
    },
    { refetchInterval: 15000 },
  )
  // System-wide totals for the summary cards, deliberately independent of the table's current
  // page/search/status filter (issue #45) — never derive these from agenciesQuery.data.items.
  const summaryQuery = useAgenciesSummaryQuery()

  const fleetQuery = useAgencyFleetQuery(selectedAgencyId, {
    enabled: Boolean(selectedAgencyId),
  })

  // GetListAsync returns a paged envelope ({ items, page, pageSize, totalItems, totalPages }),
  // not a raw array — see backend/Services/AgencyService.cs.
  const agencies = agenciesQuery.data?.items ?? []

  const metrics = summaryQuery.data ?? {
    totalAgencies: 0,
    activeAgencies: 0,
    totalDrivers: 0,
    activeDrivers: 0,
    totalVehicles: 0,
  }

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="OPERATIONS & FLEET"
        title="Registered Agencies & Fleet"
        description="View registered transportation agencies, driver rosters, fleet capacities, and depot locations."
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant="secondary"
              onClick={() => agenciesQuery.refetch()}
              disabled={agenciesQuery.isFetching}
              className="inline-flex items-center gap-1.5"
            >
              <RefreshCw className={cx('h-4 w-4', agenciesQuery.isFetching && 'animate-spin')} />
              Refresh
            </Button>
          </div>
        }
      />

      {/* Platform Governance Notice */}
      <div className="flex items-center gap-3 rounded-lg border border-blue-200 bg-blue-50/70 p-3.5 text-xs text-blue-900">
        <ShieldCheck className="h-5 w-5 shrink-0 text-blue-600" />
        <div>
          <span className="font-semibold">Platform Dispatch Rule: </span>
          Administrators view and audit registered agencies, driver counts, and fleet details. Trip dispatching authority is reserved exclusively to the executing agencies.
        </div>
      </div>

      {/* Summary Stat Cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Card className="flex items-center gap-3.5 p-4 border border-slate-200 shadow-xs">
          <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <Building2 className="h-5 w-5" />
          </div>
          <div>
            <p className="text-label-caps text-on-surface-variant">Registered Agencies</p>
            <div className="flex items-baseline gap-1.5">
              <span className="text-headline-md font-bold text-on-surface">{metrics.totalAgencies}</span>
              <span className="text-xs font-medium text-emerald-600">({metrics.activeAgencies} active)</span>
            </div>
          </div>
        </Card>

        <Card className="flex items-center gap-3.5 p-4 border border-slate-200 shadow-xs">
          <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-indigo-50 text-indigo-600">
            <Users className="h-5 w-5" />
          </div>
          <div>
            <p className="text-label-caps text-on-surface-variant">Total Drivers</p>
            <div className="flex items-baseline gap-1.5">
              <span className="text-headline-md font-bold text-on-surface">{metrics.totalDrivers}</span>
              <span className="text-xs font-medium text-indigo-600">({metrics.activeDrivers} active)</span>
            </div>
          </div>
        </Card>

        <Card className="flex items-center gap-3.5 p-4 border border-slate-200 shadow-xs">
          <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-emerald-50 text-emerald-600">
            <Truck className="h-5 w-5" />
          </div>
          <div>
            <p className="text-label-caps text-on-surface-variant">Fleet Vehicles</p>
            <span className="text-headline-md font-bold text-on-surface">{metrics.totalVehicles}</span>
          </div>
        </Card>

        <Card className="flex items-center gap-3.5 p-4 border border-slate-200 shadow-xs">
          <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-amber-50 text-amber-600">
            <CheckCircle2 className="h-5 w-5" />
          </div>
          <div>
            <p className="text-label-caps text-on-surface-variant">Active Rate</p>
            <span className="text-headline-md font-bold text-on-surface">
              {metrics.totalAgencies > 0
                ? `${Math.round((metrics.activeAgencies / metrics.totalAgencies) * 100)}%`
                : '100%'}
            </span>
          </div>
        </Card>
      </div>

      {/* Search & Filter Bar */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-slate-200 bg-surface-container-lowest p-3 shadow-xs">
        <div className="relative min-w-[260px] flex-1 max-w-md">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant" />
          <input
            type="text"
            placeholder="Search agency name, BR number, or yard address..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="w-full rounded-md border border-slate-300 bg-surface pl-9 pr-3 py-1.5 text-body-md text-on-surface placeholder:text-on-surface-variant focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          />
        </div>
        <div className="flex items-center gap-2">
          <Filter className="h-4 w-4 text-on-surface-variant" />
          <select
            value={statusFilter}
            onChange={(e) => updateParams({ status: e.target.value === 'ALL' ? undefined : e.target.value })}
            className="rounded-md border border-slate-300 bg-surface px-3 py-1.5 text-body-md text-on-surface focus:border-primary focus:outline-none"
          >
            <option value="ALL">All Statuses</option>
            <option value="Pending">Pending</option>
            <option value="Verified">Verified</option>
            <option value="Active">Active</option>
            <option value="Suspended">Suspended</option>
          </select>
        </div>
      </div>

      {/* Agencies Table */}
      <Card className="min-h-[50vh] p-0 overflow-hidden">
        {agenciesQuery.isLoading ? (
          <div className="space-y-3 p-6">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : agenciesQuery.isError ? (
          <div className="p-6">
            <ErrorState
              description="Failed to load registered agencies. Please verify network and server connectivity."
              onRetry={() => agenciesQuery.refetch()}
            />
          </div>
        ) : agencies.length === 0 ? (
          <div className="p-8">
            <EmptyState
              icon={Building2}
              title="No agencies found"
              description={
                search || statusFilter !== 'ALL'
                  ? 'No registered agencies match your search and filter criteria.'
                  : 'No transportation agencies are currently registered on the platform.'
              }
            />
          </div>
        ) : (
          <>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-body-md" data-testid="agencies-table">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50/75 text-label-caps text-on-surface-variant">
                  <th className="py-3 pl-4 pr-3 font-normal">Agency Name</th>
                  <th className="py-3 px-3 font-normal">Business Reg No</th>
                  <th className="py-3 px-3 font-normal">Status</th>
                  <th className="py-3 px-3 font-normal text-center">Drivers</th>
                  <th className="py-3 px-3 font-normal text-center">Vehicles</th>
                  <th className="py-3 px-3 font-normal">Yard Location</th>
                  <th className="py-3 px-3 font-normal">Registered</th>
                  <th className="py-3 pr-4 text-right font-normal">Fleet Inspection</th>
                </tr>
              </thead>
              <tbody>
                {agencies.map((agency, index) => (
                  <tr
                    key={agency.agencyId}
                    className={cx(
                      'border-b border-slate-100 transition-colors hover:bg-slate-50/80',
                      index % 2 === 1 && 'bg-slate-50/40',
                      selectedAgencyId === agency.agencyId && 'bg-primary/5 border-primary/20'
                    )}
                  >
                    <td className="py-3 pl-4 pr-3">
                      <div className="font-semibold text-on-surface">{agency.name}</div>
                      <div className="text-xs text-on-surface-variant font-mono">
                        {agency.agencyId.slice(0, 8)}...
                      </div>
                    </td>
                    <td className="py-3 px-3 font-mono text-sm text-slate-700">
                      {agency.businessRegNo || '—'}
                    </td>
                    <td className="py-3 px-3">
                      <StatusBadge tone={getAgencyStatusTone(agency.status)}>{agency.status}</StatusBadge>
                    </td>
                    <td className="py-3 px-3 text-center">
                      <span className="inline-flex items-center gap-1 rounded-full bg-indigo-50 px-2.5 py-0.5 text-xs font-semibold text-indigo-700 border border-indigo-200">
                        <Users className="h-3 w-3" />
                        {agency.driverCount ?? 0}
                        {agency.activeDriverCount != null && (
                          <span className="text-[10px] text-indigo-500 font-normal">
                            ({agency.activeDriverCount} active)
                          </span>
                        )}
                      </span>
                    </td>
                    <td className="py-3 px-3 text-center">
                      <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-semibold text-emerald-700 border border-emerald-200">
                        <Truck className="h-3 w-3" />
                        {agency.vehicleCount ?? 0}
                      </span>
                    </td>
                    <td className="py-3 px-3">
                      <div className="flex items-center gap-1 text-xs text-slate-700">
                        <MapPin className="h-3.5 w-3.5 shrink-0 text-slate-400" />
                        <span className="max-w-[200px] truncate" title={agency.yardAddress}>
                          {agency.yardAddress || 'Yard location unassigned'}
                        </span>
                      </div>
                      {agency.yardLat != null && agency.yardLng != null && (
                        <div className="text-[11px] font-mono text-slate-400 pl-4.5">
                          {Number(agency.yardLat).toFixed(4)}, {Number(agency.yardLng).toFixed(4)}
                        </div>
                      )}
                    </td>
                    <td className="py-3 px-3 text-xs text-on-surface-variant">
                      {formatDate(agency.createdAt)}
                    </td>
                    <td className="py-3 pr-4 text-right">
                      <Button
                        variant="secondary"
                        onClick={() =>
                          setSelectedAgencyId(selectedAgencyId === agency.agencyId ? null : agency.agencyId)
                        }
                        className="text-xs px-2.5 py-1"
                      >
                        {selectedAgencyId === agency.agencyId ? 'Close Fleet' : 'View Fleet'}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            page={agenciesQuery.data.page}
            pageSize={agenciesQuery.data.pageSize}
            totalItems={agenciesQuery.data.totalItems}
            totalPages={agenciesQuery.data.totalPages}
            onPageChange={(nextPage) => updateParams({ page: nextPage }, { resetPage: false })}
            onPageSizeChange={(nextPageSize) => updateParams({ pageSize: nextPageSize })}
          />
          </>
        )}
      </Card>

      {/* Fleet Inspection Drawer for the Admin-selected agency */}
      {Boolean(selectedAgencyId) && (
        <Card className="border border-slate-200 p-6 space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 pb-3">
            <div>
              <h2 className="text-title-lg font-bold text-on-surface">
                {fleetQuery.data?.agencyName
                  ? `${fleetQuery.data.agencyName} — Fleet Roster`
                  : 'Agency Fleet Overview'}
              </h2>
              <p className="text-body-md text-on-surface-variant">
                Active drivers and transport vehicles registered under this agency.
              </p>
            </div>
            <Button variant="secondary" onClick={() => setSelectedAgencyId(null)} className="text-xs">
              Close Fleet View
            </Button>
          </div>

          {fleetQuery.isLoading ? (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <Skeleton className="h-40 w-full" />
              <Skeleton className="h-40 w-full" />
            </div>
          ) : fleetQuery.isError ? (
            <ErrorState
              description="Unable to retrieve agency fleet details."
              onRetry={() => fleetQuery.refetch()}
            />
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
              {/* Registered Drivers */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-sm text-slate-800 flex items-center gap-1.5">
                    <Users className="h-4 w-4 text-primary" />
                    Registered Drivers ({fleetQuery.data?.drivers?.length ?? 0})
                  </span>
                </div>
                <div className="rounded-lg border border-slate-200 bg-surface-container-lowest divide-y divide-slate-100 max-h-72 overflow-y-auto">
                  {(fleetQuery.data?.drivers ?? []).length === 0 ? (
                    <div className="p-4 text-center text-xs text-on-surface-variant">
                      No drivers registered under this agency.
                    </div>
                  ) : (
                    fleetQuery.data.drivers.map((d) => (
                      <div key={d.driverId} className="flex items-center justify-between p-3 text-xs">
                        <div>
                          <div className="font-medium text-slate-900">{d.fullName || d.user?.fullName}</div>
                          <div className="font-mono text-slate-500 text-[11px]">
                            {d.licenceNo || 'No license recorded'}
                          </div>
                        </div>
                        <StatusBadge tone={d.status === 'Active' ? 'green' : 'gray'}>
                          {d.status || 'Active'}
                        </StatusBadge>
                      </div>
                    ))
                  )}
                </div>
              </div>

              {/* Registered Vehicles */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-sm text-slate-800 flex items-center gap-1.5">
                    <Truck className="h-4 w-4 text-emerald-600" />
                    Fleet Vehicles ({fleetQuery.data?.vehicles?.length ?? 0})
                  </span>
                </div>
                <div className="rounded-lg border border-slate-200 bg-surface-container-lowest divide-y divide-slate-100 max-h-72 overflow-y-auto">
                  {(fleetQuery.data?.vehicles ?? []).length === 0 ? (
                    <div className="p-4 text-center text-xs text-on-surface-variant">
                      No vehicles registered under this agency.
                    </div>
                  ) : (
                    fleetQuery.data.vehicles.map((v) => (
                      <div key={v.vehicleId} className="flex items-center justify-between p-3 text-xs">
                        <div>
                          <div className="font-mono font-medium text-slate-900">{v.registrationNo}</div>
                          <div className="text-slate-500 text-[11px]">
                            {v.vehicleType || 'Truck'} • {v.capacityKg ? `${v.capacityKg} kg` : ''}
                          </div>
                        </div>
                        <StatusBadge tone={v.status === 'Available' ? 'green' : v.status === 'InTransit' ? 'blue' : 'amber'}>
                          {v.status || 'Available'}
                        </StatusBadge>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>
          )}
        </Card>
      )}
    </div>
  )
}

export default function AgenciesPage() {
  const role = useAppSelector((state) => state.auth.role)

  if (role === UserRole.ADMIN) {
    return <AdminAgenciesDashboard />
  }

  if (role === UserRole.AGENCY_STAFF) {
    return <AgencyProfilePage />
  }

  return <Navigate to="/unauthorized" replace />
}
