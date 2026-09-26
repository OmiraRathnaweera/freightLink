/**
 * Resolves a storage key, publicId, or relative/absolute URL to a fully-qualified URL.
 *
 * 1. If keyOrUrl is already an absolute HTTP/HTTPS URL (e.g. Cloudinary direct URL), return as-is.
 * 2. If keyOrUrl starts with data: or blob:, return as-is.
 * 3. If keyOrUrl is a relative backend path starting with '/', prepend the backend origin.
 * 4. If keyOrUrl is a publicId or storageKey (e.g. 'freightlink/hqo8acb1xq4rmtauo1ti.jpg'),
 *    route to the backend's content endpoint: `${origin}/api/v1/files/content/${cleanKey}`.
 *
 * @param {string|null|undefined} keyOrUrl
 * @returns {string}
 */
export function getFileUrl(keyOrUrl) {
  if (!keyOrUrl || typeof keyOrUrl !== 'string') return ''

  const trimmed = keyOrUrl.trim()
  if (!trimmed) return ''

  if (
    trimmed.startsWith('http://') ||
    trimmed.startsWith('https://') ||
    trimmed.startsWith('data:') ||
    trimmed.startsWith('blob:')
  ) {
    return trimmed
  }

  const apiBase = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5159/api/v1'
  const origin = apiBase.replace(/\/api\/v1\/?$/, '')

  if (trimmed.startsWith('/')) {
    return `${origin}${trimmed}`
  }

  return `${origin}/api/v1/files/content/${trimmed}`
}
