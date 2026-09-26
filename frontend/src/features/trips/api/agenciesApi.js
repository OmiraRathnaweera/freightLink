import { useQuery } from "@tanstack/react-query";
import { api } from "../../../lib/api/api.js";

export const agencyKeys = {
  all: ["agencies"],
  lists: () => [...agencyKeys.all, "list"],
  list: (params) => [...agencyKeys.lists(), params],
  details: () => [...agencyKeys.all, "detail"],
  detail: (id) => [...agencyKeys.details(), id],
  fleets: () => [...agencyKeys.all, "fleet"],
  fleet: (id) => [...agencyKeys.fleets(), id ?? "my"],
};

/**
 * GET /agencies — list registered agencies (Admin)
 * @param {object} [params]
 * @returns {Promise<Array<object>>}
 */
export async function listAgencies(params) {
  return api.get("/agencies", { params });
}

/**
 * Query hook for listing registered agencies
 * @param {object} [params]
 * @param {object} [options]
 */
export function useAgenciesQuery(params, options) {
  return useQuery({
    queryKey: agencyKeys.list(params),
    queryFn: () => listAgencies(params),
    ...options,
  });
}

/**
 * GET /agencies/{agencyId} — single agency detail
 * @param {string} agencyId
 */
export async function getAgency(agencyId) {
  return api.get(`/agencies/${agencyId}`);
}

/**
 * Query hook for single agency detail
 * @param {string} agencyId
 * @param {object} [options]
 */
export function useAgencyDetailQuery(agencyId, options) {
  return useQuery({
    queryKey: agencyKeys.detail(agencyId),
    queryFn: () => getAgency(agencyId),
    enabled: Boolean(agencyId),
    ...options,
  });
}

/**
 * GET /agencies/my/fleet or /agencies/{agencyId}/fleet
 * Retrieves fleet vehicles and drivers for the caller's agency or specified agency.
 * @param {string} [agencyId]
 * @returns {Promise<{ agencyId: string, agencyName: string, vehicles: Array<object>, drivers: Array<object> }>}
 */
export async function getAgencyFleet(agencyId) {
  if (agencyId) {
    return api.get(`/agencies/${agencyId}/fleet`);
  }
  return api.get("/agencies/my/fleet");
}

/**
 * Query hook for agency fleet (vehicles & drivers)
 * @param {string} [agencyId]
 * @param {object} [options]
 */
export function useAgencyFleetQuery(agencyId, options) {
  return useQuery({
    queryKey: agencyKeys.fleet(agencyId),
    queryFn: () => getAgencyFleet(agencyId),
    ...options,
  });
}
