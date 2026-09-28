import { api } from '../../../lib/api/api.js'

/**
 * Consumes an email Accept/Decline action-link token (POST /assignment-actions/respond).
 * Public, unauthenticated endpoint - works from a browser with no active session.
 * @param {string} token
 */
export async function respondToAssignmentAction(token) {
  return api.post('/assignment-actions/respond', { token })
}
