import { useEffect, useState, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import {
  ClaimStatus,
  ClaimStatusName,
  type Claim,
  type Payment,
  type ClaimStatusHistoryEntry,
  type ClaimDocument,
  type UserRole,
} from '../types'
import { ClaimStatusBadge } from '../components/ClaimStatusBadge'
import { CurrencyAmount, formatAmount } from '../components/CurrencyAmount'
import { ErrorAlert } from '../components/ErrorAlert'

interface ClaimDetailProps {
  role: UserRole
  bankId: string
}

export function ClaimDetail({ role, bankId }: ClaimDetailProps) {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [claim, setClaim] = useState<Claim | null>(null)
  const [history, setHistory] = useState<ClaimStatusHistoryEntry[]>([])
  const [payments, setPayments] = useState<Payment[]>([])
  const [documents, setDocuments] = useState<ClaimDocument[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // Modals
  const [showPayModal, setShowPayModal] = useState(false)
  const [showCancelModal, setShowCancelModal] = useState(false)
  const [showDisputeModal, setShowDisputeModal] = useState(false)
  const [showEditModal, setShowEditModal] = useState(false)

  const loadClaim = useCallback(async () => {
    if (!id) return
    setLoading(true)
    setError(null)
    try {
      const c = await api.getClaim(id)
      setClaim(c)

      const [h, p, d] = await Promise.allSettled([
        api.getClaimHistory(id),
        api.getPayments(id),
        api.getDocuments(id),
      ])
      setHistory(h.status === 'fulfilled' ? h.value : [])
      setPayments(p.status === 'fulfilled' ? p.value : [])
      setDocuments(d.status === 'fulfilled' && Array.isArray(d.value) ? d.value : [])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load claim')
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => { loadClaim() }, [loadClaim])

  if (loading) return <p className="text-gray-500">Loading...</p>
  if (error) return <ErrorAlert message={error} />
  if (!claim) return <ErrorAlert message="Claim not found" />

  const canPay = role === 'Debtor' && [ClaimStatus.Sent, ClaimStatus.Viewed, ClaimStatus.PartiallyPaid, ClaimStatus.Overdue].includes(claim.Status)
  const canCancel = role === 'Creditor' && [ClaimStatus.Created, ClaimStatus.Sent, ClaimStatus.Viewed, ClaimStatus.PartiallyPaid, ClaimStatus.Overdue, ClaimStatus.Disputed].includes(claim.Status)
  const canDispute = role === 'Debtor' && [ClaimStatus.Viewed, ClaimStatus.PartiallyPaid, ClaimStatus.Overdue].includes(claim.Status)
  const canEdit = role === 'Creditor' && [ClaimStatus.Created, ClaimStatus.Sent].includes(claim.Status)
  const canSend = role === 'Creditor' && claim.Status === ClaimStatus.Created
  const canCalcDueCosts = [ClaimStatus.Overdue, ClaimStatus.Sent, ClaimStatus.Viewed, ClaimStatus.PartiallyPaid].includes(claim.Status)
  const canMarkOverdue = role === 'Creditor' && [ClaimStatus.Sent, ClaimStatus.Viewed, ClaimStatus.PartiallyPaid].includes(claim.Status)
  const canUploadDoc = claim.Status !== ClaimStatus.Cancelled

  const handleSend = async () => {
    setError(null)
    try {
      const updated = await api.updateClaimStatus(claim.Id, ClaimStatusName[ClaimStatus.Sent])
      setClaim(updated)
      const h = await api.getClaimHistory(claim.Id)
      setHistory(h)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update status')
    }
  }

  const handleMarkOverdue = async () => {
    setError(null)
    try {
      const updated = await api.updateClaimStatus(claim.Id, ClaimStatusName[ClaimStatus.Overdue])
      setClaim(updated)
      const h = await api.getClaimHistory(claim.Id)
      setHistory(h)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to mark as overdue')
    }
  }

  const handleCalculateDueCosts = async () => {
    setError(null)
    try {
      await api.calculateDueCosts(claim.Id)
      await loadClaim()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to calculate due costs')
    }
  }

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setError(null)
    try {
      await api.uploadDocument(claim.Id, file)
      const d = await api.getDocuments(claim.Id)
      setDocuments(d)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to upload document')
    }
    e.target.value = ''
  }

  return (
    <div>
      <button onClick={() => navigate(-1)} className="text-sm text-indigo-600 hover:text-indigo-800 mb-4 inline-block">
        &larr; Back
      </button>

      {error && <ErrorAlert message={error} onDismiss={() => setError(null)} />}

      {/* Header */}
      <div className="flex flex-wrap items-start justify-between gap-4 mb-6">
        <div>
          <h1 className="text-2xl font-bold">{claim.ClaimReference}</h1>
          <p className="text-gray-500 text-sm mt-1">{claim.Description}</p>
        </div>
        <ClaimStatusBadge status={claim.Status} />
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
        <InfoCard label="Amount" value={<CurrencyAmount amount={claim.Amount} currencyCode={claim.CurrencyCode} />} />
        <InfoCard label="Paid" value={<CurrencyAmount amount={claim.PaidAmount} currencyCode={claim.CurrencyCode} />} />
        <InfoCard label="Remaining" value={<CurrencyAmount amount={claim.RemainingBalance} currencyCode={claim.CurrencyCode} />} />
        <InfoCard label="Total Due" value={<CurrencyAmount amount={claim.TotalDue} currencyCode={claim.CurrencyCode} />} />
      </div>

      {/* Details */}
      <div className="bg-white rounded-lg border border-gray-200 p-6 mb-6">
        <h2 className="text-lg font-semibold mb-3">Details</h2>
        <dl className="grid grid-cols-2 md:grid-cols-3 gap-x-6 gap-y-3 text-sm">
          <Detail label="Creditor ID" value={claim.CreditorId} />
          <Detail label="Debtor Kennitala" value={claim.DebtorKennitala} />
          <Detail label="Currency" value={claim.CurrencyCode} />
          <Detail label="Category" value={claim.CategoryCode} />
          <Detail label="Due Date" value={new Date(claim.DueDate).toLocaleDateString('is-IS')} />
          <Detail label="Created" value={new Date(claim.Inserted).toLocaleString('is-IS')} />
          {claim.LateFee > 0 && <Detail label="Late Fee" value={formatAmount(claim.LateFee, claim.CurrencyCode) + ' ' + claim.CurrencyCode} />}
          {claim.AccruedInterest > 0 && <Detail label="Accrued Interest" value={formatAmount(claim.AccruedInterest, claim.CurrencyCode) + ' ' + claim.CurrencyCode} />}
          {claim.InterestRate > 0 && <Detail label="Interest Rate" value={`${claim.InterestRate}%`} />}
          {claim.CancellationReason && <Detail label="Cancellation Reason" value={claim.CancellationReason} />}
          {claim.DisputeReason && <Detail label="Dispute Reason" value={claim.DisputeReason} />}
        </dl>
      </div>

      {/* Due Costs Breakdown — shown when late fee or interest has been calculated */}
      {(claim.LateFee > 0 || claim.AccruedInterest > 0) && (
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-6 mb-6">
          <h2 className="text-lg font-semibold mb-3">Due Costs Breakdown</h2>
          <dl className="grid grid-cols-2 md:grid-cols-3 gap-x-6 gap-y-3 text-sm">
            <Detail label="Original Amount" value={formatAmount(claim.Amount, claim.CurrencyCode) + ' ' + claim.CurrencyCode} />
            <Detail label="Late Fee" value={formatAmount(claim.LateFee, claim.CurrencyCode) + ' ' + claim.CurrencyCode} />
            <Detail label="Days Overdue" value={String(Math.max(0, Math.floor((Date.now() - new Date(claim.DueDate).getTime()) / 86400000)))} />
            <Detail label="Interest Rate" value={`${claim.InterestRate ?? 0}%`} />
            <Detail label="Accrued Interest" value={formatAmount(claim.AccruedInterest, claim.CurrencyCode) + ' ' + claim.CurrencyCode} />
            <Detail label="Total Due" value={formatAmount(claim.TotalDue, claim.CurrencyCode) + ' ' + claim.CurrencyCode} />
            <Detail label="Paid" value={formatAmount(claim.PaidAmount, claim.CurrencyCode) + ' ' + claim.CurrencyCode} />
            <Detail label="Remaining" value={formatAmount(claim.RemainingBalance, claim.CurrencyCode) + ' ' + claim.CurrencyCode} />
          </dl>
        </div>
      )}

      {/* Actions */}
      <div className="flex flex-wrap gap-2 mb-6">
        {canSend && (
          <button onClick={handleSend} className="px-4 py-2 text-sm font-medium bg-indigo-600 text-white rounded-md hover:bg-indigo-700">
            Send to Debtor
          </button>
        )}
        {canPay && (
          <button onClick={() => setShowPayModal(true)} className="px-4 py-2 text-sm font-medium bg-green-600 text-white rounded-md hover:bg-green-700">
            Record Payment
          </button>
        )}
        {canEdit && (
          <button onClick={() => setShowEditModal(true)} className="px-4 py-2 text-sm font-medium bg-blue-600 text-white rounded-md hover:bg-blue-700">
            Edit Claim
          </button>
        )}
        {canMarkOverdue && (
          <button onClick={handleMarkOverdue} className="px-4 py-2 text-sm font-medium bg-amber-600 text-white rounded-md hover:bg-amber-700">
            Mark Overdue
          </button>
        )}
        {canCalcDueCosts && (
          <button onClick={handleCalculateDueCosts} className="px-4 py-2 text-sm font-medium bg-yellow-600 text-white rounded-md hover:bg-yellow-700">
            Calculate Due Costs
          </button>
        )}
        {canDispute && (
          <button onClick={() => setShowDisputeModal(true)} className="px-4 py-2 text-sm font-medium bg-orange-600 text-white rounded-md hover:bg-orange-700">
            Dispute
          </button>
        )}
        {canCancel && (
          <button onClick={() => setShowCancelModal(true)} className="px-4 py-2 text-sm font-medium bg-red-600 text-white rounded-md hover:bg-red-700">
            Cancel Claim
          </button>
        )}
        {canUploadDoc && (
          <label className="px-4 py-2 text-sm font-medium bg-gray-600 text-white rounded-md hover:bg-gray-700 cursor-pointer">
            Upload Document
            <input type="file" accept=".pdf" onChange={handleUpload} className="hidden" />
          </label>
        )}
      </div>

      {/* Status History */}
      <div className="bg-white rounded-lg border border-gray-200 p-6 mb-6">
        <h2 className="text-lg font-semibold mb-3">Status History</h2>
        {history.length === 0 ? (
          <p className="text-sm text-gray-500">No status changes recorded.</p>
        ) : (
          <div className="space-y-3">
            {history.map((entry) => (
              <div key={entry.Id} className="flex items-start gap-3 text-sm">
                <div className="w-32 flex-shrink-0 text-gray-500">
                  {new Date(entry.ChangedAt).toLocaleString('is-IS')}
                </div>
                <div>
                  <ClaimStatusBadge status={entry.FromStatus} />
                  <span className="mx-2 text-gray-400">&rarr;</span>
                  <ClaimStatusBadge status={entry.ToStatus} />
                  {entry.Reason && <p className="text-gray-500 mt-1 text-xs">{entry.Reason}</p>}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Payments */}
      <div className="bg-white rounded-lg border border-gray-200 p-6 mb-6">
        <h2 className="text-lg font-semibold mb-3">Payments</h2>
        {payments.length === 0 ? (
          <p className="text-sm text-gray-500">No payments recorded.</p>
        ) : (
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead>
              <tr>
                <th className="text-left py-2 pr-4 font-medium text-gray-500">Date</th>
                <th className="text-left py-2 pr-4 font-medium text-gray-500">Amount</th>
                <th className="text-left py-2 pr-4 font-medium text-gray-500">Reference</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {payments.map((p) => (
                <tr key={p.Id}>
                  <td className="py-2 pr-4 text-gray-500">{new Date(p.PaymentDate).toLocaleString('is-IS')}</td>
                  <td className="py-2 pr-4"><CurrencyAmount amount={p.Amount} currencyCode={p.CurrencyCode} /></td>
                  <td className="py-2 pr-4 text-gray-500">{p.PaymentReference}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Documents */}
      <div className="bg-white rounded-lg border border-gray-200 p-6 mb-6">
        <h2 className="text-lg font-semibold mb-3">Documents</h2>
        {documents.length === 0 ? (
          <p className="text-sm text-gray-500">No documents attached.</p>
        ) : (
          <ul className="space-y-2">
            {documents.map((doc) => (
              <li key={doc.Id} className="flex items-center justify-between text-sm">
                <div>
                  <span className="font-medium">{doc.FileName}</span>
                  <span className="text-gray-500 ml-2">({(doc.FileSizeBytes / 1024).toFixed(1)} KB)</span>
                  <span className="text-gray-400 ml-2">{new Date(doc.UploadedAt).toLocaleString('is-IS')}</span>
                </div>
                <a
                  href={api.downloadDocumentUrl(claim.Id, doc.Id)}
                  className="text-indigo-600 hover:text-indigo-800 text-sm"
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  Download
                </a>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Pay Modal */}
      {showPayModal && (
        <PaymentModal
          claim={claim}
          bankId={bankId}
          onClose={() => setShowPayModal(false)}
          onSuccess={loadClaim}
          setError={setError}
        />
      )}

      {/* Cancel Modal */}
      {showCancelModal && (
        <ReasonModal
          title="Cancel Claim"
          placeholder="Reason for cancellation"
          buttonLabel="Cancel Claim"
          buttonClass="bg-red-600 hover:bg-red-700"
          onClose={() => setShowCancelModal(false)}
          onSubmit={async (reason) => {
            await api.cancelClaim(claim.Id, reason)
            await loadClaim()
          }}
          setError={setError}
        />
      )}

      {/* Dispute Modal */}
      {showDisputeModal && (
        <ReasonModal
          title="Dispute Claim"
          placeholder="Reason for dispute"
          buttonLabel="Submit Dispute"
          buttonClass="bg-orange-600 hover:bg-orange-700"
          onClose={() => setShowDisputeModal(false)}
          onSubmit={async (reason) => {
            await api.updateClaimStatus(claim.Id, ClaimStatusName[ClaimStatus.Disputed], reason)
            await loadClaim()
          }}
          setError={setError}
        />
      )}

      {/* Edit Modal */}
      {showEditModal && (
        <EditModal
          claim={claim}
          onClose={() => setShowEditModal(false)}
          onSuccess={loadClaim}
          setError={setError}
        />
      )}
    </div>
  )
}

function InfoCard({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="bg-white rounded-lg border border-gray-200 p-4">
      <p className="text-sm text-gray-500">{label}</p>
      <p className="text-lg font-semibold mt-1">{value}</p>
    </div>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-gray-500">{label}</dt>
      <dd className="font-medium">{value}</dd>
    </div>
  )
}

function PaymentModal({
  claim,
  bankId,
  onClose,
  onSuccess,
  setError,
}: {
  claim: Claim
  bankId: string
  onClose: () => void
  onSuccess: () => Promise<void>
  setError: (err: string | null) => void
}) {
  const [amount, setAmount] = useState(String(claim.RemainingBalance ?? 0))
  const [reference, setReference] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await api.recordPayment(claim.Id, {
        amount: parseInt(amount, 10),
        currencyCode: claim.CurrencyCode,
        bankId,
        paymentReference: reference || `PAY-${Date.now()}`,
      })
      onClose()
      await onSuccess()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Payment failed')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal title="Record Payment" onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Amount ({claim.CurrencyCode}, remaining: {formatAmount(claim.RemainingBalance, claim.CurrencyCode)})
          </label>
          <input
            type="number"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            min={1}
            max={claim.RemainingBalance}
            required
            className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Payment Reference</label>
          <input
            type="text"
            value={reference}
            onChange={(e) => setReference(e.target.value)}
            placeholder="TXN-2026-12345"
            className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>
        <button
          type="submit"
          disabled={submitting}
          className="w-full bg-green-600 text-white py-2 px-4 rounded-md text-sm font-medium hover:bg-green-700 disabled:opacity-50"
        >
          {submitting ? 'Recording...' : 'Record Payment'}
        </button>
      </form>
    </Modal>
  )
}

function ReasonModal({
  title,
  placeholder,
  buttonLabel,
  buttonClass,
  onClose,
  onSubmit,
  setError,
}: {
  title: string
  placeholder: string
  buttonLabel: string
  buttonClass: string
  onClose: () => void
  onSubmit: (reason: string) => Promise<void>
  setError: (err: string | null) => void
}) {
  const [reason, setReason] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await onSubmit(reason)
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Action failed')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal title={title} onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Reason</label>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={placeholder}
            required
            rows={3}
            className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>
        <button
          type="submit"
          disabled={submitting}
          className={`w-full text-white py-2 px-4 rounded-md text-sm font-medium disabled:opacity-50 ${buttonClass}`}
        >
          {submitting ? 'Submitting...' : buttonLabel}
        </button>
      </form>
    </Modal>
  )
}

function EditModal({
  claim,
  onClose,
  onSuccess,
  setError,
}: {
  claim: Claim
  onClose: () => void
  onSuccess: () => Promise<void>
  setError: (err: string | null) => void
}) {
  const [amount, setAmount] = useState(String(claim.Amount ?? 0))
  const [dueDate, setDueDate] = useState(claim.DueDate.split('T')[0] ?? '')
  const [description, setDescription] = useState(claim.Description)
  const [categoryCode, setCategoryCode] = useState(claim.CategoryCode)
  const [submitting, setSubmitting] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await api.modifyClaim(claim.Id, {
        amount: parseInt(amount, 10),
        dueDate,
        description,
        categoryCode,
      })
      onClose()
      await onSuccess()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to modify claim')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal title="Edit Claim" onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Amount (minor units)</label>
          <input
            type="number"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            min={1}
            required
            className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Due Date</label>
          <input
            type="date"
            value={dueDate}
            onChange={(e) => setDueDate(e.target.value)}
            required
            className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Category</label>
          <select
            value={categoryCode}
            onChange={(e) => setCategoryCode(e.target.value)}
            className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            {['UTILITY', 'TELECOM', 'LOAN', 'OTHER'].map((c) => (
              <option key={c} value={c}>{c}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={500}
            rows={3}
            required
            className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>
        <button
          type="submit"
          disabled={submitting}
          className="w-full bg-blue-600 text-white py-2 px-4 rounded-md text-sm font-medium hover:bg-blue-700 disabled:opacity-50"
        >
          {submitting ? 'Saving...' : 'Save Changes'}
        </button>
      </form>
    </Modal>
  )
}

function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: React.ReactNode }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={onClose}>
      <div className="bg-white rounded-lg shadow-xl w-full max-w-md mx-4 p-6" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-lg font-semibold">{title}</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">&times;</button>
        </div>
        {children}
      </div>
    </div>
  )
}
