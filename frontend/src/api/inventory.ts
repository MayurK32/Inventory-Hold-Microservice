import { api } from './client'
import type { InventoryItem } from '../types/api'

export const getInventory = async (): Promise<InventoryItem[]> => {
  const res = await api.get<InventoryItem[]>('/inventory')
  return res.data
}

export const resetInventory = async (): Promise<InventoryItem[]> => {
  const res = await api.post<InventoryItem[]>('/inventory/reset')
  return res.data
}
