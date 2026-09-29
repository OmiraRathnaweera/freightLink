import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import AssignmentActionPage from '../../../pages/AssignmentActionPage.jsx'
import * as assignmentActionsApi from '../../../api/assignmentActionsApi.js'

// AssignmentActionPage is a public, unauthenticated page (outside both
// ProtectedRoute and PublicRoute — see routes/AppRoutes.jsx's comment) that
// reads only a `token` query param and calls one API function, so it needs
// no Redux/QueryClient providers — just a router for useSearchParams.
vi.mock('../../../api/assignmentActionsApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    respondToAssignmentAction: vi.fn(),
  }
})

function renderPage(searchParams = '?token=valid-token-123') {
  return render(
    <MemoryRouter initialEntries={[`/agency/job-proposals/respond${searchParams}`]}>
      <Routes>
        <Route path="/agency/job-proposals/respond" element={<AssignmentActionPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

afterEach(() => {
  vi.clearAllMocks()
  cleanup()
})

describe('AssignmentActionPage — missing token', () => {
  it('shows a missing-token message and never calls the API when no token is present', () => {
    renderPage('')

    expect(
      screen.getByText(/this link is missing its token and cannot be used/i),
    ).toBeInTheDocument()
    expect(assignmentActionsApi.respondToAssignmentAction).not.toHaveBeenCalled()
  })
})

describe('AssignmentActionPage — API integration', () => {
  it('does not call the API until the user clicks Confirm response', () => {
    renderPage()

    expect(screen.getByRole('button', { name: /confirm response/i })).toBeInTheDocument()
    expect(assignmentActionsApi.respondToAssignmentAction).not.toHaveBeenCalled()
  })

  it('calls respondToAssignmentAction with the token and shows an Accepted success state with a trip-created note', async () => {
    const user = userEvent.setup()
    assignmentActionsApi.respondToAssignmentAction.mockResolvedValueOnce({
      status: 'Accepted',
      referenceCode: 'LOAD-1001',
    })

    renderPage('?token=valid-token-123')
    await user.click(screen.getByRole('button', { name: /confirm response/i }))

    await waitFor(() => {
      expect(assignmentActionsApi.respondToAssignmentAction).toHaveBeenCalledWith('valid-token-123')
    })
    expect(await screen.findByText(/accepted/i)).toBeInTheDocument()
    expect(screen.getByText(/for load load-1001/i)).toBeInTheDocument()
    expect(screen.getByText(/a trip has been created for this assignment/i)).toBeInTheDocument()
  })

  it('shows a Declined success state without the trip-created note', async () => {
    const user = userEvent.setup()
    assignmentActionsApi.respondToAssignmentAction.mockResolvedValueOnce({ status: 'Declined' })

    renderPage()
    await user.click(screen.getByRole('button', { name: /confirm response/i }))

    expect(await screen.findByText(/declined/i)).toBeInTheDocument()
    expect(screen.queryByText(/a trip has been created for this assignment/i)).not.toBeInTheDocument()
  })
})

describe('AssignmentActionPage — error state', () => {
  it('shows the backend error message when the token is invalid or expired', async () => {
    const user = userEvent.setup()
    const backendError = new Error('This link has already been used.')
    assignmentActionsApi.respondToAssignmentAction.mockRejectedValueOnce(backendError)

    renderPage()
    await user.click(screen.getByRole('button', { name: /confirm response/i }))

    expect(await screen.findByText('This link has already been used.')).toBeInTheDocument()
  })

  it('falls back to a generic expired-link message when the rejected error has no message', async () => {
    const user = userEvent.setup()
    assignmentActionsApi.respondToAssignmentAction.mockRejectedValueOnce({})

    renderPage()
    await user.click(screen.getByRole('button', { name: /confirm response/i }))

    expect(await screen.findByText(/this link is invalid or has expired/i)).toBeInTheDocument()
  })
})
