import { describe, expect, it } from 'vitest'
import {
  loginSchema,
  shipperRegisterSchema,
  agencyRegisterSchema,
} from '../../lib/validationSchemas.js'

function validShipperPayload(overrides = {}) {
  return {
    fullName: 'Jane Doe',
    email: 'jane@acme.lk',
    password: 'Password123!',
    phoneE164: '+94771234567',
    companyName: 'Acme Shipping Corp',
    businessRegNo: 'PV12345',
    billingAddress: '123 Galle Road, Colombo 03',
    ...overrides,
  }
}

function validAgencyPayload(overrides = {}) {
  return {
    fullName: 'John Doe',
    email: 'john@fastfreight.lk',
    password: 'Password123!',
    phoneE164: '+94771234567',
    jobTitle: 'Fleet Manager',
    agencyName: 'Fast Freight Logistics',
    businessRegNo: 'PV98765',
    yardAddress: '45 Harbor Road, Peliyagoda',
    yardLat: 6.9583,
    yardLng: 79.8833,
    ...overrides,
  }
}

async function getError(schema, payload) {
  try {
    await schema.validate(payload, { abortEarly: false })
    return null
  } catch (error) {
    return error
  }
}

describe('loginSchema', () => {
  it('validates a valid login payload', async () => {
    await expect(
      loginSchema.validate({ email: 'operator@freight.lk', password: 'Password123!' }),
    ).resolves.toBeTruthy()
  })

  it('rejects an invalid email and short password', async () => {
    const error = await getError(loginSchema, { email: 'bad-email', password: '123' })
    expect(error).toBeTruthy()
    expect(error.inner.some((i) => i.path === 'email')).toBe(true)
    expect(error.inner.some((i) => i.path === 'password')).toBe(true)
  })
})

describe('shipperRegisterSchema', () => {
  it('validates a fully valid shipper payload', async () => {
    await expect(shipperRegisterSchema.validate(validShipperPayload())).resolves.toBeTruthy()
  })

  it('allows optional fields (phoneE164, businessRegNo) to be empty strings or undefined', async () => {
    const payload = validShipperPayload({ phoneE164: '', businessRegNo: '' })
    await expect(shipperRegisterSchema.validate(payload)).resolves.toBeTruthy()
  })

  it.each([
    ['fullName', ''],
    ['email', ''],
    ['password', ''],
    ['companyName', ''],
    ['billingAddress', ''],
  ])('requires %s', async (field, emptyValue) => {
    const error = await getError(shipperRegisterSchema, validShipperPayload({ [field]: emptyValue }))
    expect(error).toBeTruthy()
    expect(error.inner.some((i) => i.path === field)).toBe(true)
  })

  it('enforces strong password criteria', async () => {
    // Missing uppercase
    let error = await getError(shipperRegisterSchema, validShipperPayload({ password: 'password123!' }))
    expect(error.inner.some((i) => i.path === 'password')).toBe(true)

    // Missing lowercase
    error = await getError(shipperRegisterSchema, validShipperPayload({ password: 'PASSWORD123!' }))
    expect(error.inner.some((i) => i.path === 'password')).toBe(true)

    // Missing digit
    error = await getError(shipperRegisterSchema, validShipperPayload({ password: 'Password!' }))
    expect(error.inner.some((i) => i.path === 'password')).toBe(true)

    // Missing special character
    error = await getError(shipperRegisterSchema, validShipperPayload({ password: 'Password123' }))
    expect(error.inner.some((i) => i.path === 'password')).toBe(true)

    // Too short (< 8)
    error = await getError(shipperRegisterSchema, validShipperPayload({ password: 'Pass1!' }))
    expect(error.inner.some((i) => i.path === 'password')).toBe(true)
  })

  it('validates E.164 phone format when provided', async () => {
    const error = await getError(shipperRegisterSchema, validShipperPayload({ phoneE164: '0771234567' }))
    expect(error.inner.some((i) => i.path === 'phoneE164')).toBe(true)
  })

  it('validates fullName length and allowed characters', async () => {
    const error = await getError(shipperRegisterSchema, validShipperPayload({ fullName: 'A' }))
    expect(error.inner.some((i) => i.path === 'fullName')).toBe(true)
  })

  it('validates billingAddress minimum length', async () => {
    const error = await getError(shipperRegisterSchema, validShipperPayload({ billingAddress: '123' }))
    expect(error.inner.some((i) => i.path === 'billingAddress')).toBe(true)
  })
})

describe('agencyRegisterSchema', () => {
  it('validates a fully valid agency payload', async () => {
    await expect(agencyRegisterSchema.validate(validAgencyPayload())).resolves.toBeTruthy()
  })

  it('allows optional fields (phoneE164, jobTitle) to be empty strings', async () => {
    const payload = validAgencyPayload({ phoneE164: '', jobTitle: '' })
    await expect(agencyRegisterSchema.validate(payload)).resolves.toBeTruthy()
  })

  it.each([
    ['fullName', ''],
    ['email', ''],
    ['password', ''],
    ['agencyName', ''],
    ['businessRegNo', ''],
    ['yardAddress', ''],
    ['yardLat', ''],
    ['yardLng', ''],
  ])('requires %s', async (field, emptyValue) => {
    const error = await getError(agencyRegisterSchema, validAgencyPayload({ [field]: emptyValue }))
    expect(error).toBeTruthy()
    expect(error.inner.some((i) => i.path === field)).toBe(true)
  })

  it('validates coordinate boundaries', async () => {
    // Latitude out of range
    let error = await getError(agencyRegisterSchema, validAgencyPayload({ yardLat: 95.0 }))
    expect(error.inner.some((i) => i.path === 'yardLat')).toBe(true)

    error = await getError(agencyRegisterSchema, validAgencyPayload({ yardLat: -95.0 }))
    expect(error.inner.some((i) => i.path === 'yardLat')).toBe(true)

    // Longitude out of range
    error = await getError(agencyRegisterSchema, validAgencyPayload({ yardLng: 185.0 }))
    expect(error.inner.some((i) => i.path === 'yardLng')).toBe(true)

    error = await getError(agencyRegisterSchema, validAgencyPayload({ yardLng: -185.0 }))
    expect(error.inner.some((i) => i.path === 'yardLng')).toBe(true)
  })

  it('rejects non-numeric coordinates', async () => {
    const error = await getError(agencyRegisterSchema, validAgencyPayload({ yardLat: 'not-a-number' }))
    expect(error.inner.some((i) => i.path === 'yardLat')).toBe(true)
  })
})
