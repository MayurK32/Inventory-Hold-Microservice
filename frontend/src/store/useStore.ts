import { create } from 'zustand'

interface Toast {
  id: number
  message: string
}

interface Store {
  activeOnlyFilter: boolean
  currentPage: number
  toasts: Toast[]
  setActiveOnlyFilter: (v: boolean) => void
  setCurrentPage: (p: number) => void
  addToast: (msg: string) => void
  removeToast: (id: number) => void
}

export const useStore = create<Store>()(set => ({
  activeOnlyFilter: true,
  currentPage: 1,
  toasts: [],
  setActiveOnlyFilter: (v) => set({ activeOnlyFilter: v, currentPage: 1 }),
  setCurrentPage: (p) => set({ currentPage: p }),
  addToast: (msg) => set(s => ({ toasts: [...s.toasts, { id: Date.now(), message: msg }] })),
  removeToast: (id) => set(s => ({ toasts: s.toasts.filter(t => t.id !== id) })),
}))
