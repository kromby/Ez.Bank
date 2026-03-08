import { useEffect, useState, useRef } from 'react'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { authenticate, setToken, api } from './api/client'
import type { UserRole } from './types'
import { Layout } from './components/Layout'
import { ErrorBoundary } from './components/ErrorBoundary'
import { Dashboard } from './pages/Dashboard'
import { CreateClaim } from './pages/CreateClaim'
import { ClaimsList } from './pages/ClaimsList'
import { ClaimDetail } from './pages/ClaimDetail'

const BANK_ID = 'bank-1'
const USER_ID = 'user-1'
const DEBTOR_KENNITALA = '1234567890'

export default function App() {
  const [role, setRole] = useState<UserRole>('Creditor')
  const [ready, setReady] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [creditorId, setCreditorId] = useState<string>('')
  const creditorIdRef = useRef('')

  useEffect(() => {
    let cancelled = false
    setReady(false)
    setError(null)

    async function setup() {
      await authenticate(BANK_ID, USER_ID, role)

      if (role === 'Creditor' && !creditorIdRef.current) {
        const creditor = await api.createCreditor({
          kennitala: '5555555555',
          name: 'Demo Creditor',
          bankId: BANK_ID,
          accountNumber: 'IS0001260076450130346972',
        })
        creditorIdRef.current = creditor.Id
        if (!cancelled) setCreditorId(creditor.Id)
      }

      if (!cancelled) setReady(true)
    }

    setup().catch((err) => {
      if (!cancelled) setError(err instanceof Error ? err.message : 'Startup failed')
    })

    return () => { cancelled = true }
  }, [role])

  const handleRoleChange = (newRole: UserRole) => {
    setToken(null)
    setRole(newRole)
  }

  if (error) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="bg-red-50 border border-red-200 rounded-lg p-6 max-w-md">
          <h2 className="text-lg font-semibold text-red-800 mb-2">Startup Error</h2>
          <p className="text-sm text-red-700">{error}</p>
          <p className="text-xs text-red-500 mt-2">Make sure the API is running at localhost:7071</p>
        </div>
      </div>
    )
  }

  if (!ready) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <p className="text-gray-500">Setting up demo as {role}...</p>
      </div>
    )
  }

  return (
    <BrowserRouter>
      <ErrorBoundary>
        <Layout role={role} onRoleChange={handleRoleChange}>
          <Routes>
            <Route path="/" element={<Dashboard role={role} creditorId={creditorId} debtorKennitala={DEBTOR_KENNITALA} />} />
            <Route path="/claims" element={<ClaimsList role={role} creditorId={creditorId} debtorKennitala={DEBTOR_KENNITALA} />} />
            <Route path="/claims/new" element={<CreateClaim creditorId={creditorId} />} />
            <Route path="/claims/:id" element={<ClaimDetail role={role} bankId={BANK_ID} />} />
          </Routes>
        </Layout>
      </ErrorBoundary>
    </BrowserRouter>
  )
}
