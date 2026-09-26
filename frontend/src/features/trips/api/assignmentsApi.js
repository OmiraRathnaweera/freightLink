import { useMutation, useQuery } from "@tanstack/react-query";
import { api } from "../../../lib/api/api.js";
import { queryClient } from "../../../lib/api/queryClient.js";
import { loadKeys } from "../../loads/api/loadsApi.js";
import { tripKeys } from "./tripsApi.js";

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

/**
 * POST /assignments/{loadId}/accept — Agency accepts a proposed load assignment
 * @param {{ loadId: string, data?: { vehicleId?: string, driverId?: string, notes?: string } }} payload
 */
export async function acceptAssignment({ loadId, data }) {
  return api.post(`/assignments/${loadId}/accept`, data ?? {});
}

/** Mutation hook for accepting a load assignment */
export function useAcceptAssignmentMutation(options = {}) {
  return useMutation({
    mutationFn: acceptAssignment,
    onSuccess: (...args) => {
      queryClient.invalidateQueries({ queryKey: assignmentKeys.all });
      queryClient.invalidateQueries({ queryKey: loadKeys.all });
      queryClient.invalidateQueries({ queryKey: tripKeys.all });
      options.onSuccess?.(...args);
    },
    ...options,
  });
}

/**
 * POST /assignments/{id}/approve — Approve an assignment or direct load
 * @param {{ id: string, data?: { vehicleId?: string, driverId?: string, notes?: string } }} payload
 */
export async function approveAssignment({ id, data }) {
  return api.post(`/assignments/${id}/approve`, data ?? {});
}

/** Mutation hook for approving an assignment */
export function useApproveAssignmentMutation(options = {}) {
  return useMutation({
    mutationFn: approveAssignment,
    onSuccess: (...args) => {
      queryClient.invalidateQueries({ queryKey: assignmentKeys.all });
      queryClient.invalidateQueries({ queryKey: loadKeys.all });
      queryClient.invalidateQueries({ queryKey: tripKeys.all });
      options.onSuccess?.(...args);
    },
    ...options,
  });
}

/**
 * POST /assignments/{loadId}/decline — Agency declines a proposed assignment
 * @param {{ loadId: string, data?: { declineReason?: string } }} payload
 */
export async function declineAssignment({ loadId, data }) {
  return api.post(`/assignments/${loadId}/decline`, data ?? {});
}

/** Mutation hook for declining an assignment */
export function useDeclineAssignmentMutation(options = {}) {
  return useMutation({
    mutationFn: declineAssignment,
    onSuccess: (...args) => {
      queryClient.invalidateQueries({ queryKey: assignmentKeys.all });
      queryClient.invalidateQueries({ queryKey: loadKeys.all });
      options.onSuccess?.(...args);
    },
    ...options,
  });
}
