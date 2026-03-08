import { useEffect, useState, useCallback } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import { ClaimStatus, ClaimStatusLabel, type Claim, type PagedResult, type UserRole } from '../types'
import { ClaimStatusBadge } from '../components/ClaimStatusBadge'
import { CurrencyAmount } from '../components/CurrencyAmount'
import { ErrorAlert } from '../components/ErrorAlert'

interface ClaimsListProps {
  role: UserRole
  creditorId: string
  debtorKennitala: string
}

export function ClaimsList({ role, creditorId, debtorKennitala }: ClaimsListProps) {
  const [claims, setClaims] = useState<Claim[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [continuationToken, setContinuationToken] = useState<string | null>(null)
  const [hasMore, setHasMore] = useState(false)
  const [statusFilter, setStatusFilter] = useState<ClaimStatus | undefined>(undefined)
  const [kennitalaInput, setKennitalaInput] = useState(debtorKennitala)

  const fetchClaims = useCallback(async (append = false, token?: string | null) => {
    setLoading(true)
    setError(null)

    try {
      let result: PagedResult<Claim>
      if (role === 'Creditor') {
        result = await api.getClaimsByCreditor(creditorId, statusFilter, 20, token ?? undefined)
      } else {
        result = await api.getClaimsByDebtor(kennitalaInput, 20, token ?? undefined)
      }
      setClaims(append ? (prev) => [...prev, ...result.Items] : result.Items)
      setContinuationToken(result.ContinuationToken)
      setHasMore(result.HasMore)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load claims')
    } finally {
      setLoading(false)
    }
  }, [role, creditorId, kennitalaInput, statusFilter])

  useEffect(() => {
    if (role === 'Creditor' || kennitalaInput.length === 10) {
      fetchClaims()
    }
  }, [fetchClaims, role, kennitalaInput.length])

  return (
    <div>
      <h1 className="text-2xl font-bold mb-6">
        {role === 'Creditor' ? 'Claims' : 'My Claims'}
      </h1>

      {error && <ErrorAlert message={error} onDismiss={() => setError(null)} />}

      <div className="flex flex-wrap gap-3 mb-4">
        {role === 'Debtor' && (
          <div>
            <label className="block text-xs text-gray-500 mb-1">Kennitala</label>
            <input
              type="text"
              value={kennitalaInput}
              onChange={(e) => setKennitalaInput(e.target.value)}
              placeholder="1234567890"
              maxLength={10}
              className="rounded-md border-gray-300 text-sm py-1.5 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>
        )}
        {role === 'Creditor' && (
          <div>
            <label className="block text-xs text-gray-500 mb-1">Status Filter</label>
            <select
              value={statusFilter ?? ''}
              onChange={(e) => setStatusFilter(e.target.value ? Number(e.target.value) as ClaimStatus : undefined)}
              className="rounded-md border-gray-300 text-sm py-1.5 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="">All Statuses</option>
              {Object.entries(ClaimStatusLabel).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </div>
        )}
      </div>

      {loading && claims.length === 0 ? (
        <p className="text-gray-500">Loading...</p>
      ) : claims.length === 0 ? (
        <p className="text-gray-500">No claims found.</p>
      ) : (
        <>
          <div className="bg-white rounded-lg border border-gray-200 overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Reference</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    {role === 'Creditor' ? 'Debtor' : 'Creditor'}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Amount</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Paid</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Due Date</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Category</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {claims.map((claim) => (
                  <tr key={claim.Id} className="hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <Link to={`/claims/${claim.Id}`} className="text-indigo-600 hover:text-indigo-800 font-medium text-sm">
                        {claim.ClaimReference}
                      </Link>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">
                      {role === 'Creditor' ? claim.DebtorKennitala : claim.CreditorId}
                    </td>
                    <td className="px-4 py-3 text-sm">
                      <CurrencyAmount amount={claim.Amount} currencyCode={claim.CurrencyCode} />
                    </td>
                    <td className="px-4 py-3 text-sm">
                      <CurrencyAmount amount={claim.PaidAmount} currencyCode={claim.CurrencyCode} />
                    </td>
                    <td className="px-4 py-3">
                      <ClaimStatusBadge status={claim.Status} />
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">
                      {new Date(claim.DueDate).toLocaleDateString('is-IS')}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">{claim.CategoryCode}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {hasMore && (
            <div className="mt-4 text-center">
              <button
                onClick={() => fetchClaims(true, continuationToken)}
                disabled={loading}
                className="px-4 py-2 text-sm font-medium text-indigo-600 bg-indigo-50 rounded-md hover:bg-indigo-100 disabled:opacity-50"
              >
                {loading ? 'Loading...' : 'Load More'}
              </button>
            </div>
          )}
        </>
      )}
    </div>
  )
}
