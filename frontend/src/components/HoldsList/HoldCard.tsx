import { useState, useEffect } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { Hold } from '../../types/api'
import { releaseHold } from '../../api/holds'
import ErrorBanner from '../shared/ErrorBanner'
import LoadingSpinner from '../shared/LoadingSpinner'
import styles from './HoldsList.module.css'

const STATUS_COLORS: Record<string, string> = {
  Active: '#16a34a',
  Released: '#6b7280',
  Expired: '#dc2626',
}

function formatCountdown(seconds: number): string {
  if (seconds <= 0) return 'Expired'
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}m ${s.toString().padStart(2, '0')}s`
}

interface Props {
  hold: Hold
}

export default function HoldCard({ hold }: Props) {
  const queryClient = useQueryClient()
  const [confirming, setConfirming] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  const [secondsLeft, setSecondsLeft] = useState(() =>
    Math.max(0, Math.floor((new Date(hold.expiresAt).getTime() - Date.now()) / 1000))
  )

  useEffect(() => {
    if (hold.status !== 'Active') return
    if (secondsLeft <= 0) {
      queryClient.invalidateQueries({ queryKey: ['holds'] })
      return
    }
    const id = setInterval(() => setSecondsLeft(s => s - 1), 1000)
    return () => clearInterval(id)
  }, [secondsLeft, hold.status, queryClient])

  const release = useMutation({
    mutationFn: () => releaseHold(hold.holdId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['holds'] })
      queryClient.invalidateQueries({ queryKey: ['inventory'] })
      setConfirming(false)
    },
    onError: (err: { response?: { status?: number } }) => {
      setConfirming(false)
      if (err.response?.status === 410) {
        setErrorMessage('Hold already released or expired')
        queryClient.invalidateQueries({ queryKey: ['holds'] })
      } else {
        setErrorMessage('Failed to release hold')
      }
    },
  })

  return (
    <div className={styles.card}>
      <div className={styles.cardHeader}>
        <div>
          <span className={styles.customerId}>
            {hold.customerName ?? <em>No customer</em>}
          </span>
          <span className={styles.holdId}>#{hold.holdId.slice(0, 8)}</span>
        </div>
        <span
          className={styles.badge}
          style={{ background: STATUS_COLORS[hold.status] }}
        >
          {hold.status}
        </span>
      </div>

      <ul className={styles.items}>
        {hold.items.map(item => (
          <li key={item.productId}>
            {item.productName} × {item.quantity}
          </li>
        ))}
      </ul>

      {hold.status === 'Active' && (
        <p className={styles.countdown}>
          Expires in: <strong>{formatCountdown(secondsLeft)}</strong>
        </p>
      )}

      <ErrorBanner message={errorMessage} />

      {hold.status === 'Active' && (
        <div className={styles.actions}>
          {confirming ? (
            <>
              <button
                className={styles.confirmBtn}
                onClick={() => release.mutate()}
                disabled={release.isPending}
              >
                {release.isPending && <LoadingSpinner />}
                Confirm
              </button>
              <button
                className={styles.cancelBtn}
                onClick={() => setConfirming(false)}
                disabled={release.isPending}
              >
                Cancel
              </button>
            </>
          ) : (
            <button
              className={styles.releaseBtn}
              onClick={() => setConfirming(true)}
            >
              Release
            </button>
          )}
        </div>
      )}
    </div>
  )
}
