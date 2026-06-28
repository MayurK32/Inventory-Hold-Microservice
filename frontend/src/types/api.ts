export interface HoldItem {
  productId: string
  productName: string
  quantity: number
}

export type HoldStatus = 'Active' | 'Released' | 'Expired'

export interface Hold {
  holdId: string
  customerName: string | null
  status: HoldStatus
  items: HoldItem[]
  createdAt: string
  expiresAt: string
  releasedAt: string | null
  expiredAt: string | null
}

export interface InventoryItem {
  productId: string
  name: string
  totalQuantity: number
  availableQuantity: number
  heldQuantity: number
}

export interface PagedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface CreateHoldRequest {
  customerName: string | null
  items: Array<{ productId: string; quantity: number }>
}

export interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
}

export interface StockFailure {
  productId: string
  requested: number
  available: number
}
