import { ClaimStatus, ClaimStatusLabel } from '../types'

const statusStyles: Record<ClaimStatus, string> = {
  [ClaimStatus.Created]: 'bg-blue-100 text-blue-800',
  [ClaimStatus.Sent]: 'bg-indigo-100 text-indigo-800',
  [ClaimStatus.Viewed]: 'bg-purple-100 text-purple-800',
  [ClaimStatus.PartiallyPaid]: 'bg-yellow-100 text-yellow-800',
  [ClaimStatus.Paid]: 'bg-green-100 text-green-800',
  [ClaimStatus.Overdue]: 'bg-red-100 text-red-800',
  [ClaimStatus.Cancelled]: 'bg-gray-100 text-gray-800',
  [ClaimStatus.Disputed]: 'bg-orange-100 text-orange-800',
}

export function ClaimStatusBadge({ status }: { status: ClaimStatus }) {
  const style = statusStyles[status] ?? 'bg-gray-100 text-gray-800'
  const label = ClaimStatusLabel[status] ?? `Unknown (${status})`

  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${style}`}>
      {label}
    </span>
  )
}
