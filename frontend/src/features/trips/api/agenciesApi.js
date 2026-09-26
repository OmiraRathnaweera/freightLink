import { useQuery } from "@tanstack/react-query";
import { api } from "../../../lib/api/api.js";

export const agencyKeys = {
  all: ["agencies"],
  fleets: () => [...agencyKeys.all, "fleet"],
  fleet: (id) => [...agencyKeys.fleets(), id ?? "my"],
};

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
