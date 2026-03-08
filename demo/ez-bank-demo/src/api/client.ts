import type { ApiError } from '../types'

const API_BASE = '/api'

let authToken: string | null = null

export function setToken(token: string | null) {
  authToken = token
}

export function getToken() {
  return authToken
}

class ApiRequestError extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
  ) {
    super(message)
    this.name = 'ApiRequestError'
  }
}

export { ApiRequestError }

async function request<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const headers: Record<string, string> = {
    ...(options.headers as Record<string, string>),
  }

  if (authToken) {
    headers['Authorization'] = `Bearer ${authToken}`
  }

  if (
    options.body &&
    typeof options.body === 'string' &&
    !headers['Content-Type']
  ) {
    headers['Content-Type'] = 'application/json'
  }

  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
  })

  if (!res.ok) {
    let code = 'UNKNOWN'
    let message = `Request failed with status ${res.status}`
    try {
      const body = (await res.json()) as ApiError
      if (body.Error) {
        code = body.Error.Code
        message = body.Error.Message
      }
    } catch {
      // use defaults
    }
    throw new ApiRequestError(res.status, code, message)
  }

  if (res.status === 204) return undefined as T

  const body = await res.json()

  // The API sometimes returns errors with HTTP 200
  if (body && typeof body === 'object' && 'Error' in body && body.Error) {
    const err = body as ApiError
    throw new ApiRequestError(res.status, err.Error.Code, err.Error.Message)
  }

  return body as T
}

export async function authenticate(
  bankId: string,
  userId: string,
  userRole: string,
): Promise<string> {
  const data = await request<{ Token: string; ExpiresIn: number }>(
    '/auth/token',
    {
      method: 'POST',
      body: JSON.stringify({
        password: 'dev-password-change-me',
        bankId,
        userId,
        userRole,
      }),
    },
  )
  authToken = data.Token
  return data.Token
}

export const api = {
  // Health
  health: () => request<{ Status: string }>('/health'),

  // Banks
  listBanks: () => request<Bank[]>('/banks'),

  // Creditors
  createCreditor: (data: {
    kennitala: string
    name: string
    bankId: string
    accountNumber: string
  }) =>
    request<Creditor>('/creditors', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  getCreditor: (id: string) => request<Creditor>(`/creditors/${id}`),

  // Debtors
  createDebtor: (data: { kennitala: string; name: string; bankId: string }) =>
    request<Debtor>('/debtors', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  // Claims
  createClaim: (data: {
    creditorId: string
    debtorKennitala: string
    amount: number
    currencyCode: string
    dueDate: string
    categoryCode: string
    description: string
  }) =>
    request<Claim>('/claims', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  getClaim: (id: string) => request<Claim>(`/claims/${id}`),

  getClaimsByDebtor: (kennitala: string, pageSize = 50, continuationToken?: string) => {
    let url = `/claims/by-debtor/${kennitala}?pageSize=${pageSize}`
    if (continuationToken) url += `&continuationToken=${encodeURIComponent(continuationToken)}`
    return request<PagedResult<Claim>>(url)
  },

  getClaimsByCreditor: (creditorId: string, status?: number, pageSize = 50, continuationToken?: string) => {
    let url = `/claims/by-creditor/${creditorId}?pageSize=${pageSize}`
    if (status !== undefined) url += `&status=${status}`
    if (continuationToken) url += `&continuationToken=${encodeURIComponent(continuationToken)}`
    return request<PagedResult<Claim>>(url)
  },

  updateClaimStatus: (id: string, status: string, reason?: string) =>
    request<Claim>(`/claims/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status, reason }),
    }),

  modifyClaim: (id: string, data: { amount?: number; dueDate?: string; description?: string; categoryCode?: string }) =>
    request<Claim>(`/claims/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  cancelClaim: (id: string, reason: string) =>
    request<Claim>(`/claims/${id}`, {
      method: 'DELETE',
      body: JSON.stringify({ reason }),
      headers: { 'Content-Type': 'application/json' },
    }),

  getClaimHistory: (id: string) =>
    request<ClaimStatusHistoryEntry[]>(`/claims/${id}/history`),

  // Payments
  recordPayment: (claimId: string, data: { amount: number; currencyCode: string; bankId: string; paymentReference: string }) =>
    request<Payment>(`/claims/${claimId}/payments`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  getPayments: (claimId: string) =>
    request<Payment[]>(`/claims/${claimId}/payments`),

  // Documents
  uploadDocument: (claimId: string, file: File) => {
    const formData = new FormData()
    formData.append('file', file)
    return request<ClaimDocument>(`/claims/${claimId}/documents`, {
      method: 'POST',
      body: formData,
    })
  },

  getDocuments: (claimId: string) =>
    request<ClaimDocument[]>(`/claims/${claimId}/documents`),

  downloadDocumentUrl: (claimId: string, documentId: string) =>
    `${API_BASE}/claims/${claimId}/documents/${documentId}`,

  // Due costs
  calculateDueCosts: (claimId: string) =>
    request<DueCostResult>(`/claims/${claimId}/calculate-due-costs`, {
      method: 'POST',
    }),

  getDueCosts: (claimId: string) =>
    request<DueCostResult>(`/claims/${claimId}/due-costs`),
}

import type { Claim, Creditor, Bank, Payment, ClaimStatusHistoryEntry, ClaimDocument, PagedResult, DueCostResult } from '../types'

interface Debtor {
  Id: string
  Kennitala: string
  Name: string
  BankId: string
}
