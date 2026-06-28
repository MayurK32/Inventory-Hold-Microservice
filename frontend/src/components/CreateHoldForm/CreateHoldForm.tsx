import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getInventory } from '../../api/inventory'
import { createHold } from '../../api/holds'
import type { CreateHoldRequest } from '../../types/api'
import ErrorBanner from '../shared/ErrorBanner'
import LoadingSpinner from '../shared/LoadingSpinner'
import styles from './CreateHoldForm.module.css'

interface HoldItem {
  productId: string
  quantity: number
}

export default function CreateHoldForm() {
  const queryClient = useQueryClient()
  const [customerName, setCustomerName] = useState('')
  const [items, setItems] = useState<HoldItem[]>([{ productId: '', quantity: 1 }])
  const [error, setError] = useState<string | null>(null)

  const { data: inventory } = useQuery({
    queryKey: ['inventory'],
    queryFn: getInventory,
  })

  const mutation = useMutation({
    mutationFn: (req: CreateHoldRequest) => createHold(req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['holds'] })
      queryClient.invalidateQueries({ queryKey: ['inventory'] })
      setCustomerName('')
      setItems([{ productId: '', quantity: 1 }])
      setError(null)
    },
    onError: (err: { response?: { data?: { detail?: string } } }) => {
      setError(err.response?.data?.detail ?? 'Failed to create hold')
    },
  })

  const selectedIds = items.map(i => i.productId).filter(Boolean)

  const addItem = () => setItems(prev => [...prev, { productId: '', quantity: 1 }])

  const removeItem = (idx: number) =>
    setItems(prev => prev.filter((_, i) => i !== idx))

  const updateItem = (idx: number, field: keyof HoldItem, value: string | number) =>
    setItems(prev => prev.map((item, i) => i === idx ? { ...item, [field]: value } : item))

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    mutation.mutate({
      customerName: customerName.trim() || null,
      items: items
        .filter(i => i.productId)
        .map(i => ({ productId: i.productId, quantity: Number(i.quantity) })),
    })
  }

  return (
    <section className={styles.section}>
      <h2 className={styles.title}>Create Hold</h2>
      <form onSubmit={handleSubmit} className={styles.form}>
        <div className={styles.field}>
          <label htmlFor="customerName">Customer Name</label>
          <input
            id="customerName"
            type="text"
            value={customerName}
            onChange={e => setCustomerName(e.target.value)}
            placeholder="Optional"
          />
        </div>

        <div className={styles.itemsHeader}>
          <span>Items</span>
          <button type="button" className={styles.addBtn} onClick={addItem}>
            + Add Item
          </button>
        </div>

        {items.map((item, idx) => (
          <div key={idx} className={styles.itemRow}>
            <select
              aria-label={`Product ${idx + 1}`}
              value={item.productId}
              onChange={e => updateItem(idx, 'productId', e.target.value)}
            >
              <option value="">Select product…</option>
              {inventory
                ?.filter(p => !selectedIds.includes(p.productId) || p.productId === item.productId)
                .map(p => (
                  <option key={p.productId} value={p.productId}>
                    {p.name} (avail: {p.availableQuantity})
                  </option>
                ))}
            </select>
            <label htmlFor={`qty-${idx}`} className={styles.srOnly}>Quantity</label>
            <input
              id={`qty-${idx}`}
              type="number"
              min={1}
              value={item.quantity}
              onChange={e => updateItem(idx, 'quantity', e.target.value)}
              className={styles.qtyInput}
              aria-label="Quantity"
            />
            {items.length > 1 && (
              <button
                type="button"
                className={styles.removeBtn}
                onClick={() => removeItem(idx)}
              >
                ×
              </button>
            )}
          </div>
        ))}

        <ErrorBanner message={error} />

        <button
          type="submit"
          className={styles.submitBtn}
          disabled={mutation.isPending}
        >
          {mutation.isPending && <LoadingSpinner />}
          Create Hold
        </button>
      </form>
    </section>
  )
}
