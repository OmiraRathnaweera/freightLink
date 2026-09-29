import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClientProvider } from '@tanstack/react-query'
import { toast } from 'sonner'
import CancelLoadDialog from '../../../components/CancelLoadDialog.jsx'
import { createTestQueryClient } from '../../../../../test/testUtils.jsx'
import * as loadsApi from '../../../api/loadsApi.js'

vi.mock('../../../api/loadsApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return { ...actual, useCancelLoadMutation: vi.fn() }
})

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))

function renderDialog(onClose = vi.fn()) {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <CancelLoadDialog loadId="load-1" onClose={onClose} />
    </QueryClientProvider>,
  )
  return { onClose }
}

afterEach(() => {
  vi.mocked(loadsApi.useCancelLoadMutation).mockReset()
  vi.mocked(toast.success).mockReset()
  cleanup()
})

describe('CancelLoadDialog — validation', () => {
  it('requires a cancellation reason before submitting', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn()
    loadsApi.useCancelLoadMutation.mockReturnValue({ mutateAsync, isPending: false })
    renderDialog()

    await user.click(screen.getByRole('button', { name: 'Cancel load' }))

    expect(await screen.findByText('A cancellation reason is required')).toBeInTheDocument()
    expect(mutateAsync).not.toHaveBeenCalled()
  })
})

describe('CancelLoadDialog — API integration', () => {
  it('submits the reason and closes on success', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn().mockResolvedValue({ loadId: 'load-1', status: 'Cancelled' })
    loadsApi.useCancelLoadMutation.mockReturnValue({ mutateAsync, isPending: false })
    const { onClose } = renderDialog()

    await user.type(screen.getByLabelText('Cancellation reason'), 'Shipper found alternate carrier')
    await user.click(screen.getByRole('button', { name: 'Cancel load' }))

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith({ reason: 'Shipper found alternate carrier' }))
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Load cancelled'))
    await waitFor(() => expect(onClose).toHaveBeenCalled())
  })

  it('shows an inline error and does not close when the mutation fails', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn().mockRejectedValue({ code: 'LOAD_NOT_OWNED' })
    loadsApi.useCancelLoadMutation.mockReturnValue({ mutateAsync, isPending: false })
    const { onClose } = renderDialog()

    await user.type(screen.getByLabelText('Cancellation reason'), 'Shipper found alternate carrier')
    await user.click(screen.getByRole('button', { name: 'Cancel load' }))

    expect(await screen.findByText("You don't have access to this load.")).toBeInTheDocument()
    expect(onClose).not.toHaveBeenCalled()
  })
})
