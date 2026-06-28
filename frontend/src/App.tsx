import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import Toast from './components/shared/Toast'
import InventoryDashboard from './components/InventoryDashboard/InventoryDashboard'
import CreateHoldForm from './components/CreateHoldForm/CreateHoldForm'
import HoldsList from './components/HoldsList/HoldsList'
import './App.css'

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 10_000 } },
})

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <Toast />
      <header className="app-header">
        <h1>Inventory Hold Manager</h1>
      </header>
      <main className="app-main">
        <InventoryDashboard />
        <CreateHoldForm />
        <HoldsList />
      </main>
    </QueryClientProvider>
  )
}
