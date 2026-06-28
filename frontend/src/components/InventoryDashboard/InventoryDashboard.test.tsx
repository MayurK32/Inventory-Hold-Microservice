import { screen } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import { renderWithProviders } from '../../test-utils'
import InventoryDashboard from './InventoryDashboard'

vi.mock('../../api/inventory', () => ({
  getInventory: vi.fn().mockResolvedValue([
    { productId: 'widget-a', name: 'Widget A', totalQuantity: 100, availableQuantity: 90, heldQuantity: 10 },
    { productId: 'widget-b', name: 'Widget B', totalQuantity: 50, availableQuantity: 50, heldQuantity: 0 },
  ]),
  resetInventory: vi.fn(),
}))

describe('InventoryDashboard', () => {
  it('renders all products', async () => {
    renderWithProviders(<InventoryDashboard />)
    expect(await screen.findByText('Widget A')).toBeInTheDocument()
    expect(await screen.findByText('Widget B')).toBeInTheDocument()
  })

  it('highlights row when heldQuantity > 0', async () => {
    renderWithProviders(<InventoryDashboard />)
    await screen.findByText('Widget A')
    expect(document.querySelector('[data-testid="row-widget-a"]')?.className).toMatch(/held/)
    expect(document.querySelector('[data-testid="row-widget-b"]')?.className).not.toMatch(/held/)
  })
})
