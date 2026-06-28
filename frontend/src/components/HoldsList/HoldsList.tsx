import { useQuery } from '@tanstack/react-query'
import { useStore } from '../../store/useStore'
import { getHolds } from '../../api/holds'
import HoldCard from './HoldCard'
import LoadingSkeleton from '../shared/LoadingSkeleton'
import styles from './HoldsList.module.css'

const PAGE_SIZE = 20

export default function HoldsList() {
  const { activeOnlyFilter, currentPage, setActiveOnlyFilter, setCurrentPage } = useStore()

  const status = activeOnlyFilter ? 'active' : undefined

  const { data, isLoading } = useQuery({
    queryKey: ['holds', { status, page: currentPage }],
    queryFn: () => getHolds(status, currentPage, PAGE_SIZE),
  })

  return (
    <section className={styles.section}>
      <div className={styles.header}>
        <h2>Holds</h2>
        <label className={styles.filterLabel}>
          <input
            type="checkbox"
            checked={activeOnlyFilter}
            onChange={e => setActiveOnlyFilter(e.target.checked)}
            aria-label="Active only"
          />
          Active only
        </label>
      </div>

      {isLoading ? (
        <LoadingSkeleton rows={3} />
      ) : data?.items.length === 0 ? (
        <p className={styles.empty}>No holds found.</p>
      ) : (
        <div className={styles.grid}>
          {data?.items.map(hold => (
            <HoldCard key={hold.holdId} hold={hold} />
          ))}
        </div>
      )}

      {data && data.totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            onClick={() => setCurrentPage(currentPage - 1)}
            disabled={currentPage <= 1}
          >
            ← Prev
          </button>
          <span>Page {data.page} of {data.totalPages}</span>
          <button
            aria-label="Next"
            onClick={() => setCurrentPage(currentPage + 1)}
            disabled={currentPage >= data.totalPages}
          >
            Next →
          </button>
        </div>
      )}
    </section>
  )
}
