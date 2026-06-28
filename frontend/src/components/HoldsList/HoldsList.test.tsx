import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderWithProviders } from '../../test-utils'
import { useStore } from '../../store/useStore'
import HoldsList from './HoldsList'

const mockHold = {
  holdId: 'hold-1',
  customerName: 'Bob',
  status: 'Active' as const,
  items: [{ productId: 'widget-a', productName: 'Widget A', quantity: 2 }],
  createdAt: new Date().toISOString(),
  expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
  releasedAt: null,
  expiredAt: null,
}

const mockGetHolds = vi.hoisted(() => vi.fn())
vi.mock('../../api/holds', () => ({
  getHolds: mockGetHolds,
  releaseHold: vi.fn(),
}))

describe('HoldsList', () => {
  beforeEach(() => {
    useStore.setState({ activeOnlyFilter: true, currentPage: 1 })
    mockGetHolds.mockResolvedValue({
      items: [mockHold],
      total: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    })
  })

  it('renders hold cards', async () => {
    renderWithProviders(<HoldsList />)
    expect(await screen.findByText('Bob')).toBeInTheDocument()
  })

  it('filter toggle switches to show all statuses', async () => {
    renderWithProviders(<HoldsList />)
    await screen.findByText('Bob')
    await userEvent.click(screen.getByRole('checkbox', { name: /active only/i }))
    await waitFor(() =>
      expect(mockGetHolds).toHaveBeenCalledWith(undefined, 1, 20)
    )
  })

  it('next page button increments page', async () => {
    mockGetHolds.mockResolvedValue({
      items: [mockHold],
      total: 25,
      page: 1,
      pageSize: 20,
      totalPages: 2,
    })
    renderWithProviders(<HoldsList />)
    await screen.findByText('Bob')
    await userEvent.click(screen.getByRole('button', { name: /next/i }))
    await waitFor(() =>
      expect(mockGetHolds).toHaveBeenCalledWith('active', 2, 20)
    )
  })
})
