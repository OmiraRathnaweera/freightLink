import { describe, expect, it } from 'vitest'
import { addVehicleSchema } from '../../lib/validationSchemas.js'

describe('addVehicleSchema', () => {
  const validPayload = {
    vehicleType: 'MiniTruck',
    registrationNo: 'WP-CAD-1234',
    capacityKg: 2000,
    volumeM3: 12.5,
  }

  it('validates a correct vehicle registration payload', async () => {
    const validated = await addVehicleSchema.validate(validPayload)
    expect(validated.registrationNo).toBe('WP-CAD-1234')
    expect(validated.capacityKg).toBe(2000)
    expect(validated.volumeM3).toBe(12.5)
    expect(validated.vehicleType).toBe('MiniTruck')
  })

  it('rejects missing or invalid vehicleType', async () => {
    await expect(
      addVehicleSchema.validate({ ...validPayload, vehicleType: '' }),
    ).rejects.toThrow(/vehicle class is required/i)

    await expect(
      addVehicleSchema.validate({ ...validPayload, vehicleType: 'Helicopter' }),
    ).rejects.toThrow(/select a valid vehicle class/i)
  })

  describe('Sri Lankan vehicle registration number pattern', () => {
    it('accepts valid Sri Lankan registration patterns', async () => {
      const validNumbers = [
        'WP-CAB-1234',
        'WP CAB-1234',
        'WPCAB-1234',
        'CAB-1234',
        'WP-CAD-1020',
        'WP-LH-5544',
        'CP-CONT-9988',
        'WP-DA-9988',
        'WP-REP-5555',
        'SP-GA-5678',
        '228-1234',
        'WP-228-1234',
        '40-1234',
        'cab-5678',
        'wp-cab-1234',
      ]

      for (const regNo of validNumbers) {
        await expect(
          addVehicleSchema.validate({ ...validPayload, registrationNo: regNo }),
        ).resolves.toBeTruthy()
      }
    })

    it('rejects empty, malformed, or non-Sri Lankan registration numbers', async () => {
      await expect(
        addVehicleSchema.validate({ ...validPayload, registrationNo: '' }),
      ).rejects.toThrow(/registration number is required/i)

      const invalidNumbers = [
        'AB',
        '12345',
        'Hello World',
        'US-CAB-1234',
        'WP-CAB-12345',
        'WP@CAB#1234',
        'NY-1234-AB',
        'WP-123',
        'CAB-123',
      ]

      for (const regNo of invalidNumbers) {
        await expect(
          addVehicleSchema.validate({ ...validPayload, registrationNo: regNo }),
        ).rejects.toThrow(/enter a valid sri lankan vehicle registration number/i)
      }
    })
  })

  describe('vehicle class weight limitations', () => {
    it('enforces Mini Truck capacity limits (up to 2,500 kg)', async () => {
      // Valid Mini Truck capacities
      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'MiniTruck', capacityKg: 1 }),
      ).resolves.toBeTruthy()

      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'MiniTruck', capacityKg: 2500 }),
      ).resolves.toBeTruthy()

      // Invalid: exceeds 2,500 kg
      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'MiniTruck', capacityKg: 2501 }),
      ).rejects.toThrow(/capacity for mini truck cannot exceed 2,500 kg/i)

      // Invalid: zero or negative
      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'MiniTruck', capacityKg: 0 }),
      ).rejects.toThrow(/capacity for mini truck must be at least 1 kg/i)
    })

    it('enforces Medium Lorry capacity limits (2,500 – 10,000 kg)', async () => {
      // Below 2,500 kg
      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'MediumLorry', capacityKg: 2499 }),
      ).rejects.toThrow(/capacity for medium lorry must be at least 2,500 kg/i)

      // Valid boundary values
      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'MediumLorry', capacityKg: 2500 }),
      ).resolves.toBeTruthy()

      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'MediumLorry', capacityKg: 6000 }),
      ).resolves.toBeTruthy()

      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'MediumLorry', capacityKg: 10000 }),
      ).resolves.toBeTruthy()

      // Exceeds 10,000 kg
      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'MediumLorry', capacityKg: 10001 }),
      ).rejects.toThrow(/capacity for medium lorry cannot exceed 10,000 kg/i)
    })

    it('enforces Container Truck capacity limits (10,000 – 100,000 kg)', async () => {
      // Below 10,000 kg
      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'ContainerTruck', capacityKg: 9999 }),
      ).rejects.toThrow(/capacity for container truck must be at least 10,000 kg/i)

      // Valid boundary values
      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'ContainerTruck', capacityKg: 10000 }),
      ).resolves.toBeTruthy()

      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'ContainerTruck', capacityKg: 30000 }),
      ).resolves.toBeTruthy()

      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'ContainerTruck', capacityKg: 100000 }),
      ).resolves.toBeTruthy()

      // Exceeds system max 100,000 kg
      await expect(
        addVehicleSchema.validate({ ...validPayload, vehicleType: 'ContainerTruck', capacityKg: 100001 }),
      ).rejects.toThrow(/capacity for container truck cannot exceed 100,000 kg/i)
    })
  })

  it('rejects volumeM3 that is non-numeric, zero, negative, or exceeds limit', async () => {
    await expect(
      addVehicleSchema.validate({ ...validPayload, volumeM3: '' }),
    ).rejects.toThrow(/volume is required/i)

    await expect(
      addVehicleSchema.validate({ ...validPayload, volumeM3: 0 }),
    ).rejects.toThrow(/greater than 0/i)

    await expect(
      addVehicleSchema.validate({ ...validPayload, volumeM3: -5 }),
    ).rejects.toThrow(/greater than 0/i)

    await expect(
      addVehicleSchema.validate({ ...validPayload, volumeM3: 1001 }),
    ).rejects.toThrow(/cannot exceed 1,000 m³/i)
  })
})
