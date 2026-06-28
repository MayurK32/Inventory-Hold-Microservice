import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { renderWithProviders } from '../../test-utils'
import HoldCard from './HoldCard'

const activeHold = {
  holdId: 'hold-1',
  customerName: 'Alice',
  status: 'Active' as const,
  items: [{ productId: 'widget-a', productName: 'Widget A', quantity: 2 }],
  createdAt: new Date().toISOString(),
  expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
  releasedAt: null,
  expiredAt: null,
}

const releasedHold = {
  ...activeHold,
  status: 'Released' as const,
  releasedAt: new Date().toISOString(),
}

const mockReleaseHold = vi.hoisted(() => vi.fn())
vi.mock('../../api/holds', () => ({
  releaseHold: mockReleaseHold,
}))

describe('HoldCard', () => {
  it('shows countdown for active hold', () => {
    renderWithProviders(<HoldCard hold={activeHold} />)
    expect(screen.getByText(/expires in/i)).toBeInTheDocument()
  })

  it('Release button only shown for Active holds', () => {
    const { rerender } = renderWithProviders(<HoldCard hold={activeHold} />)
    expect(screen.getByRole('button', { name: /^release$/i })).toBeInTheDocument()

    rerender(<HoldCard hold={releasedHold} />)
    expect(screen.queryByRole('button', { name: /^release$/i })).not.toBeInTheDocument()
  })

  it('Release button shows inline confirm', async () => {
    renderWithProviders(<HoldCard hold={activeHold} />)
    await userEvent.click(screen.getByRole('button', { name: /^release$/i }))
    expect(screen.getByRole('button', { name: /confirm/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument()
  })

  it('confirm calls releaseHold with the hold ID', async () => {
    mockReleaseHold.mockResolvedValueOnce({ ...activeHold, status: 'Released' })
    renderWithProviders(<HoldCard hold={activeHold} />)
    await userEvent.click(screen.getByRole('button', { name: /^release$/i }))
    await userEvent.click(screen.getByRole('button', { name: /confirm/i }))
    expect(mockReleaseHold).toHaveBeenCalledWith('hold-1')
  })
})
