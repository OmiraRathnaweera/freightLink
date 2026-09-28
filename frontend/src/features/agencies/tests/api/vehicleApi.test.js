import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../../../lib/api/api.js'
import { updateVehicle, updateVehicleStatus } from '../../api/agencyApi.js'

vi.mock('../../../../lib/api/api.js', () => ({
  api: { put: vi.fn(), patch: vi.fn() },
}))

afterEach(() => vi.clearAllMocks())

describe('vehicle management API', () => {
  it('updates editable details without sending vehicle status', async () => {
    const vehicle = {
      registrationNo: 'WP-CAB-2468',
      vehicleType: 'Container',
      capacityKg: 24000,
      volumeM3: 60,
    }
    api.put.mockResolvedValueOnce({ vehicleId: 'vehicle-1', ...vehicle, status: 'Available' })

    await updateVehicle({ agencyId: 'agency-1', vehicleId: 'vehicle-1', vehicle })

    expect(api.put).toHaveBeenCalledWith('/agencies/agency-1/vehicles/vehicle-1', vehicle)
  })

  it('uses the dedicated status endpoint with the backend wire value', async () => {
    api.patch.mockResolvedValueOnce({ vehicleId: 'vehicle-1', status: 'Maintenance' })

    await updateVehicleStatus({ agencyId: 'agency-1', vehicleId: 'vehicle-1', status: 'Maintenance' })

    expect(api.patch).toHaveBeenCalledWith(
      '/agencies/agency-1/vehicles/vehicle-1/status',
      { status: 'Maintenance' },
    )
  })
})
