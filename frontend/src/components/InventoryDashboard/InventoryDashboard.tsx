import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getInventory, resetInventory } from '../../api/inventory'
import LoadingSkeleton from '../shared/LoadingSkeleton'
import LoadingSpinner from '../shared/LoadingSpinner'
import styles from './InventoryDashboard.module.css'

export default function InventoryDashboard() {
  const queryClient = useQueryClient()
  const { data: items, isLoading } = useQuery({
    queryKey: ['inventory'],
    queryFn: getInventory,
  })

  const reset = useMutation({
    mutationFn: resetInventory,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inventory'] })
      queryClient.invalidateQueries({ queryKey: ['holds'] })
    },
  })

  return (
    <section className={styles.section}>
      <div className={styles.header}>
        <h2>Inventory</h2>
        <button
          className={styles.resetBtn}
          onClick={() => reset.mutate()}
          disabled={reset.isPending}
        >
          {reset.isPending && <LoadingSpinner />}
          Reset Inventory
        </button>
      </div>

      {isLoading ? (
        <LoadingSkeleton rows={5} />
      ) : (
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Product</th>
              <th>Total</th>
              <th>Available</th>
              <th>Held</th>
            </tr>
          </thead>
          <tbody>
            {items?.map(item => (
              <tr
                key={item.productId}
                data-testid={`row-${item.productId}`}
                className={item.heldQuantity > 0 ? styles.held : undefined}
              >
                <td>{item.name}</td>
                <td>{item.totalQuantity}</td>
                <td>{item.availableQuantity}</td>
                <td>{item.heldQuantity}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
