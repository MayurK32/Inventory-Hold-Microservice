import { api } from './client'
import type { Hold, PagedResponse, CreateHoldRequest } from '../types/api'

export const getHolds = async (
  status: string | undefined,
  page: number,
  pageSize = 20
): Promise<PagedResponse<Hold>> => {
  const params: Record<string, string | number> = { page, pageSize }
  if (status) params.status = status
  const res = await api.get<PagedResponse<Hold>>('/holds', { params })
  return res.data
}

export const getHold = async (id: string): Promise<Hold> => {
  const res = await api.get<Hold>(`/holds/${id}`)
  return res.data
}

export const createHold = async (req: CreateHoldRequest): Promise<Hold> => {
  const res = await api.post<Hold>('/holds', req)
  return res.data
}

export const releaseHold = async (id: string): Promise<Hold> => {
  const res = await api.delete<Hold>(`/holds/${id}`)
  return res.data
}
