import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import { ClaimStatus, type Claim, type PagedResult, type UserRole } from '../types'
import { ClaimStatusBadge } from '../components/ClaimStatusBadge'
import { CurrencyAmount } from '../components/CurrencyAmount'
import { ErrorAlert } from '../components/ErrorAlert'

interface DashboardProps {
  role: UserRole
  creditorId: string
  debtorKennitala: string
}

export function Dashboard({ role, creditorId, debtorKennitala }: DashboardProps) {
  const [claims, setClaims] = useState<Claim[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setLoading(true)
    setError(null)

    const fetchClaims = role === 'Creditor'
      ? api.getClaimsByCreditor(creditorId)
      : api.getClaimsByDebtor(debtorKennitala)

    fetchClaims
      .then((result: PagedResult<Claim>) => setClaims(result.Items))
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false))
  }, [role, creditorId, debtorKennitala])

  const totalClaims = claims.length
  const totalAmount = claims.reduce((sum, c) => sum + c.Amount, 0)
  const totalPaid = claims.reduce((sum, c) => sum + c.PaidAmount, 0)
  const overdueClaims = claims.filter((c) => c.Status === ClaimStatus.Overdue).length
  const disputedClaims = claims.filter((c) => c.Status === ClaimStatus.Disputed).length
  const paidClaims = claims.filter((c) => c.Status === ClaimStatus.Paid).length

  const recentClaims = [...claims]
    .sort((a, b) => new Date(b.Inserted).getTime() - new Date(a.Inserted).getTime())
    .slice(0, 5)

  return (
    <div>
      <h1 className="text-2xl font-bold mb-6">Dashboard</h1>

      {error && <ErrorAlert message={error} onDismiss={() => setError(null)} />}

      {loading ? (
        <p className="text-gray-500">Loading...</p>
      ) : (
        <>
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 mb-8">
            <StatCard label="Total claims" value={totalClaims.toString()} />
            <StatCard label="Total amount" value={`${totalAmount.toLocaleString('is-IS')} ISK`} />
            <StatCard label="Total paid" value={`${totalPaid.toLocaleString('is-IS')} ISK`} />
            <StatCard label="Paid" value={paidClaims.toString()} color="text-green-600" />
            <StatCard label="Overdue" value={overdueClaims.toString()} color="text-red-600" />
            <StatCard label="Disputed" value={disputedClaims.toString()} color="text-orange-600" />
          </div>

          <h2 className="text-lg font-semibold mb-3">Recent Claims</h2>
          {recentClaims.length === 0 ? (
            <p className="text-gray-500">No claims found.</p>
          ) : (
            <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
              <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Reference</th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Amount</th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Due Date</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {recentClaims.map((claim) => (
                    <tr key={claim.Id} className="hover:bg-gray-50">
                      <td className="px-4 py-3">
                        <Link to={`/claims/${claim.Id}`} className="text-indigo-600 hover:text-indigo-800 font-medium">
                          {claim.ClaimReference}
                        </Link>
                      </td>
                      <td className="px-4 py-3 text-sm">
                        <CurrencyAmount amount={claim.Amount} currencyCode={claim.CurrencyCode} />
                      </td>
                      <td className="px-4 py-3">
                        <ClaimStatusBadge status={claim.Status} />
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500">
                        {new Date(claim.DueDate).toLocaleDateString('is-IS')}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  )
}

function StatCard({ label, value, color }: { label: string; value: string; color?: string }) {
  return (
    <div className="bg-white rounded-lg border border-gray-200 p-4">
      <p className="text-sm text-gray-500">{label}</p>
      <p className={`text-xl font-semibold mt-1 ${color ?? 'text-gray-900'}`}>{value}</p>
    </div>
  )
}
