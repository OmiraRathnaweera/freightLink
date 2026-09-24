import { useQuery } from "@tanstack/react-query";
import { api } from "../../../lib/api/api.js";

export const assignmentKeys = {
  all: ["assignments"],
  lists: () => [...assignmentKeys.all, "list"],
  list: (params) => [...assignmentKeys.lists(), params],
  details: () => [...assignmentKeys.all, "detail"],
  detail: (id) => [...assignmentKeys.details(), id],
};

/**
 * GET /assignments — paginated list of assignments / proposals.
 * @param {{ page?: number, pageSize?: number, status?: string, search?: string, hasTrip?: boolean, sortBy?: string, sortDir?: string }} [params]
 * @returns {Promise<{ items: Array<object>, page: number, pageSize: number, totalItems: number, totalPages: number }>}
 */
export async function listAssignments(params) {
  return api.get("/assignments", { params });
}

/** Query hook for listing assignments */
export function useAssignmentsQuery(params, options) {
  return useQuery({
    queryKey: assignmentKeys.list(params),
    queryFn: () => listAssignments(params),
    ...options,
  });
}

/**
 * GET /assignments/{id} — single assignment detail.
 * @param {string} assignmentId
 */
export async function getAssignment(assignmentId) {
  return api.get(`/assignments/${assignmentId}`);
}

/** Query hook for single assignment detail */
export function useAssignmentDetailQuery(assignmentId, options) {
  return useQuery({
    queryKey: assignmentKeys.detail(assignmentId),
    queryFn: () => getAssignment(assignmentId),
    enabled: Boolean(assignmentId),
    ...options,
  });
}
