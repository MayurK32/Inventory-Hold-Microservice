import { useEffect } from 'react'
import { useStore } from '../../store/useStore'

function ToastItem({ id, message }: { id: number; message: string }) {
  const removeToast = useStore(s => s.removeToast)

  useEffect(() => {
    const timer = setTimeout(() => removeToast(id), 4000)
    return () => clearTimeout(timer)
  }, [id, removeToast])

  return (
    <div
      style={{
        background: '#1f2937',
        color: '#fff',
        padding: '10px 16px',
        borderRadius: 6,
        marginBottom: 8,
        display: 'flex',
        alignItems: 'center',
        gap: 12,
        minWidth: 260,
        boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
      }}
    >
      <span style={{ flex: 1, fontSize: 14 }}>{message}</span>
      <button
        onClick={() => removeToast(id)}
        style={{ background: 'none', border: 'none', color: '#9ca3af', cursor: 'pointer', fontSize: 16 }}
      >
        ×
      </button>
    </div>
  )
}

export default function Toast() {
  const toasts = useStore(s => s.toasts)

  return (
    <div
      style={{
        position: 'fixed',
        bottom: 24,
        right: 24,
        zIndex: 1000,
      }}
    >
      {toasts.map(t => (
        <ToastItem key={t.id} id={t.id} message={t.message} />
      ))}
    </div>
  )
}
