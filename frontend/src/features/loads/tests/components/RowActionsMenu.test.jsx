import { describe, expect, it } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import RowActionsMenu from '../../components/RowActionsMenu.jsx'
import { LoadStatus, UserRole } from '../../../../lib/enums.js'
import { createTestQueryClient } from '../../../../test/testUtils.jsx'

// RowActionsMenu drives its own open/close state and needs a router (View/
// Edit are <Link>s); Cancel/Publish mount CancelLoadDialog/PublishLoadDialog
// after a click, which call useMutation, so every render also needs a
// QueryClientProvider — no Redux read happens in this subtree, so that's
// the only other provider required.
function renderMenu(loadId, status, role) {
  return render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter>
        <RowActionsMenu loadId={loadId} status={status} role={role} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

async function openMenu(loadId, status, role) {
  const user = userEvent.setup()
  renderMenu(loadId, status, role)
  await user.click(screen.getByRole('button', { name: `Actions for ${loadId}` }))
  return within(screen.getByRole('menu'))
}

describe('RowActionsMenu — per-status action visibility (Shipper)', () => {
  it('shows Edit and Cancel for a Draft load', async () => {
    const menu = await openMenu('load-1', LoadStatus.DRAFT, UserRole.SHIPPER)
    expect(menu.getByRole('menuitem', { name: /view/i })).toBeInTheDocument()
    expect(menu.getByRole('menuitem', { name: /edit/i })).toBeInTheDocument()
    expect(menu.getByRole('menuitem', { name: /cancel/i })).toBeInTheDocument()
    // Draft is also publishable.
    expect(menu.getByRole('menuitem', { name: /publish/i })).toBeInTheDocument()
  })

  it('shows Edit and Cancel (but not Publish) for a Posted load', async () => {
    const menu = await openMenu('load-2', LoadStatus.POSTED, UserRole.SHIPPER)
    expect(menu.getByRole('menuitem', { name: /edit/i })).toBeInTheDocument()
    expect(menu.getByRole('menuitem', { name: /cancel/i })).toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /publish/i })).not.toBeInTheDocument()
  })

  it('shows Cancel but not Edit for a Matched load', async () => {
    const menu = await openMenu('load-3', LoadStatus.MATCHED, UserRole.SHIPPER)
    expect(menu.getByRole('menuitem', { name: /cancel/i })).toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /edit/i })).not.toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /publish/i })).not.toBeInTheDocument()
  })

  it('hides Edit, Cancel and Publish for a Delivered load (only View remains)', async () => {
    const menu = await openMenu('load-4', LoadStatus.DELIVERED, UserRole.SHIPPER)
    expect(menu.getByRole('menuitem', { name: /view/i })).toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /edit/i })).not.toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /cancel/i })).not.toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /publish/i })).not.toBeInTheDocument()
  })

  it('hides Edit, Cancel and Publish for a Cancelled load', async () => {
    const menu = await openMenu('load-5', LoadStatus.CANCELLED, UserRole.SHIPPER)
    expect(menu.queryByRole('menuitem', { name: /edit/i })).not.toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /cancel/i })).not.toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /publish/i })).not.toBeInTheDocument()
  })
})

describe('RowActionsMenu — non-Shipper roles never see mutating actions', () => {
  it('hides Edit/Cancel/Publish for an Admin viewing a Draft load', async () => {
    const menu = await openMenu('load-6', LoadStatus.DRAFT, UserRole.ADMIN)
    expect(menu.getByRole('menuitem', { name: /view/i })).toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /edit/i })).not.toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /cancel/i })).not.toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /publish/i })).not.toBeInTheDocument()
  })
})

describe('RowActionsMenu — opening a mutating item mounts its dialog', () => {
  it('opens CancelLoadDialog when Cancel is clicked', async () => {
    const user = userEvent.setup()
    renderMenu('load-7', LoadStatus.POSTED, UserRole.SHIPPER)
    await user.click(screen.getByRole('button', { name: 'Actions for load-7' }))
    await user.click(screen.getByRole('menuitem', { name: /cancel/i }))
    expect(screen.getByText('Cancel this load?')).toBeInTheDocument()
  })

  it('opens PublishLoadDialog when Publish is clicked', async () => {
    const user = userEvent.setup()
    renderMenu('load-8', LoadStatus.DRAFT, UserRole.SHIPPER)
    await user.click(screen.getByRole('button', { name: 'Actions for load-8' }))
    await user.click(screen.getByRole('menuitem', { name: /publish/i }))
    expect(screen.getByText('Publish this load?')).toBeInTheDocument()
  })
})
