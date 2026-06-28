interface Props {
  message: string | null
}

export default function ErrorBanner({ message }: Props) {
  if (!message) return null
  return (
    <div
      role="alert"
      style={{
        background: '#fee2e2',
        border: '1px solid #fca5a5',
        borderRadius: 6,
        color: '#991b1b',
        padding: '10px 14px',
        margin: '8px 0',
        fontSize: 14,
      }}
    >
      {message}
    </div>
  )
}
