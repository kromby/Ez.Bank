# PRD: Cross-Bank Claims System (Kröfupottur)

## Summary

A centralized claims (kröfur) API that allows creditors to create payment claims and debtors to view and pay them — across all participating banks. Includes a React demo application for creating and viewing claims. Modeled after the Icelandic Kröfupotturinn operated by RB.

## Problem

Creditors (companies, institutions, individuals) need a way to issue payment claims to debtors. Today, each bank would handle this independently, forcing debtors to check multiple systems and creditors to integrate with each bank separately. A centralized claims pool eliminates this fragmentation.

## Goals

- Provide a REST API for creating, viewing, updating, and paying claims across banks.
- Support the full claim lifecycle: Created → Sent → Viewed → Partially Paid → Paid → Overdue → Cancelled → Disputed.
- Support partial payments against a claim balance.
- Enable cross-bank visibility: a claim created via Bank A is viewable and payable via Bank B.
- Deliver a React demo application that exercises the create and view flows.
- Follow the existing Ez.Bank architecture: .NET 8, Azure Functions, SQL Server, Clean Architecture (Entities / UseCases / DataAccess).

## Non-goals

- Real payment processing or bank settlement. Payments are recorded as ledger entries; no funds transfer occurs.
- User authentication for the demo app. The demo uses a hardcoded user/bank context.
- Recurring/scheduled claims (subscriptions). Out of scope for v1.
- Notification delivery (email, SMS, push). The API exposes notification-ready events but does not send them.
- PDF or paper invoice generation (the system accepts PDF attachments but does not generate them).
- Currency exchange or conversion. The system enforces same-currency payments but does not convert between currencies.
- Production deployment, CI/CD, or infrastructure provisioning.

## Terminology

| Term | Definition |
|---|---|
| **Claim (Krafa)** | A payment request from a creditor to a debtor for a specific amount. |
| **Creditor (Kröfuhafi)** | The entity issuing the claim (company or individual). |
| **Debtor (Greiðandi)** | The entity that owes the payment. |
| **Bank (Banki)** | A participating financial institution. Creditors and debtors are associated with a bank. |
| **Payment (Greiðsla)** | A partial or full payment recorded against a claim. |
| **Claim Reference (Tilvísun)** | A unique, human-readable identifier for a claim (e.g., `KB-2026-00001234`). |
| **Currency (Gjaldmiðill)** | ISO 4217 currency code. A claim is denominated in one currency; payments must match. |
| **Document (Skjal)** | A PDF file attached to a claim (e.g., invoice, contract, supporting evidence). |

## Requirements

### Data Model

#### Bank

| Field | Type | Notes |
|---|---|---|
| ID | int | PK, auto-increment |
| Name | string(100) | e.g., "Landsbankinn" |
| Kennitala | string(10) | Icelandic national ID for the bank |
| Inserted | datetime | Audit |
| InsertedBy | int | Audit |
| Updated | datetime? | Audit |
| UpdatedBy | int? | Audit |
| Deleted | datetime? | Soft delete |

#### Currency

| Field | Type | Notes |
|---|---|---|
| ID | int | PK, auto-increment |
| Code | string(3) | ISO 4217 code (e.g., "ISK", "EUR", "USD", "GBP", "DKK", "SEK", "NOK") |
| Name | string(100) | e.g., "Icelandic króna" |
| DecimalPlaces | int | Number of decimal places (0 for ISK/JPY, 2 for EUR/USD). Determines amount precision. |
| Inserted/InsertedBy/Updated/UpdatedBy/Deleted | | Standard audit fields |

Pre-seeded currencies:

| Code | Name | DecimalPlaces |
|---|---|---|
| ISK | Icelandic króna | 0 |
| EUR | Euro | 2 |
| USD | US Dollar | 2 |
| GBP | British Pound | 2 |
| DKK | Danish krone | 2 |
| SEK | Swedish krona | 2 |
| NOK | Norwegian krone | 2 |

Amounts are stored as integers in the smallest unit of the currency. For ISK (0 decimals): 15000 = 15,000 ISK. For EUR (2 decimals): 15000 = 150.00 EUR.

#### Creditor

| Field | Type | Notes |
|---|---|---|
| ID | int | PK |
| Kennitala | string(10) | National ID of creditor |
| Name | string(200) | |
| BankID | int | FK → Bank |
| AccountNumber | string(26) | IBAN or local account number |
| Inserted/InsertedBy/Updated/UpdatedBy/Deleted | | Standard audit fields |

#### Debtor

| Field | Type | Notes |
|---|---|---|
| ID | int | PK |
| Kennitala | string(10) | National ID of debtor |
| Name | string(200) | |
| BankID | int | FK → Bank |
| Inserted/InsertedBy/Updated/UpdatedBy/Deleted | | Standard audit fields |

#### Claim

| Field | Type | Notes |
|---|---|---|
| ID | int | PK |
| ClaimReference | string(20) | Unique, generated (e.g., `KB-2026-00001234`) |
| CreditorID | int | FK → Creditor |
| DebtorID | int | FK → Debtor |
| CurrencyID | int | FK → Currency. Immutable after creation. |
| Amount | bigint | Total amount in smallest currency unit (e.g., ISK whole units, EUR cents) |
| PaidAmount | bigint | Sum of all payments. Maintained via trigger or application logic. |
| DueDate | date | Payment deadline |
| Status | int | FK → ClaimStatus |
| CategoryCode | string(10) | Claim type (e.g., "UTILITY", "TELECOM", "LOAN", "OTHER") |
| Description | string(500) | Human-readable description of what is owed |
| FinalDueDate | date? | Extended deadline after which claim is sent to collection |
| LateFee | bigint | Fixed fee applied when claim becomes overdue. Default from system config. |
| InterestRate | decimal(5,2) | Annual interest rate (%) applied from DueDate. Default from system config. |
| AccruedInterest | bigint | Calculated interest amount in ISK. Updated on demand via calculation endpoint. |
| TotalDue | bigint | Computed: Amount + LateFee + AccruedInterest - PaidAmount. |
| CancellationReason | string(500)? | Required when status = Cancelled |
| DisputeReason | string(500)? | Required when status = Disputed |
| Inserted/InsertedBy/Updated/UpdatedBy/Deleted | | Standard audit fields |

#### ClaimStatus

| ID | Name | Description |
|---|---|---|
| 1 | Created | Claim registered but not yet delivered to debtor's bank |
| 2 | Sent | Delivered to debtor's bank |
| 3 | Viewed | Debtor has opened/viewed the claim |
| 4 | PartiallyPaid | At least one payment recorded, balance remaining |
| 5 | Paid | PaidAmount >= Amount |
| 6 | Overdue | DueDate has passed, balance remaining |
| 7 | Cancelled | Creditor cancelled the claim |
| 8 | Disputed | Debtor filed a dispute |

#### Payment

| Field | Type | Notes |
|---|---|---|
| ID | int | PK |
| ClaimID | int | FK → Claim |
| CurrencyID | int | FK → Currency. Must match Claim.CurrencyID. |
| Amount | bigint | Payment amount in smallest currency unit. Must match claim currency. |
| PaymentDate | datetime | When payment was recorded |
| BankID | int | FK → Bank (the bank processing the payment) |
| PaymentReference | string(50) | Bank transaction reference |
| Inserted/InsertedBy/Updated/UpdatedBy/Deleted | | Standard audit fields |

#### SystemConfig

| Field | Type | Notes |
|---|---|---|
| ID | int | PK |
| Key | string(50) | Unique config key |
| Value | string(200) | Config value |
| Inserted/InsertedBy/Updated/UpdatedBy/Deleted | | Standard audit fields |

System-wide defaults:

| Key | Default Value | Description |
|---|---|---|
| `DefaultLateFee` | `950` | Fixed late fee in ISK applied when a claim becomes overdue |
| `DefaultInterestRate` | `12.00` | Annual interest rate (%) for overdue claims (dráttarvextir) |

#### ClaimStatusHistory

| Field | Type | Notes |
|---|---|---|
| ID | int | PK |
| ClaimID | int | FK → Claim |
| FromStatus | int | FK → ClaimStatus |
| ToStatus | int | FK → ClaimStatus |
| ChangedAt | datetime | |
| ChangedBy | int | User who triggered the change |
| Reason | string(500)? | Optional context |

#### ClaimDocument

| Field | Type | Notes |
|---|---|---|
| ID | int | PK |
| ClaimID | int | FK → Claim |
| FileName | string(255) | Original file name (e.g., "invoice-march-2026.pdf") |
| ContentType | string(100) | MIME type. Must be `application/pdf`. |
| FileSizeBytes | bigint | File size in bytes |
| StoragePath | string(500) | Path or key in blob storage (Azure Blob Storage or local file system) |
| UploadedAt | datetime | When the document was uploaded |
| UploadedBy | int | User who uploaded |
| Deleted | datetime? | Soft delete |

Constraints:
- Only PDF files accepted (`Content-Type: application/pdf`).
- Maximum file size: 10 MB per document.
- Maximum 5 documents per claim.
- Documents can be attached at any claim status except `Cancelled`.

### Status Transitions

Valid transitions:

```
Created   → Sent, Cancelled
Sent      → Viewed, Overdue, Cancelled
Viewed    → PartiallyPaid, Paid, Overdue, Cancelled, Disputed
PartiallyPaid → Paid, Overdue, Cancelled, Disputed
Overdue   → PartiallyPaid, Paid, Cancelled, Disputed
Disputed  → Sent (re-opened after resolution), Cancelled
```

Invalid transitions must return HTTP 409 Conflict with a message describing the allowed transitions from the current status.

### API Endpoints

Base path: `/api/claims`

#### Claims

| Method | Route | Description | Auth |
|---|---|---|---|
| POST | `/api/claims` | Create a new claim | Creditor |
| GET | `/api/claims/{id}` | Get claim by ID | Creditor or Debtor |
| GET | `/api/claims?debtorKennitala={kt}` | List claims for a debtor | Debtor's bank |
| GET | `/api/claims?creditorId={id}&status={status}` | List claims by creditor, optionally filtered by status | Creditor |
| PATCH | `/api/claims/{id}/status` | Update claim status | Depends on transition (see rules below) |
| GET | `/api/claims/{id}/history` | Get status change history for a claim | Creditor or Debtor |

#### Due Cost Calculation

| Method | Route | Description | Auth |
|---|---|---|---|
| POST | `/api/claims/{id}/calculate-due-costs` | Calculate and persist late fee + accrued interest for an overdue claim | Creditor or system |
| GET | `/api/claims/{id}/due-costs` | Get current due cost breakdown (late fee, interest, total due) without persisting | Any authenticated user |

#### Payments

| Method | Route | Description | Auth |
|---|---|---|---|
| POST | `/api/claims/{id}/payments` | Record a payment against a claim | Debtor's bank |
| GET | `/api/claims/{id}/payments` | List payments for a claim | Creditor or Debtor |

#### Documents

| Method | Route | Description | Auth |
|---|---|---|---|
| POST | `/api/claims/{id}/documents` | Upload a PDF document to a claim (multipart/form-data) | Creditor or Debtor |
| GET | `/api/claims/{id}/documents` | List all documents attached to a claim | Creditor or Debtor |
| GET | `/api/claims/{id}/documents/{documentId}` | Download a specific document | Creditor or Debtor |
| DELETE | `/api/claims/{id}/documents/{documentId}` | Soft-delete a document | Uploader only |

#### Banks

| Method | Route | Description | Auth |
|---|---|---|---|
| GET | `/api/banks` | List participating banks | Any authenticated user |
| GET | `/api/banks/{id}` | Get bank details | Any authenticated user |

#### Creditors

| Method | Route | Description | Auth |
|---|---|---|---|
| POST | `/api/creditors` | Register a creditor | Bank |
| GET | `/api/creditors/{id}` | Get creditor details | Bank |

#### Debtors

| Method | Route | Description | Auth |
|---|---|---|---|
| POST | `/api/debtors` | Register a debtor | Bank |
| GET | `/api/debtors/{id}` | Get debtor details | Bank |

### Request/Response Examples

#### POST /api/claims

Request:
```json
{
  "creditorId": 1,
  "debtorKennitala": "1234567890",
  "amount": 15000,
  "currencyCode": "ISK",
  "dueDate": "2026-04-01",
  "categoryCode": "UTILITY",
  "description": "Electricity bill - March 2026"
}
```

Response (201 Created):
```json
{
  "id": 42,
  "claimReference": "KB-2026-00000042",
  "creditorId": 1,
  "debtorId": 7,
  "amount": 15000,
  "paidAmount": 0,
  "currency": { "code": "ISK", "name": "Icelandic króna", "decimalPlaces": 0 },
  "dueDate": "2026-04-01",
  "status": "Created",
  "categoryCode": "UTILITY",
  "description": "Electricity bill - March 2026",
  "documents": [],
  "inserted": "2026-03-04T14:00:00Z"
}
```

#### POST /api/claims/{id}/payments

Request:
```json
{
  "amount": 5000,
  "currencyCode": "ISK",
  "bankId": 2,
  "paymentReference": "TXN-2026-98765"
}
```

Response (200 OK):
```json
{
  "id": 101,
  "claimId": 42,
  "amount": 5000,
  "currency": { "code": "ISK", "name": "Icelandic króna", "decimalPlaces": 0 },
  "paymentDate": "2026-03-05T10:30:00Z",
  "bankId": 2,
  "paymentReference": "TXN-2026-98765",
  "claim": {
    "id": 42,
    "amount": 15000,
    "paidAmount": 5000,
    "remainingAmount": 10000,
    "status": "PartiallyPaid"
  }
}
```

#### POST /api/claims/{id}/calculate-due-costs

Request: No body required. Calculation uses current date.

Response (200 OK):
```json
{
  "claimId": 42,
  "claimReference": "KB-2026-00000042",
  "originalAmount": 15000,
  "lateFee": 950,
  "daysOverdue": 30,
  "interestRate": 12.00,
  "accruedInterest": 148,
  "totalDue": 16098,
  "paidAmount": 0,
  "remainingBalance": 16098,
  "calculatedAt": "2026-05-01T12:00:00Z"
}
```

Interest formula: `AccruedInterest = floor(Amount * (InterestRate / 100) * (DaysOverdue / 365))`

Example: 15,000 ISK at 12% for 30 days = floor(15000 * 0.12 * 30/365) = floor(147.945) = 147 ISK.

#### PUT /api/claims/{id} (Modify Claim)

Request (partial update — only mutable fields):
```json
{
  "amount": 18000,
  "dueDate": "2026-05-01",
  "description": "Updated electricity bill - March 2026"
}
```

Response (200 OK): Returns the updated claim object.

Modification rules: See Business Rule 10 below.

#### DELETE /api/claims/{id} (Cancel Claim)

Request:
```json
{
  "reason": "Issued in error"
}
```

Response (200 OK): Returns the claim with status `Cancelled`.

#### POST /api/claims/{id}/documents (Upload PDF)

Request: `multipart/form-data` with a single file field named `file`.

```
Content-Type: multipart/form-data; boundary=---boundary
Content-Disposition: form-data; name="file"; filename="invoice-march-2026.pdf"
Content-Type: application/pdf

<binary PDF data>
```

Response (201 Created):
```json
{
  "id": 1,
  "claimId": 42,
  "fileName": "invoice-march-2026.pdf",
  "contentType": "application/pdf",
  "fileSizeBytes": 245832,
  "uploadedAt": "2026-03-04T14:05:00Z",
  "uploadedBy": 1
}
```

#### GET /api/claims/{id}/documents

Response (200 OK):
```json
[
  {
    "id": 1,
    "claimId": 42,
    "fileName": "invoice-march-2026.pdf",
    "contentType": "application/pdf",
    "fileSizeBytes": 245832,
    "uploadedAt": "2026-03-04T14:05:00Z",
    "uploadedBy": 1
  }
]
```

#### GET /api/claims/{id}/documents/{documentId}

Response: Binary PDF stream with `Content-Type: application/pdf` and `Content-Disposition: attachment; filename="invoice-march-2026.pdf"`.

### Business Rules

1. **Claim creation**: The debtor is resolved by kennitala. If no debtor record exists for that kennitala, the system creates one with BankID = null (unaffiliated). The debtor's bank is assigned when they first view or pay the claim.
2. **Payment recording**: A payment that causes `PaidAmount >= Amount` automatically transitions the claim to `Paid`. A payment against a `Sent` or `Viewed` claim transitions it to `PartiallyPaid` (if partial) or `Paid` (if full).
3. **Overpayment**: Payments that would cause `PaidAmount > Amount` are rejected with HTTP 400.
4. **Overdue processing**: A background process (or on-read check) marks claims as `Overdue` when `DueDate < today` and status is `Sent`, `Viewed`, or `PartiallyPaid`.
5. **Cancellation**: Only the creditor (or creditor's bank) can cancel a claim. Cancelled claims cannot receive payments.
6. **Dispute**: Only the debtor (or debtor's bank) can dispute a claim. Disputed claims cannot receive payments until re-opened.
7. **Claim reference format**: `KB-{year}-{zero-padded ID}`. Generated server-side, immutable.
8. **Amount**: Must be a positive integer greater than 0.
9. **DueDate**: Must be a future date (at least 1 day from creation).
10. **Claim modification**: A claim can only be modified (amount, dueDate, description, categoryCode) when its status is `Created` or `Sent`. Once a claim is `Viewed`, `PartiallyPaid`, `Paid`, `Overdue`, `Cancelled`, or `Disputed`, it is immutable. Modification records the change in ClaimStatusHistory with reason "Claim modified".
11. **Claim cancellation (niðurfelling)**: Uses `DELETE /api/claims/{id}` with a required reason. Soft-deletes the claim and sets status to `Cancelled`. Only the creditor or creditor's bank can cancel.
12. **Due cost calculation (dráttarvextir og innheimtukostnaður)**:
    - Triggered on demand via `POST /api/claims/{id}/calculate-due-costs`.
    - Only applies to claims with status `Overdue` (or `PartiallyPaid`/`Viewed`/`Sent` where DueDate has passed).
    - **Late fee**: A fixed amount (from `SystemConfig.DefaultLateFee`, default 950 ISK). Applied once. If already applied (LateFee > 0), not re-applied.
    - **Interest (dráttarvextir)**: `floor(Amount * (InterestRate / 100) * (DaysOverdue / 365))` where DaysOverdue = days since DueDate. Uses `SystemConfig.DefaultInterestRate` (default 12.00%).
    - Interest is recalculated on each invocation (not compounded — simple interest on the original amount).
    - `TotalDue = Amount + LateFee + AccruedInterest`.
    - Payments are applied against `TotalDue`, not just `Amount`. A claim is `Paid` when `PaidAmount >= TotalDue`.
13. **Due cost read-only endpoint**: `GET /api/claims/{id}/due-costs` returns the same calculation without persisting. Useful for previewing costs before committing.
14. **Currency enforcement**: Every claim has a `CurrencyID` set at creation. Currency is immutable after creation. Every payment must specify the same `currencyCode` as the claim. If a payment specifies a different currency, the API returns HTTP 400: "Payment currency {paymentCurrency} does not match claim currency {claimCurrency}."
15. **Amount precision by currency**: Amounts are stored as integers in the smallest unit. For 0-decimal currencies (ISK, JPY): `15000` = 15,000 ISK. For 2-decimal currencies (EUR, USD): `15000` = 150.00 EUR. The `Currency.DecimalPlaces` field defines the precision. The API accepts integer amounts only; display formatting is the client's responsibility.
16. **Document upload**: PDF documents can be attached to a claim via `POST /api/claims/{id}/documents` using `multipart/form-data`.
    - Only `application/pdf` content type accepted. Other types return HTTP 400: "Only PDF files are accepted."
    - Maximum file size: 10 MB. Larger files return HTTP 400: "File exceeds maximum size of 10 MB."
    - Maximum 5 documents per claim. Exceeding the limit returns HTTP 400: "Maximum of 5 documents per claim reached."
    - Documents cannot be attached to claims in `Cancelled` status. Returns HTTP 409: "Cannot attach documents to a cancelled claim."
    - File content is stored in Azure Blob Storage (or local file system for development). Metadata is stored in SQL.
17. **Document deletion**: Only the user who uploaded the document can delete it. Deletion is a soft delete (sets `Deleted` timestamp). Returns HTTP 403 if a different user attempts deletion.

### Constraints

- **Database**: SQL Server. Manual SQL scripts for schema creation (no EF migrations).
- **Concurrency**: Payments against the same claim must be serialized to prevent race conditions on `PaidAmount`. Use optimistic concurrency (row version) or serializable transactions on the payment insert + claim update.
- **Query performance**: The `GET /api/claims?debtorKennitala=` endpoint will be the highest-traffic query. Index on `Debtor.Kennitala` and `Claim.DebtorID + Claim.Status`.
- **Payload size**: List endpoints return max 100 items per page. Support `?page=1&pageSize=50` query parameters.
- **Amount precision**: All amounts stored as `bigint` in smallest currency unit. Precision determined by `Currency.DecimalPlaces`. Client is responsible for display formatting.
- **Document storage**: PDF files stored in Azure Blob Storage (production) or local file system (development). Controlled via `IDocumentStorage` interface. Max 10 MB per file, max 5 per claim.

### Project Structure

```
api/
├── Ez.Bank.sln
├── Ez.Bank.Core/                    # Shared base classes, interfaces
│   ├── Entities/
│   │   └── EntityBase.cs
│   └── Ez.Bank.Core.csproj
├── Ez.Bank.Claims/                  # Claims domain module
│   ├── Entities/
│   │   ├── Claim.cs
│   │   ├── ClaimStatus.cs
│   │   ├── Payment.cs
│   │   ├── Creditor.cs
│   │   ├── Debtor.cs
│   │   ├── Bank.cs
│   │   ├── Currency.cs
│   │   ├── ClaimDocument.cs
│   │   └── ClaimStatusHistory.cs
│   ├── UseCases/
│   │   ├── IClaimDataAccess.cs
│   │   ├── IPaymentDataAccess.cs
│   │   ├── ICreditorDataAccess.cs
│   │   ├── IDebtorDataAccess.cs
│   │   ├── IBankDataAccess.cs
│   │   ├── ICurrencyDataAccess.cs
│   │   ├── IClaimDocumentDataAccess.cs
│   │   ├── IDocumentStorage.cs
│   │   ├── ISystemConfigDataAccess.cs
│   │   ├── ClaimInteractor.cs
│   │   ├── PaymentInteractor.cs
│   │   ├── DocumentInteractor.cs
│   │   └── DueCostInteractor.cs
│   ├── DataAccess/
│   │   ├── ClaimSqlAccess.cs
│   │   ├── PaymentSqlAccess.cs
│   │   ├── CreditorSqlAccess.cs
│   │   ├── DebtorSqlAccess.cs
│   │   ├── BankSqlAccess.cs
│   │   ├── CurrencySqlAccess.cs
│   │   ├── ClaimDocumentSqlAccess.cs
│   │   ├── BlobDocumentStorage.cs
│   │   ├── LocalDocumentStorage.cs
│   │   └── SystemConfigSqlAccess.cs
│   └── Ez.Bank.Claims.csproj
├── Ez.Bank.FunctionsApi/            # Azure Functions HTTP triggers
│   ├── Claims/
│   │   ├── ClaimFunctions.cs
│   │   ├── PaymentFunctions.cs
│   │   ├── BankFunctions.cs
│   │   ├── CreditorFunctions.cs
│   │   ├── DebtorFunctions.cs
│   │   ├── DocumentFunctions.cs
│   │   └── DueCostFunctions.cs
│   ├── Program.cs
│   ├── host.json
│   └── Ez.Bank.FunctionsApi.csproj
├── Ez.Bank.UnitTests/
│   ├── Claims/
│   │   ├── ClaimInteractorTests.cs
│   │   └── PaymentInteractorTests.cs
│   └── Ez.Bank.UnitTests.csproj
└── db/
    └── migrations/
        └── 001-create-claims-schema.sql
```

### Demo Application (React)

Location: `/demo/ez-bank-demo/`

#### Pages

1. **Dashboard** — Summary: total claims, total paid, total overdue, total disputed.
2. **Create Claim** — Form: creditor (dropdown), debtor kennitala (text), amount, currency (dropdown, defaults to ISK), due date, category, description. Submits POST to API.
3. **Claims List** — Table with columns: Reference, Creditor, Debtor, Amount, Currency, Paid, Status, Due Date. Filterable by status and debtor kennitala. Paginated. Amounts formatted based on currency decimal places.
4. **Claim Detail** — Shows full claim info (including currency), status history timeline, payments list, documents list with upload/download/delete actions, and actions (Pay, Cancel, Dispute) based on current status.
5. **Record Payment** — Modal on Claim Detail: amount input (pre-filled with remaining balance), payment reference. Validates amount <= remaining.
6. **Calculate Due Costs** — Button on Claim Detail (visible for overdue claims). Calls `POST /api/claims/{id}/calculate-due-costs`. Shows breakdown: original amount, late fee, days overdue, accrued interest, total due, remaining balance. Refreshes claim detail after calculation.

#### Tech

- React 18+ with TypeScript
- Vite for build tooling
- Fetch API for HTTP calls (no axios — keep dependencies minimal)
- CSS Modules or plain CSS (no UI framework required for a demo)
- Environment variable for API base URL

## User Flows

### Flow 1: Creditor creates a claim

1. Creditor opens "Create Claim" page.
2. Selects themselves as creditor from dropdown (populated from `GET /api/creditors`).
3. Enters debtor kennitala, amount, selects currency (defaults to ISK), due date, category, description.
4. Submits form → `POST /api/claims`.
5. API validates inputs, resolves debtor by kennitala, creates claim with status `Created`, generates claim reference.
6. UI redirects to Claim Detail page showing the new claim.

### Flow 2: Debtor views and pays a claim

1. Debtor enters their kennitala on the Claims List page.
2. UI calls `GET /api/claims?debtorKennitala={kt}`.
3. Debtor sees list of claims. Clicks one.
4. UI calls `GET /api/claims/{id}` → API transitions status from `Sent` to `Viewed` (if first view).
5. Debtor clicks "Pay", enters amount (defaults to remaining balance).
6. UI calls `POST /api/claims/{id}/payments`.
7. API records payment, updates `PaidAmount`, transitions status if needed.
8. Claim Detail refreshes showing updated status and payment in the list.

### Flow 3: Debtor disputes a claim

1. From Claim Detail, debtor clicks "Dispute".
2. UI prompts for a reason (required text field).
3. UI calls `PATCH /api/claims/{id}/status` with `{ "status": "Disputed", "reason": "..." }`.
4. API validates transition is allowed, records status change with reason.
5. Claim Detail shows "Disputed" status and the reason.

### Flow 4: Attach a document to a claim

1. From Claim Detail, user clicks "Upload Document".
2. File picker opens. User selects a PDF file (max 10 MB).
3. UI validates: file is PDF, size <= 10 MB, claim has < 5 documents.
4. UI calls `POST /api/claims/{id}/documents` with `multipart/form-data`.
5. API validates content type, size, document count, and claim status.
6. File stored in blob/local storage. Metadata saved to SQL.
7. Documents list on Claim Detail refreshes showing the new document with download and delete links.

### Flow 5: Creditor modifies a claim

1. Creditor opens Claim Detail for a claim in `Created` or `Sent` status.
2. Clicks "Edit". Amount, due date, description, and category become editable.
3. Changes fields, clicks "Save" → `PUT /api/claims/{id}`.
4. API validates status allows modification, updates the claim, records change in history.
5. UI shows updated claim.

### Flow 6: Calculate due costs on overdue claim

1. Claim is in `Overdue` status (or past due date).
2. User opens Claim Detail. A "Calculate Due Costs" button is visible.
3. User clicks button → `POST /api/claims/{id}/calculate-due-costs`.
4. API calculates: late fee (950 ISK, applied once) + accrued interest (simple interest from due date to today).
5. API persists LateFee, AccruedInterest, TotalDue on the claim.
6. UI displays breakdown: original amount, late fee, days overdue, interest rate, accrued interest, total due, remaining balance.
7. Debtor can now pay against the new TotalDue.

### Flow 7: Overdue processing

1. A scheduled function (timer trigger) runs daily.
2. Queries claims where `DueDate < today` and status in (`Sent`, `Viewed`, `PartiallyPaid`).
3. Transitions each to `Overdue`, records in ClaimStatusHistory.

## Edge Cases

| Case | Expected Behaviour |
|---|---|
| Payment amount = 0 or negative | HTTP 400: "Amount must be a positive integer." |
| Payment exceeds remaining balance | HTTP 400: "Payment of {amount} exceeds remaining balance of {remaining}." |
| Two simultaneous payments that together exceed balance | First succeeds, second gets HTTP 409: "Claim balance changed. Remaining: {new remaining}." Retry with correct amount. |
| Claim created with past due date | HTTP 400: "Due date must be at least 1 day in the future." |
| Status transition not allowed | HTTP 409: "Cannot transition from {current} to {requested}. Allowed: [{list}]." |
| Debtor kennitala not found on claim creation | System creates a new Debtor record with the kennitala and BankID = null. |
| Cancel a claim with existing payments | Allowed. Status becomes Cancelled. No refund logic (out of scope). |
| Pay a Cancelled or Disputed claim | HTTP 409: "Cannot record payment. Claim status is {status}." |
| GET claims for kennitala with no claims | HTTP 200 with empty array `[]`, not 404. |
| Claim amount exceeds bigint max | Practically impossible for ISK amounts. No explicit guard needed. |
| Duplicate payment reference | Allowed. Payment references are informational, not unique constraints. |
| Calculate due costs on non-overdue claim | HTTP 400: "Claim is not overdue. Due date is {dueDate}." |
| Calculate due costs on Paid claim | HTTP 400: "Claim is already fully paid." |
| Calculate due costs on Cancelled claim | HTTP 400: "Cannot calculate costs for a cancelled claim." |
| Late fee already applied, calculate again | Late fee not re-applied. Interest recalculated with current date. |
| Payment after due costs calculated | Payment applied against TotalDue (Amount + LateFee + AccruedInterest). |
| Modify claim in Viewed status | HTTP 409: "Claim cannot be modified in status Viewed. Allowed statuses: Created, Sent." |
| Modify claim amount to less than PaidAmount | Cannot happen — modification only allowed in Created/Sent (PaidAmount = 0). |
| Cancel claim with due costs accrued | Allowed. Claim is cancelled. No refund of already-paid amounts. |
| Payment in different currency than claim | HTTP 400: "Payment currency {X} does not match claim currency {Y}." |
| Claim created with unknown currency code | HTTP 400: "Currency code {X} is not supported." |
| Upload non-PDF file | HTTP 400: "Only PDF files are accepted. Received: {contentType}." |
| Upload file exceeding 10 MB | HTTP 400: "File exceeds maximum size of 10 MB." |
| Upload 6th document to a claim | HTTP 400: "Maximum of 5 documents per claim reached." |
| Upload document to cancelled claim | HTTP 409: "Cannot attach documents to a cancelled claim." |
| Delete document uploaded by another user | HTTP 403: "Only the uploader can delete this document." |
| Download document that has been soft-deleted | HTTP 404. |
| Claim GET response includes documents array | Always included; empty array if no documents. |

## Success Metrics

| Metric | Target |
|---|---|
| API response time (p95) for single claim GET | < 200ms |
| API response time (p95) for claims list (100 items) | < 500ms |
| Payment recording with concurrent requests (no data loss) | 0 lost payments under 10 concurrent requests to same claim |
| Unit test coverage on ClaimInteractor and PaymentInteractor | > 80% line coverage |
| Demo app: create claim → view in list | Functional end-to-end in < 5 clicks |
| All status transitions enforce valid-transition rules | 100% of invalid transitions return 409 |

## Dependencies and Blockers

| Dependency | Impact |
|---|---|
| SQL Server instance (local or Azure) | Required before any data access work can begin |
| .NET 8 SDK | Required for API development |
| Node.js 18+ | Required for React demo app |
| Azure Blob Storage or local file system | Required for PDF document storage. Local FS fallback for development. |
| No existing Ez.Bank code in repo | Greenfield — no migration or compatibility constraints, but all scaffolding must be built |

## Open Questions

1. **Authentication model for v1**: Should the API use JWT (as in the reference Hress.Org project) or API keys per bank? JWT is recommended for consistency.
2. **Kennitala validation**: Should the system validate kennitala check digits, or accept any 10-digit string? Validation is recommended but can be deferred.
3. **Claim expiry**: Should unpaid claims expire after a configurable period past FinalDueDate? Not included in v1 scope but noted for future consideration.
