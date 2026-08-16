import { axiosClient } from './axiosClient.js'

/**
 * Thin verb helpers over `axiosClient`, unwrapped to `response.data`
 * directly — per the API contract (api-contract-openapi-skeleton.md
 * Section 2.1), a single-resource endpoint's `data` *is* the resource, and
 * a list endpoint's `data` *is* the `{ items, page, pageSize, totalItems,
 * totalPages }` paging envelope. Feature `*Api.js` modules call these
 * instead of `axiosClient` directly.
 */
export const api = {
  get: (url, config) => axiosClient.get(url, config).then((response) => response.data),
  post: (url, data, config) => axiosClient.post(url, data, config).then((response) => response.data),
  put: (url, data, config) => axiosClient.put(url, data, config).then((response) => response.data),
  patch: (url, data, config) => axiosClient.patch(url, data, config).then((response) => response.data),
  delete: (url, config) => axiosClient.delete(url, config).then((response) => response.data),
}
