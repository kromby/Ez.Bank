import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import { ErrorAlert } from '../components/ErrorAlert'

interface CreateClaimProps {
  creditorId: string
}

const CATEGORIES = ['UTILITY', 'TELECOM', 'LOAN', 'OTHER']
const CURRENCIES = ['ISK', 'EUR', 'USD', 'GBP', 'DKK', 'SEK', 'NOK']

export function CreateClaim({ creditorId }: CreateClaimProps) {
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const [form, setForm] = useState({
    debtorKennitala: '',
    amount: '',
    currencyCode: 'ISK',
    dueDate: '',
    categoryCode: 'UTILITY',
    description: '',
  })

  const updateField = (field: string, value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSubmitting(true)

    try {
      const claim = await api.createClaim({
        creditorId,
        debtorKennitala: form.debtorKennitala,
        amount: parseInt(form.amount, 10),
        currencyCode: form.currencyCode,
        dueDate: form.dueDate,
        categoryCode: form.categoryCode,
        description: form.description,
      })
      navigate(`/claims/${claim.Id}`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create claim')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="max-w-2xl">
      <h1 className="text-2xl font-bold mb-6">Create Claim</h1>

      {error && <ErrorAlert message={error} onDismiss={() => setError(null)} />}

      <form onSubmit={handleSubmit} className="bg-white rounded-lg border border-gray-200 p-6 space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Creditor ID</label>
          <input
            type="text"
            value={creditorId}
            disabled
            className="w-full rounded-md border-gray-300 bg-gray-100 text-sm py-2 px-3 border"
          />
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Debtor Kennitala</label>
          <input
            type="text"
            value={form.debtorKennitala}
            onChange={(e) => updateField('debtorKennitala', e.target.value)}
            placeholder="1234567890"
            maxLength={10}
            pattern="\d{10}"
            required
            className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Amount (minor units)</label>
            <input
              type="number"
              value={form.amount}
              onChange={(e) => updateField('amount', e.target.value)}
              placeholder="50000"
              min={1}
              required
              className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Currency</label>
            <select
              value={form.currencyCode}
              onChange={(e) => updateField('currencyCode', e.target.value)}
              className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              {CURRENCIES.map((c) => (
                <option key={c} value={c}>{c}</option>
              ))}
            </select>
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Due Date</label>
          <input
            type="date"
            value={form.dueDate}
            onChange={(e) => updateField('dueDate', e.target.value)}
            required
            className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Category</label>
          <select
            value={form.categoryCode}
            onChange={(e) => updateField('categoryCode', e.target.value)}
            className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            {CATEGORIES.map((c) => (
              <option key={c} value={c}>{c}</option>
            ))}
          </select>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
          <textarea
            value={form.description}
            onChange={(e) => updateField('description', e.target.value)}
            placeholder="Electricity bill - March 2026"
            maxLength={500}
            rows={3}
            required
            className="w-full rounded-md border-gray-300 text-sm py-2 px-3 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>

        <button
          type="submit"
          disabled={submitting}
          className="w-full bg-indigo-600 text-white py-2 px-4 rounded-md text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {submitting ? 'Creating...' : 'Create Claim'}
        </button>
      </form>
    </div>
  )
}
