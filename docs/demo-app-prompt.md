# Demo App Implementation Prompt

Use this as the opening message in a new Claude Code session from the `/home/kromby/source/Ez.Bank` directory.

---

Build a React + TypeScript demo app for the Ez.Bank Claims API. The API is already built and running locally at `http://localhost:7071/api`.

## Context

Read these files first for full context:
- `docs/PRD-claims-system.md` — product requirements
- `docs/technical-design.md` — API design, entities, endpoints, status machine
- `docs/deployment-guide.md` — environment setup

The API has 25 endpoints. Key ones for the demo:

```
POST   /api/auth/token                              — get JWT (password: "dev-password-change-me")
GET    /api/health                                   — health check
POST   /api/creditors                                — register creditor
POST   /api/debtors                                  — register debtor
POST   /api/claims                                   — create claim
GET    /api/claims/{id}                              — get claim
GET    /api/claims/by-debtor/{kennitala}             — claims for debtor
GET    /api/claims/by-creditor/{creditorId}          — claims for creditor
PATCH  /api/claims/{id}/status                       — update status (body: {"status":"Sent"})
PUT    /api/claims/{id}                              — modify claim
DELETE /api/claims/{id}                              — cancel claim (body: {"reason":"..."})
POST   /api/claims/{claimId}/payments                — record payment
GET    /api/claims/{claimId}/payments                — list payments
POST   /api/claims/{claimId}/documents               — upload PDF (multipart)
GET    /api/claims/{claimId}/documents               — list documents
GET    /api/claims/{id}/history                      — status history
POST   /api/claims/{claimId}/calculate-due-costs     — calculate late fees
GET    /api/claims/{claimId}/due-costs               — preview due costs
GET    /api/banks                                    — list banks
```

Auth: POST to `/api/auth/token` with `{"password":"dev-password-change-me","bankId":"bank-1","userId":"user-1","userRole":"Creditor"}`. Returns `{"Token":"...","ExpiresIn":3600}`. Use the token as `Authorization: Bearer <token>` on all other requests.

API responses use PascalCase JSON (e.g., `ClaimReference`, `PaidAmount`, `TotalDue`). Status is returned as an integer enum: Created=1, Sent=2, Viewed=3, PartiallyPaid=4, Paid=5, Overdue=6, Cancelled=7, Disputed=8.

Pagination uses continuation tokens: `?pageSize=50&continuationToken=...` → `{"Items":[...],"PageSize":50,"ContinuationToken":"...","HasMore":true}`.

## Requirements

- **Stack:** React 18+, TypeScript, Vite, Tailwind CSS. No component library — keep it simple.
- **Location:** `demo/ez-bank-demo/` (already referenced in project structure).
- **Role switching:** The demo should let the user switch between Creditor and Debtor views (re-authenticates with different userRole).
- **Creditor flow:** Register creditor → Create claim → View claims list → View claim detail → Update status → Cancel claim.
- **Debtor flow:** View claims by kennitala → View claim detail → Record payment → View payment history.
- **Claim detail page:** Show status badge, amount/paid/remaining, status history timeline, documents list, payment list. Allow actions based on current status.
- **Currency formatting:** Amounts are in minor units (e.g., 50000 = 50,000 ISK). ISK has 0 decimal places.
- **Status badges:** Color-coded by status (green=Paid, red=Overdue, yellow=PartiallyPaid, gray=Cancelled, etc.).
- **Responsive:** Should work on desktop. Mobile is nice-to-have.
- **Error handling:** Show API error messages from `{"Error":{"Code":"...","Message":"..."}}` responses.

## Running

To start the API locally before working on the demo:
```bash
# Terminal 1: Start Azurite (Azure Storage emulator)
azurite --silent --location api/.azurite

# Terminal 2: Start the API
cd api/Ez.Bank.FunctionsApi && func start
```

The demo app should proxy API requests to `http://localhost:7071` during development (configure in vite.config.ts).
