import { describe, expect, it } from 'vitest'
import { cancelLoadSchema, createLoadSchema, editLoadSchema } from '../../../lib/validationSchemas.js'

// A minimal, fully-valid createLoadSchema payload — every field-level test
// below starts from this and knocks out exactly one field, so a failure
// clearly points at the rule under test rather than an unrelated one.
function validPayload(overrides = {}) {
  return {
    cargoDescription: 'Pallets of canned goods',
    weightKg: 1200.5,
    volumeM3: 8.25,
    pickupAddress: '123 Galle Road, Colombo',
    pickupLat: 6.9271,
    pickupLng: 79.8612,
    dropoffAddress: '45 Kandy Road, Kandy',
    dropoffLat: 7.2906,
    dropoffLng: 80.6337,
    pickupWindowStart: '2026-09-10T09:00:00.000Z',
    pickupWindowEnd: '2026-09-10T17:00:00.000Z',
    postImmediately: false,
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

describe('createLoadSchema', () => {
  it('passes for a fully valid payload', async () => {
    await expect(createLoadSchema.validate(validPayload())).resolves.toBeTruthy()
  })

  it.each([
    ['cargoDescription', ''],
    ['pickupAddress', ''],
    ['dropoffAddress', ''],
    ['pickupWindowStart', ''],
    ['pickupWindowEnd', ''],
  ])('requires %s', async (field, emptyValue) => {
    const error = await getError(createLoadSchema, validPayload({ [field]: emptyValue }))
    expect(error).toBeTruthy()
    expect(error.inner.some((inner) => inner.path === field)).toBe(true)
  })

  it.each([
    ['weightKg', ''],
    ['volumeM3', ''],
  ])('requires %s (empty numeric field reports "required", not "must be a number")', async (field, emptyValue) => {
    const error = await getError(createLoadSchema, validPayload({ [field]: emptyValue }))
    const fieldError = error.inner.find((inner) => inner.path === field)
    expect(fieldError).toBeTruthy()
    expect(fieldError.message).toMatch(/required/i)
  })

  it('rejects a weight below the minimum', async () => {
    const error = await getError(createLoadSchema, validPayload({ weightKg: 0 }))
    const fieldError = error.inner.find((inner) => inner.path === 'weightKg')
    expect(fieldError.message).toBe('Weight must be at least 0.01')
  })

  it('rejects a weight above the maximum', async () => {
    const error = await getError(createLoadSchema, validPayload({ weightKg: 100000000 }))
    const fieldError = error.inner.find((inner) => inner.path === 'weightKg')
    expect(fieldError.message).toBe('Weight must be 99999999.99 or less')
  })

  it('rejects a volume below the minimum', async () => {
    const error = await getError(createLoadSchema, validPayload({ volumeM3: 0 }))
    const fieldError = error.inner.find((inner) => inner.path === 'volumeM3')
    expect(fieldError.message).toBe('Volume must be at least 0.001')
  })

  it('rejects a volume above the maximum', async () => {
    const error = await getError(createLoadSchema, validPayload({ volumeM3: 10000000 }))
    const fieldError = error.inner.find((inner) => inner.path === 'volumeM3')
    expect(fieldError.message).toBe('Volume must be 9999999.999 or less')
  })

  it('accepts weight/volume exactly at their boundary values', async () => {
    await expect(
      createLoadSchema.validate(validPayload({ weightKg: 0.01, volumeM3: 0.001 })),
    ).resolves.toBeTruthy()
    await expect(
      createLoadSchema.validate(validPayload({ weightKg: 99999999.99, volumeM3: 9999999.999 })),
    ).resolves.toBeTruthy()
  })

  it('rejects a pickupWindowEnd that is before pickupWindowStart', async () => {
    const error = await getError(
      createLoadSchema,
      validPayload({ pickupWindowStart: '2026-09-10T17:00:00.000Z', pickupWindowEnd: '2026-09-10T09:00:00.000Z' }),
    )
    const fieldError = error.inner.find((inner) => inner.path === 'pickupWindowEnd')
    expect(fieldError.message).toBe('Pickup window end must be after the start')
  })

  it('rejects a pickupWindowEnd equal to pickupWindowStart', async () => {
    const sameInstant = '2026-09-10T09:00:00.000Z'
    const error = await getError(
      createLoadSchema,
      validPayload({ pickupWindowStart: sameInstant, pickupWindowEnd: sameInstant }),
    )
    const fieldError = error.inner.find((inner) => inner.path === 'pickupWindowEnd')
    expect(fieldError).toBeTruthy()
  })

  it('accepts a pickupWindowEnd after pickupWindowStart', async () => {
    await expect(
      createLoadSchema.validate(
        validPayload({ pickupWindowStart: '2026-09-10T09:00:00.000Z', pickupWindowEnd: '2026-09-10T09:00:01.000Z' }),
      ),
    ).resolves.toBeTruthy()
  })

  it('rejects identical pickup/dropoff coordinates', async () => {
    const error = await getError(
      createLoadSchema,
      validPayload({ dropoffLat: 6.9271, dropoffLng: 79.8612 }), // same as pickupLat/pickupLng above
    )
    const fieldError = error.inner.find((inner) => inner.path === 'dropoffLat')
    expect(fieldError.message).toBe('Pickup and dropoff locations cannot be identical')
  })

  it('accepts pickup/dropoff coordinates that differ', async () => {
    await expect(createLoadSchema.validate(validPayload())).resolves.toBeTruthy()
  })
})

describe('editLoadSchema', () => {
  it('has no postImmediately field but otherwise enforces the same rules', async () => {
    const payload = validPayload()
    delete payload.postImmediately
    await expect(editLoadSchema.validate(payload)).resolves.toBeTruthy()

    const error = await getError(editLoadSchema, { ...payload, cargoDescription: '' })
    expect(error.inner.some((inner) => inner.path === 'cargoDescription')).toBe(true)
  })

  it('rejects identical pickup/dropoff coordinates', async () => {
    const payload = validPayload({ dropoffLat: 6.9271, dropoffLng: 79.8612 })
    delete payload.postImmediately
    const error = await getError(editLoadSchema, payload)
    expect(error.inner.some((inner) => inner.path === 'dropoffLat')).toBe(true)
  })
})

describe('cancelLoadSchema', () => {
  it('requires a reason', async () => {
    const error = await getError(cancelLoadSchema, { reason: '' })
    expect(error.inner[0].message).toBe('A cancellation reason is required')
  })

  it('rejects a reason over 500 characters', async () => {
    const error = await getError(cancelLoadSchema, { reason: 'x'.repeat(501) })
    expect(error.inner[0].message).toBe('Reason must be 500 characters or fewer')
  })

  it('accepts a valid reason', async () => {
    await expect(cancelLoadSchema.validate({ reason: 'Shipper found an alternate carrier' })).resolves.toBeTruthy()
  })
})
