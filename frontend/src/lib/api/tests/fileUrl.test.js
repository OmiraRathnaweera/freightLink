import { describe, expect, it } from 'vitest'
import { getFileUrl } from '../fileUrl.js'

describe('getFileUrl', () => {
  it('returns empty string when input is falsy', () => {
    expect(getFileUrl(null)).toBe('')
    expect(getFileUrl(undefined)).toBe('')
    expect(getFileUrl('')).toBe('')
  })

  it('returns original URL when already absolute http or https', () => {
    expect(getFileUrl('https://res.cloudinary.com/test/image.jpg')).toBe(
      'https://res.cloudinary.com/test/image.jpg',
    )
    expect(getFileUrl('http://example.com/file.pdf')).toBe('http://example.com/file.pdf')
  })

  it('returns data or blob URLs as-is', () => {
    expect(getFileUrl('data:image/png;base64,abc')).toBe('data:image/png;base64,abc')
    expect(getFileUrl('blob:http://localhost/123')).toBe('blob:http://localhost/123')
  })

  it('prepends backend files content route when given a storageKey/publicId', () => {
    const result = getFileUrl('freightlink/hqo8acb1xq4rmtauo1ti.jpg')
    expect(result).toMatch(/\/api\/v1\/files\/content\/freightlink\/hqo8acb1xq4rmtauo1ti\.jpg$/)
  })

  it('prepends backend origin when path starts with /', () => {
    const result = getFileUrl('/api/v1/files/content/test')
    expect(result).toMatch(/\/api\/v1\/files\/content\/test$/)
  })
})
