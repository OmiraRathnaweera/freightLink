import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import InvoiceStatusBadge from '../../../components/InvoiceStatusBadge.jsx'
import { InvoiceStatus } from '../../../../../lib/enums.js'

// Guards against the status-vocabulary drift found in the Sep 27 2026 audit:
// every real backend status must render, and the invented 'Overdue'/'Refunded'
// values (never returned by the API) must not exist anywhere in this mapping.
describe('InvoiceStatusBadge', () => {
  it.each(Object.values(InvoiceStatus))('renders the real backend status "%s"', (status) => {
    render(<InvoiceStatusBadge status={status} />)
    expect(screen.getByText(status)).toBeInTheDocument()
  })

  it('does not reference invented statuses that the backend never returns', () => {
    expect(InvoiceStatus.Overdue).toBeUndefined()
    expect(InvoiceStatus.Refunded).toBeUndefined()
    expect(Object.values(InvoiceStatus)).not.toContain('Overdue')
    expect(Object.values(InvoiceStatus)).not.toContain('Refunded')
  })

  it('falls back to a neutral style for an unrecognized status rather than throwing', () => {
    render(<InvoiceStatusBadge status="SomeUnknownStatus" />)
    expect(screen.getByText('SomeUnknownStatus')).toBeInTheDocument()
  })
})
