import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderWithProviders } from '../../test-utils'
import CreateHoldForm from './CreateHoldForm'

vi.mock('../../api/inventory', () => ({
  getInventory: vi.fn().mockResolvedValue([
    { productId: 'widget-a', name: 'Widget A', totalQuantity: 100, availableQuantity: 90, heldQuantity: 10 },
  ]),
}))

const mockCreateHold = vi.hoisted(() => vi.fn())
vi.mock('../../api/holds', () => ({
  createHold: mockCreateHold,
}))

describe('CreateHoldForm', () => {
  beforeEach(() => mockCreateHold.mockReset())

  it('submits correct payload', async () => {
    mockCreateHold.mockResolvedValueOnce({ holdId: 'h1', status: 'Active' })
    renderWithProviders(<CreateHoldForm />)

    await screen.findByText(/Widget A/)

    await userEvent.type(screen.getByLabelText('Customer Name'), 'Alice')
    await userEvent.selectOptions(screen.getAllByRole('combobox')[0], 'widget-a')
    const qtyInput = screen.getByLabelText('Quantity')
    await userEvent.clear(qtyInput)
    await userEvent.type(qtyInput, '3')
    await userEvent.click(screen.getByRole('button', { name: /create hold/i }))

    await waitFor(() =>
      expect(mockCreateHold).toHaveBeenCalledWith({
        customerName: 'Alice',
        items: [{ productId: 'widget-a', quantity: 3 }],
      })
    )
  })

  it('shows ErrorBanner on 409', async () => {
    mockCreateHold.mockRejectedValueOnce({
      response: { status: 409, data: { detail: 'Insufficient stock for Widget A' } },
    })
    renderWithProviders(<CreateHoldForm />)

    await screen.findByText(/Widget A/)
    await userEvent.selectOptions(screen.getAllByRole('combobox')[0], 'widget-a')
    await userEvent.click(screen.getByRole('button', { name: /create hold/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Insufficient stock for Widget A')
  })
})
