import { describe, it, expect } from 'vitest'
import {
  DisputeStatus,
  canStartReview,
  canResolveDispute,
  isDisputeResolved,
  getStatusTone,
  getStatusLabel,
  validateResolutionPayload,
} from '../../lib/disputeRules.js'

describe('Dispute State Machine Rules (Y3S01-81)', () => {
  describe('canStartReview', () => {
    it('returns true only for Raised status', () => {
      expect(canStartReview(DisputeStatus.RAISED)).toBe(true)
      expect(canStartReview(DisputeStatus.UNDER_REVIEW)).toBe(false)
      expect(canStartReview(DisputeStatus.RESOLVED)).toBe(false)
    })
  })

  describe('canResolveDispute', () => {
    it('returns true only for UnderReview status (strict sequential progression)', () => {
      expect(canResolveDispute(DisputeStatus.UNDER_REVIEW)).toBe(true)
      expect(canResolveDispute(DisputeStatus.RAISED)).toBe(false) // cannot skip UnderReview
      expect(canResolveDispute(DisputeStatus.RESOLVED)).toBe(false)
    })
  })

  describe('isDisputeResolved', () => {
    it('returns true only for Resolved status', () => {
      expect(isDisputeResolved(DisputeStatus.RESOLVED)).toBe(true)
      expect(isDisputeResolved(DisputeStatus.RAISED)).toBe(false)
      expect(isDisputeResolved(DisputeStatus.UNDER_REVIEW)).toBe(false)
    })
  })

  describe('getStatusTone', () => {
    it('maps statuses to the appropriate design system tones', () => {
      expect(getStatusTone(DisputeStatus.RAISED)).toBe('amber')
      expect(getStatusTone(DisputeStatus.UNDER_REVIEW)).toBe('blue')
      expect(getStatusTone(DisputeStatus.RESOLVED)).toBe('green')
    })
  })

  describe('getStatusLabel', () => {
    it('returns user-friendly labels', () => {
      expect(getStatusLabel(DisputeStatus.RAISED)).toBe('Raised')
      expect(getStatusLabel(DisputeStatus.UNDER_REVIEW)).toBe('Under Review')
      expect(getStatusLabel(DisputeStatus.RESOLVED)).toBe('Resolved')
    })
  })

  describe('validateResolutionPayload', () => {
    it('fails when resolution note is empty or whitespace only', () => {
      expect(validateResolutionPayload('')).toBeTruthy()
      expect(validateResolutionPayload('   ')).toBeTruthy()
      expect(validateResolutionPayload(null)).toBeTruthy()
      expect(validateResolutionPayload(undefined)).toBeTruthy()
    })

    it('fails when resolution note is too short', () => {
      expect(validateResolutionPayload('ok')).toContain('at least 10 characters')
    })

    it('passes when a meaningful resolution note is supplied', () => {
      expect(
        validateResolutionPayload('Verified fuel surcharge clause; adjusted excess debit to credit invoice.'),
      ).toBeNull()
    })
  })
})
