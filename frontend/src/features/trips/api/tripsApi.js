import { useMutation, useQuery } from "@tanstack/react-query";
import { api } from "../../../lib/api/api.js";
import { axiosClient } from "../../../lib/api/axiosClient.js";
import { queryClient } from "../../../lib/api/queryClient.js";

// Query key factory — mirrors loadKeys.js's hierarchical shape.
export const tripKeys = {
  all: ["trips"],
  lists: () => [...tripKeys.all, "list"],
  list: (params) => [...tripKeys.lists(), params],
  details: () => [...tripKeys.all, "detail"],
  detail: (id) => [...tripKeys.details(), id],
  evidences: (id) => [...tripKeys.detail(id), "evidence"],
};

/**
 * @typedef {object} TripListItem
 * @property {string} tripId
 * @property {string} assignmentId
 * @property {string} loadId
 * @property {string} agencyId
 * @property {string|null} [agencyName]
 * @property {string} vehicleId
 * @property {string} driverId
 * @property {string|null} [driverName]
 * @property {string} status
 * @property {string} createdAt
 * @property {string} updatedAt
 */

/**
 * @typedef {object} TripEventItem
 * @property {string} tripEventId
 * @property {string} recordedByUserId
 * @property {string|null} fromStatus
 * @property {string} toStatus
 * @property {string|null} notes
 * @property {number|null} snapshotLat
 * @property {number|null} snapshotLng
 * @property {string} occurredAt
 */

/**
 * @typedef {object} TripEvidenceItem
 * @property {string} tripEvidenceId
 * @property {string} capturedByUserId
 * @property {string} evidenceType
 * @property {string} storageKey
 * @property {number|null} capturedLat
 * @property {number|null} capturedLng
 * @property {string} capturedAt
 */

/**
 * @typedef {object} TripDetail
 * @property {string} tripId
 * @property {string} assignmentId
 * @property {string} loadId
 * @property {string} agencyId
 * @property {string|null} agencyName
 * @property {string} vehicleId
 * @property {string|null} vehicleRegistrationNo
 * @property {string} driverId
 * @property {string|null} driverName
 * @property {string|null} pickupAddress
 * @property {string|null} dropoffAddress
 * @property {number|null} pickupLat
 * @property {number|null} pickupLng
 * @property {number|null} dropoffLat
 * @property {number|null} dropoffLng
 * @property {string} status
 * @property {string} createdAt
 * @property {string} updatedAt
 * @property {TripEventItem[]} events
 * @property {TripEvidenceItem[]} evidence
 */

/**
 * GET /trips — paginated list.
 * @param {{ page?: number, pageSize?: number, status?: string, agencyId?: string, driverId?: string, sortBy?: string, sortDir?: string }} [params]
 * @returns {Promise<{ items: TripListItem[], page: number, pageSize: number, totalItems: number, totalPages: number }>}
 */
export async function listTrips(params) {
  return api.get("/trips", { params });
}

/** List query, e.g. `useTripsQuery()` for the default first page. */
export function useTripsQuery(params, options) {
  return useQuery({
    queryKey: tripKeys.list(params),
    queryFn: () => listTrips(params),
    ...options,
  });
}

/**
 * GET /trips/{id} — full TripResponseDto detail including events and evidence.
 * @param {string} tripId
 * @returns {Promise<TripDetail>}
 */
export async function getTrip(tripId) {
  return api.get(`/trips/${tripId}`);
}

/** Detail query for a single trip. */
export function useTripDetailQuery(tripId, options) {
  return useQuery({
    queryKey: tripKeys.detail(tripId),
    queryFn: () => getTrip(tripId),
    enabled: Boolean(tripId),
    ...options,
  });
}

/**
 * GET /trips/{id}/evidence — list evidence items for a trip.
 * @param {string} tripId
 * @returns {Promise<TripEvidenceItem[]>}
 */
export async function getTripEvidence(tripId) {
  return api.get(`/trips/${tripId}/evidence`);
}

/** Evidence query for a trip. */
export function useTripEvidenceQuery(tripId, options) {
  return useQuery({
    queryKey: tripKeys.evidences(tripId),
    queryFn: () => getTripEvidence(tripId),
    enabled: Boolean(tripId),
    ...options,
  });
}

/**
 * POST /trips/{id}/status — advance trip status.
 * @param {string} tripId
 * @param {{ targetStatus: string, notes?: string, snapshotLat?: number, snapshotLng?: number }} data
 * @returns {Promise<TripDetail>}
 */
export async function changeTripStatus(tripId, data) {
  return api.post(`/trips/${tripId}/status`, data);
}

/** Mutation hook for advancing trip status. */
export function useChangeTripStatusMutation(options) {
  return useMutation({
    mutationFn: ({ tripId, data }) => changeTripStatus(tripId, data),
    onSuccess: (_, { tripId }) => {
      queryClient.invalidateQueries({ queryKey: tripKeys.detail(tripId) });
      queryClient.invalidateQueries({ queryKey: tripKeys.lists() });
    },
    ...options,
  });
}

/**
 * POST /files/single — upload file for proof of pickup or delivery.
 * @param {File} file
 * @returns {Promise<{ publicId: string, url: string, originalName: string, sizeBytes: number, mimeType: string }>}
 */
export async function uploadSingleFile(file) {
  const formData = new FormData();
  formData.append("file", file);
  const response = await axiosClient.post("/files/single", formData);
  return response.data;
}

/**
 * POST /trips/{id}/evidence — upload proof of pickup or delivery.
 * @param {string} tripId
 * @param {{ publicId: string, evidenceType: string, capturedLat?: number, capturedLng?: number }} data
 * @returns {Promise<TripEvidenceItem>}
 */
export async function uploadTripEvidence(tripId, data) {
  return api.post(`/trips/${tripId}/evidence`, data);
}

/** Mutation hook for uploading trip evidence. */
export function useUploadTripEvidenceMutation(options) {
  return useMutation({
    mutationFn: ({ tripId, data }) => uploadTripEvidence(tripId, data),
    onSuccess: (_, { tripId }) => {
      queryClient.invalidateQueries({ queryKey: tripKeys.detail(tripId) });
      queryClient.invalidateQueries({ queryKey: tripKeys.evidences(tripId) });
      queryClient.invalidateQueries({ queryKey: tripKeys.lists() });
    },
    ...options,
  });
}

/**
 * POST /trips — create and dispatch a new trip.
 * @param {{ assignmentId: string, vehicleId: string, driverId: string, notes?: string }} data
 * @returns {Promise<TripDetail>}
 */
export async function createTrip(data) {
  return api.post("/trips", data);
}

/** Mutation hook for creating a new trip. */
export function useCreateTripMutation(options) {
  return useMutation({
    mutationFn: (data) => createTrip(data),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: tripKeys.lists() });
      if (created?.tripId) {
        queryClient.setQueryData(tripKeys.detail(created.tripId), created);
      }
    },
    ...options,
  });
}

/**
 * PUT /trips/{id} — update/reassign vehicle or driver for an assigned trip.
 * @param {string} tripId
 * @param {{ vehicleId?: string, driverId?: string, notes?: string }} data
 * @returns {Promise<TripDetail>}
 */
export async function updateTrip(tripId, data) {
  return api.put(`/trips/${tripId}`, data);
}

/** Mutation hook for updating an assigned trip. */
export function useUpdateTripMutation(options) {
  return useMutation({
    mutationFn: ({ tripId, data }) => updateTrip(tripId, data),
    onSuccess: (updated, { tripId }) => {
      queryClient.setQueryData(tripKeys.detail(tripId), updated);
      queryClient.invalidateQueries({ queryKey: tripKeys.lists() });
    },
    ...options,
  });
}

/**
 * DELETE /trips/{id} or PATCH /trips/{id}/cancel — cancel an active trip.
 * @param {string} tripId
 * @param {{ reason?: string }} [data]
 * @returns {Promise<TripDetail>}
 */
export async function cancelTrip(tripId, data) {
  return api.patch(`/trips/${tripId}/cancel`, data);
}

/** Mutation hook for cancelling a trip. */
export function useCancelTripMutation(options) {
  return useMutation({
    mutationFn: ({ tripId, data }) => cancelTrip(tripId, data),
    onSuccess: (updated, { tripId }) => {
      queryClient.setQueryData(tripKeys.detail(tripId), updated);
      queryClient.invalidateQueries({ queryKey: tripKeys.lists() });
    },
    ...options,
  });
}

/**
 * DELETE /trips/{id} — permanently delete a trip fully from the database.
 * @param {string} tripId
 * @returns {Promise<void>}
 */
export async function deleteTrip(tripId) {
  return api.delete(`/trips/${tripId}`);
}

/** Mutation hook for permanently deleting a trip fully. */
export function useDeleteTripMutation(options) {
  return useMutation({
    mutationFn: ({ tripId }) => deleteTrip(tripId),
    onSuccess: (_, { tripId }) => {
      queryClient.removeQueries({ queryKey: tripKeys.detail(tripId) });
      queryClient.invalidateQueries({ queryKey: tripKeys.lists() });
      queryClient.invalidateQueries({ queryKey: tripKeys.all });
      queryClient.invalidateQueries({ queryKey: ["assignments"] });
    },
    ...options,
  });
}

