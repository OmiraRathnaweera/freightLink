import { describe, expect, it } from 'vitest'
import { AgencyStatus } from '../../../../../lib/enums.js'
import {
  canTransitionAgency,
  getAllowedNextStatuses,
} from '../../../lib/agencyStatusTransitions.js'
import { agencyStatusChangeSchema } from '../../../lib/validationSchemas.js'

describe('agency status transitions (issue #56)', () => {
  it('lets a suspended agency be reactivated', () => {
    expect(canTransitionAgency(AgencyStatus.SUSPENDED, AgencyStatus.ACTIVE)).toBe(true)
  })

  it('lets an active agency be suspended', () => {
    expect(canTransitionAgency(AgencyStatus.ACTIVE, AgencyStatus.SUSPENDED)).toBe(true)
  })

  it('does not let a pending agency skip verification', () => {
    expect(canTransitionAgency(AgencyStatus.PENDING, AgencyStatus.ACTIVE)).toBe(false)
  })

  it('has no terminal status', () => {
    Object.values(AgencyStatus).forEach((status) => {
      expect(getAllowedNextStatuses(status).length).toBeGreaterThan(0)
    })
  })

  it('returns no transitions for an unknown status', () => {
    expect(getAllowedNextStatuses('Bogus')).toEqual([])
  })
})

describe('agencyStatusChangeSchema', () => {
  it('requires a non-blank reason', async () => {
    await expect(agencyStatusChangeSchema.validate({ reason: '   ' })).rejects.toThrow(/reason is required/i)
  })

  it('rejects a reason over 500 characters', async () => {
    await expect(agencyStatusChangeSchema.validate({ reason: 'x'.repeat(501) })).rejects.toThrow(/500/)
  })

  it('accepts and trims a valid reason', async () => {
    const result = await agencyStatusChangeSchema.validate({ reason: '  Suspended in error ' })
    expect(result.reason).toBe('Suspended in error')
  })
})
