export default function LoadingSpinner() {
  return (
    <>
      <span
        style={{
          display: 'inline-block',
          width: 12,
          height: 12,
          border: '2px solid rgba(255,255,255,0.4)',
          borderTopColor: '#fff',
          borderRadius: '50%',
          animation: 'spin 0.6s linear infinite',
          marginRight: 6,
          verticalAlign: 'middle',
        }}
      />
      <style>{`@keyframes spin { to { transform: rotate(360deg) } }`}</style>
    </>
  )
}
