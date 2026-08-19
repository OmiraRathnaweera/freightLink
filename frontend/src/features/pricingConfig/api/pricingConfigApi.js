import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'
import { queryClient } from '../../../lib/api/queryClient.js'

/**
 * @typedef {object} FuelRate
 * @property {string} fuelPriceRateId
 * @property {string} fuelType
 * @property {number} pricePerLitre
 * @property {string} source
 * @property {string} effectiveFrom
 * @property {string} setByUserId
 * @property {string} setByUserName
 * @property {string} createdAt
 * @property {string|null} deletedAt
 * @property {string|null} deletedByUserId
 */

/**
 * @typedef {object} VehicleEfficiency
 * @property {string} vehicleClassEfficiencyId
 * @property {string} classLabel
 * @property {number} minPayloadKg
 * @property {number|null} maxPayloadKg
 * @property {number} fuelConsumptionLPer100Km
 * @property {string} source
 * @property {string} effectiveFrom
 * @property {string} setByUserId
 * @property {string} setByUserName
 * @property {string} createdAt
 * @property {string|null} deletedAt
 * @property {string|null} deletedByUserId
 */

// Query key factory, same shape as loadKeys (src/features/loads/api/loadsApi.js)
// — a broad key (fuelRates) for the "current" list, a narrow one per fuel
// type for its version history.
export const pricingConfigKeys = {
  all: ['pricingConfig'],
  fuelRates: () => [...pricingConfigKeys.all, 'fuelRates'],
  fuelRateHistory: (fuelType) => [...pricingConfigKeys.fuelRates(), 'history', fuelType],
  vehicleEfficiency: () => [...pricingConfigKeys.all, 'vehicleEfficiency'],
  vehicleEfficiencyHistory: (vehicleClass) => [...pricingConfigKeys.vehicleEfficiency(), 'history', vehicleClass],
}

export async function listFuelRates() {
  return api.get('/admin/pricing/fuel-rates')
}

export async function getFuelRateHistory(fuelType) {
  return api.get('/admin/pricing/fuel-rates/history', { params: { fuelType } })
}

export async function createFuelRate(data) {
  return api.post('/admin/pricing/fuel-rates', data)
}

export async function deleteFuelRate(id) {
  return api.delete(`/admin/pricing/fuel-rates/${id}`)
}

export async function listVehicleEfficiency() {
  return api.get('/admin/pricing/vehicle-efficiency')
}

export async function getVehicleEfficiencyHistory(vehicleClass) {
  return api.get('/admin/pricing/vehicle-efficiency/history', { params: { vehicleClass } })
}

export async function createVehicleEfficiency(data) {
  return api.post('/admin/pricing/vehicle-efficiency', data)
}

export async function deleteVehicleEfficiency(id) {
  return api.delete(`/admin/pricing/vehicle-efficiency/${id}`)
}

export function useFuelRatesQuery(options) {
  return useQuery({ queryKey: pricingConfigKeys.fuelRates(), queryFn: listFuelRates, ...options })
}

export function useFuelRateHistoryQuery(fuelType, options) {
  return useQuery({
    queryKey: pricingConfigKeys.fuelRateHistory(fuelType),
    queryFn: () => getFuelRateHistory(fuelType),
    enabled: Boolean(fuelType),
    ...options,
  })
}

export function useCreateFuelRateMutation(options) {
  return useMutation({
    mutationFn: createFuelRate,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: pricingConfigKeys.fuelRates() })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

export function useDeleteFuelRateMutation(options) {
  return useMutation({
    mutationFn: deleteFuelRate,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: pricingConfigKeys.fuelRates() })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

export function useVehicleEfficiencyQuery(options) {
  return useQuery({ queryKey: pricingConfigKeys.vehicleEfficiency(), queryFn: listVehicleEfficiency, ...options })
}

export function useVehicleEfficiencyHistoryQuery(vehicleClass, options) {
  return useQuery({
    queryKey: pricingConfigKeys.vehicleEfficiencyHistory(vehicleClass),
    queryFn: () => getVehicleEfficiencyHistory(vehicleClass),
    enabled: Boolean(vehicleClass),
    ...options,
  })
}

export function useCreateVehicleEfficiencyMutation(options) {
  return useMutation({
    mutationFn: createVehicleEfficiency,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: pricingConfigKeys.vehicleEfficiency() })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

export function useDeleteVehicleEfficiencyMutation(options) {
  return useMutation({
    mutationFn: deleteVehicleEfficiency,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: pricingConfigKeys.vehicleEfficiency() })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}
