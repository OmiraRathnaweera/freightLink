import { describe, expect, it } from 'vitest'
import { fuelRateSchema, vehicleEfficiencySchema, pricingFormulaSchema } from '../../../lib/validationSchemas.js'

// Mirrors the style of features/loads/tests/lib/validationSchemas.test.js —
// each test starts from a minimal, fully-valid payload and knocks out or
// perturbs exactly one field.

async function getError(schema, payload) {
  try {
    await schema.validate(payload, { abortEarly: false })
    return null
  } catch (error) {
    return error
  }
}

describe('fuelRateSchema', () => {
  function validPayload(overrides = {}) {
    return {
      fuelType: 'AutoDiesel',
      pricePerLitre: 350.5,
      source: 'CPC official price list',
      effectiveFrom: '2026-09-10T09:00',
      ...overrides,
    }
  }

  it('passes for a fully valid payload', async () => {
    await expect(fuelRateSchema.validate(validPayload())).resolves.toBeTruthy()
  })

  it('requires fuelType', async () => {
    const error = await getError(fuelRateSchema, validPayload({ fuelType: '' }))
    const fieldError = error.inner.find((inner) => inner.path === 'fuelType')
    expect(fieldError.message).toBe('Fuel type is required')
  })

  it('requires source', async () => {
    const error = await getError(fuelRateSchema, validPayload({ source: '' }))
    const fieldError = error.inner.find((inner) => inner.path === 'source')
    expect(fieldError.message).toBe('Source is required')
  })

  it('rejects a source over 500 characters', async () => {
    const error = await getError(fuelRateSchema, validPayload({ source: 'x'.repeat(501) }))
    const fieldError = error.inner.find((inner) => inner.path === 'source')
    expect(fieldError.message).toBe('Source must be 500 characters or fewer')
  })

  it('requires effectiveFrom', async () => {
    const error = await getError(fuelRateSchema, validPayload({ effectiveFrom: '' }))
    const fieldError = error.inner.find((inner) => inner.path === 'effectiveFrom')
    expect(fieldError.message).toBe('Effective date is required')
  })

  it('requires pricePerLitre (empty numeric field reports "required", not "must be a number")', async () => {
    const error = await getError(fuelRateSchema, validPayload({ pricePerLitre: '' }))
    const fieldError = error.inner.find((inner) => inner.path === 'pricePerLitre')
    expect(fieldError.message).toMatch(/required/i)
  })

  it('rejects a pricePerLitre of zero (below the 0.01 minimum)', async () => {
    const error = await getError(fuelRateSchema, validPayload({ pricePerLitre: 0 }))
    const fieldError = error.inner.find((inner) => inner.path === 'pricePerLitre')
    expect(fieldError.message).toBe('Price per litre must be at least 0.01')
  })

  it('accepts pricePerLitre exactly at the 0.01 minimum', async () => {
    await expect(fuelRateSchema.validate(validPayload({ pricePerLitre: 0.01 }))).resolves.toBeTruthy()
  })

  it('rejects a non-numeric pricePerLitre', async () => {
    const error = await getError(fuelRateSchema, validPayload({ pricePerLitre: 'abc' }))
    const fieldError = error.inner.find((inner) => inner.path === 'pricePerLitre')
    expect(fieldError.message).toBe('Price per litre must be a number')
  })
})

describe('vehicleEfficiencySchema', () => {
  function validPayload(overrides = {}) {
    return {
      classLabel: 'MiniTruck',
      minPayloadKg: 0,
      maxPayloadKg: 1000,
      minVolumeM3: 0,
      maxVolumeM3: 10,
      fuelConsumptionLPer100Km: 15,
      source: 'Manufacturer spec sheet',
      effectiveFrom: '2026-09-10T09:00',
      ...overrides,
    }
  }

  it('passes for a fully valid payload', async () => {
    await expect(vehicleEfficiencySchema.validate(validPayload())).resolves.toBeTruthy()
  })

  it('requires classLabel', async () => {
    const error = await getError(vehicleEfficiencySchema, validPayload({ classLabel: '' }))
    const fieldError = error.inner.find((inner) => inner.path === 'classLabel')
    expect(fieldError.message).toBe('Vehicle class is required')
  })

  it('allows maxPayloadKg to be left blank for an open-ended top tier', async () => {
    await expect(
      vehicleEfficiencySchema.validate(validPayload({ maxPayloadKg: '' })),
    ).resolves.toBeTruthy()
  })

  it('rejects a maxPayloadKg that is not greater than minPayloadKg', async () => {
    const error = await getError(
      vehicleEfficiencySchema,
      validPayload({ minPayloadKg: 500, maxPayloadKg: 500 }),
    )
    const fieldError = error.inner.find((inner) => inner.path === 'maxPayloadKg')
    expect(fieldError.message).toBe('Max payload must be greater than min payload')
  })

  it('accepts a maxPayloadKg greater than minPayloadKg', async () => {
    await expect(
      vehicleEfficiencySchema.validate(validPayload({ minPayloadKg: 500, maxPayloadKg: 501 })),
    ).resolves.toBeTruthy()
  })

  it('allows maxVolumeM3 to be left blank for an open-ended top tier', async () => {
    await expect(
      vehicleEfficiencySchema.validate(validPayload({ maxVolumeM3: '' })),
    ).resolves.toBeTruthy()
  })

  it('rejects a maxVolumeM3 that is not greater than minVolumeM3', async () => {
    const error = await getError(
      vehicleEfficiencySchema,
      validPayload({ minVolumeM3: 5, maxVolumeM3: 5 }),
    )
    const fieldError = error.inner.find((inner) => inner.path === 'maxVolumeM3')
    expect(fieldError.message).toBe('Max volume must be greater than min volume')
  })

  it('requires fuelConsumptionLPer100Km to be at least 0.01', async () => {
    const error = await getError(vehicleEfficiencySchema, validPayload({ fuelConsumptionLPer100Km: 0 }))
    const fieldError = error.inner.find((inner) => inner.path === 'fuelConsumptionLPer100Km')
    expect(fieldError.message).toBe('Fuel consumption must be at least 0.01')
  })
})

describe('pricingFormulaSchema', () => {
  function validPayload(overrides = {}) {
    return {
      baseFare: 500,
      ratePerKg: 10,
      driverCostPerKm: 20,
      maintenanceAllowancePerKm: 5,
      marginPercent: 0.15,
      source: 'Ops committee decision',
      effectiveFrom: '2026-09-10T09:00',
      ...overrides,
    }
  }

  it('passes for a fully valid payload', async () => {
    await expect(pricingFormulaSchema.validate(validPayload())).resolves.toBeTruthy()
  })

  it('rejects a marginPercent above 1 (100%) — the field is a fraction, not a 0-100 percent', async () => {
    const error = await getError(pricingFormulaSchema, validPayload({ marginPercent: 1.5 }))
    const fieldError = error.inner.find((inner) => inner.path === 'marginPercent')
    expect(fieldError.message).toBe('Margin must be at most 1')
  })

  it('accepts marginPercent exactly at the 1 (100%) boundary', async () => {
    await expect(pricingFormulaSchema.validate(validPayload({ marginPercent: 1 }))).resolves.toBeTruthy()
  })

  it('rejects a negative baseFare', async () => {
    const error = await getError(pricingFormulaSchema, validPayload({ baseFare: -1 }))
    const fieldError = error.inner.find((inner) => inner.path === 'baseFare')
    expect(fieldError.message).toBe('Base fare must be at least 0')
  })

  it('requires source and effectiveFrom', async () => {
    const error = await getError(pricingFormulaSchema, validPayload({ source: '', effectiveFrom: '' }))
    expect(error.inner.some((inner) => inner.path === 'source')).toBe(true)
    expect(error.inner.some((inner) => inner.path === 'effectiveFrom')).toBe(true)
  })
})
