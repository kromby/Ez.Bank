export enum ClaimStatus {
  Created = 1,
  Sent = 2,
  Viewed = 3,
  PartiallyPaid = 4,
  Paid = 5,
  Overdue = 6,
  Cancelled = 7,
  Disputed = 8,
}

export const ClaimStatusLabel: Record<ClaimStatus, string> = {
  [ClaimStatus.Created]: 'Created',
  [ClaimStatus.Sent]: 'Sent',
  [ClaimStatus.Viewed]: 'Viewed',
  [ClaimStatus.PartiallyPaid]: 'Partially Paid',
  [ClaimStatus.Paid]: 'Paid',
  [ClaimStatus.Overdue]: 'Overdue',
  [ClaimStatus.Cancelled]: 'Cancelled',
  [ClaimStatus.Disputed]: 'Disputed',
}

export const ClaimStatusName: Record<ClaimStatus, string> = {
  [ClaimStatus.Created]: 'Created',
  [ClaimStatus.Sent]: 'Sent',
  [ClaimStatus.Viewed]: 'Viewed',
  [ClaimStatus.PartiallyPaid]: 'PartiallyPaid',
  [ClaimStatus.Paid]: 'Paid',
  [ClaimStatus.Overdue]: 'Overdue',
  [ClaimStatus.Cancelled]: 'Cancelled',
  [ClaimStatus.Disputed]: 'Disputed',
}

export interface Claim {
  Id: string
  ClaimReference: string
  CreditorId: string
  DebtorKennitala: string
  CurrencyCode: string
  Amount: number
  PaidAmount: number
  DueDate: string
  Status: ClaimStatus
  CategoryCode: string
  Description: string
  FinalDueDate: string | null
  LateFee: number
  InterestRate: number
  AccruedInterest: number
  TotalDue: number
  RemainingBalance: number
  CancellationReason: string | null
  DisputeReason: string | null
  Inserted: string
  InsertedBy: string
}

export interface Payment {
  Id: string
  ClaimId: string
  CurrencyCode: string
  Amount: number
  PaymentDate: string
  BankId: string
  PaymentReference: string
  Inserted: string
}

export interface ClaimStatusHistoryEntry {
  Id: string
  ClaimId: string
  FromStatus: ClaimStatus
  ToStatus: ClaimStatus
  ChangedAt: string
  ChangedBy: string
  Reason: string | null
}

export interface ClaimDocument {
  Id: string
  ClaimId: string
  FileName: string
  ContentType: string
  FileSizeBytes: number
  StoragePath: string
  UploadedAt: string
  UploadedBy: string
}

export interface Creditor {
  Id: string
  Kennitala: string
  Name: string
  BankId: string
  AccountNumber: string
}

export interface Bank {
  Id: string
  Name: string
  Kennitala: string
}

export interface PagedResult<T> {
  Items: T[]
  PageSize: number
  ContinuationToken: string | null
  HasMore: boolean
}

export interface DueCostResult {
  ClaimId: string
  ClaimReference: string
  OriginalAmount: number
  LateFee: number
  DaysOverdue: number
  InterestRate: number
  AccruedInterest: number
  TotalDue: number
  PaidAmount: number
  RemainingBalance: number
  CalculatedAt: string
}

export interface ApiError {
  Error: {
    Code: string
    Message: string
  }
}

export type UserRole = 'Creditor' | 'Debtor'
