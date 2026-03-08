# Technical Design: Cross-Bank Claims System (Kröfupottur)

> Companion to [PRD-claims-system.md](./PRD-claims-system.md). This document covers architecture, infrastructure, implementation patterns, and technical decisions.

## 1. System Overview

Ez.Bank Claims is a centralized REST API that enables creditors to issue payment claims and debtors to view and pay them across participating Icelandic banks. A React demo application exercises the core flows.

### Technology Stack

| Layer | Technology | Version |
|---|---|---|
| Runtime | .NET | 8 LTS |
| API host | Azure Functions (Isolated Worker) | v4 |
| Data storage | Azure Table Storage | v12 SDK |
| Blob storage | Azure Blob Storage | v12 SDK |
| Authentication | JWT (HS256) | System.IdentityModel.Tokens.Jwt |
| Demo app | React + TypeScript | 18+ |
| Build tooling | Vite | 5+ |
| CI/CD | GitHub Actions | N/A |
| Infrastructure | Azure (Resource Group) | N/A |

### Architecture Style

Clean Architecture with three layers:

```
┌──────────────────────────────────────────────────┐
│  Ez.Bank.FunctionsApi   (HTTP triggers, DI)      │
│  ── thin controllers, request/response mapping ──│
├──────────────────────────────────────────────────┤
│  Ez.Bank.Claims         (Domain module)          │
│  ├─ Entities/           (domain models)          │
│  ├─ UseCases/           (interactors + ports)    │
│  └─ DataAccess/         (adapters)               │
├──────────────────────────────────────────────────┤
│  Ez.Bank.Core           (shared base classes)    │
└──────────────────────────────────────────────────┘
```

**Dependency rule:** FunctionsApi → Claims → Core. Domain logic in `UseCases/` depends only on interfaces (ports). Data access adapters implement those interfaces and are injected at startup.

---

## 2. Azure Infrastructure

### Resource Group Layout

```
rg-ezbank-{env}
├── func-ezbank-claims-{env}          # Azure Functions app
├── plan-ezbank-{env}                 # App Service Plan (Flex Consumption)
├── st{env}ezbank                     # Storage Account (tables + blobs + function state)
│   ├── Tables: Banks, Currencies, Creditors, Debtors, Claims,
│   │          ClaimsByCreditor, Sequences, Payments,
│   │          ClaimStatusHistory, ClaimDocuments, SystemConfig
│   ├── Container: claim-documents    # PDF uploads
│   └── Container: function-state     # Azure Functions internal
├── appi-ezbank-{env}                 # Application Insights
├── log-ezbank-{env}                  # Log Analytics Workspace
└── stapp-ezbank-demo-{env}           # Static Web App (React demo)
```

Where `{env}` is `dev`, `staging`, or `prod`.

### Environment Strategy

| Environment | Purpose | Storage Account | Functions plan |
|---|---|---|---|
| `dev` | Local + cloud development | Azurite (local emulator) | Local (func start) |
| `staging` | Integration testing, demo | Standard LRS | Flex Consumption |
| `prod` | Production | Standard GRS | Flex Consumption (always-ready: 1) |

### Networking

- **Dev/Staging:** Public endpoints with IP restrictions (dev team IPs + GitHub Actions runners).
- **Prod:** VNet integration for the Functions app. Storage account behind service endpoint.

---

## 3. Project Structure

```
Ez.Bank/
├── api/
│   ├── Ez.Bank.sln
│   ├── Ez.Bank.Core/
│   │   ├── Entities/
│   │   │   └── EntityBase.cs               # ID (GUID string), audit fields, soft delete
│   │   ├── Exceptions/
│   │   │   ├── BusinessRuleException.cs    # 400-level errors
│   │   │   ├── ConflictException.cs        # 409 errors
│   │   │   └── NotFoundException.cs        # 404 errors
│   │   ├── Models/
│   │   │   └── PagedResult.cs              # Continuation-token pagination
│   │   ├── CallerContext.cs                 # record(UserId, BankId, UserRole)
│   │   └── Ez.Bank.Core.csproj
│   ├── Ez.Bank.Claims/
│   │   ├── Entities/
│   │   │   ├── Claim.cs
│   │   │   ├── ClaimStatus.cs              # Enum (Created..Disputed)
│   │   │   ├── Payment.cs
│   │   │   ├── Creditor.cs
│   │   │   ├── Debtor.cs
│   │   │   ├── Bank.cs
│   │   │   ├── Currency.cs
│   │   │   ├── ClaimDocument.cs
│   │   │   ├── ClaimStatusHistory.cs
│   │   │   └── SystemConfig.cs
│   │   ├── UseCases/
│   │   │   ├── Ports/                      # Interfaces (data access contracts)
│   │   │   │   ├── IClaimDataAccess.cs
│   │   │   │   ├── IPaymentDataAccess.cs
│   │   │   │   ├── ICreditorDataAccess.cs
│   │   │   │   ├── IDebtorDataAccess.cs
│   │   │   │   ├── IBankDataAccess.cs
│   │   │   │   ├── ICurrencyDataAccess.cs
│   │   │   │   ├── IClaimDocumentDataAccess.cs
│   │   │   │   ├── IDocumentStorage.cs
│   │   │   │   └── ISystemConfigDataAccess.cs
│   │   │   ├── ClaimInteractor.cs
│   │   │   ├── PaymentInteractor.cs
│   │   │   ├── DocumentInteractor.cs
│   │   │   └── DueCostInteractor.cs
│   │   ├── DataAccess/
│   │   │   ├── TableAccessBase.cs          # Shared base with TableClient
│   │   │   ├── ClaimTableAccess.cs         # Dual-write to Claims + ClaimsByCreditor
│   │   │   ├── PaymentTableAccess.cs
│   │   │   ├── CreditorTableAccess.cs
│   │   │   ├── DebtorTableAccess.cs
│   │   │   ├── BankTableAccess.cs
│   │   │   ├── CurrencyTableAccess.cs
│   │   │   ├── ClaimDocumentTableAccess.cs
│   │   │   ├── SystemConfigTableAccess.cs
│   │   │   ├── BlobDocumentStorage.cs
│   │   │   ├── LocalDocumentStorage.cs
│   │   │   └── DataSeeder.cs              # Idempotent seed for currencies + config
│   │   └── Ez.Bank.Claims.csproj
│   ├── Ez.Bank.FunctionsApi/
│   │   ├── Auth/
│   │   │   ├── AuthFunctions.cs            # POST /api/auth/token
│   │   │   ├── JwtTokenService.cs          # HS256 token generation/validation
│   │   │   ├── IJwtTokenService.cs
│   │   │   └── AuthSettings.cs
│   │   ├── Claims/
│   │   │   ├── ClaimFunctions.cs
│   │   │   ├── PaymentFunctions.cs
│   │   │   ├── BankFunctions.cs
│   │   │   ├── CreditorFunctions.cs
│   │   │   ├── DebtorFunctions.cs
│   │   │   ├── DocumentFunctions.cs
│   │   │   └── DueCostFunctions.cs
│   │   ├── Scheduling/
│   │   │   └── OverdueProcessingFunction.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   └── AuthenticationMiddleware.cs
│   │   ├── Extensions/
│   │   │   └── FunctionContextExtensions.cs
│   │   ├── Models/
│   │   │   ├── Requests/                   # API request DTOs
│   │   │   └── Responses/                  # API response DTOs
│   │   ├── Program.cs
│   │   ├── HealthFunctions.cs
│   │   ├── host.json
│   │   ├── local.settings.json             # Local dev only (gitignored)
│   │   └── Ez.Bank.FunctionsApi.csproj
│   └── Ez.Bank.UnitTests/
│       ├── Claims/
│       │   ├── ClaimInteractorTests.cs
│       │   ├── PaymentInteractorTests.cs
│       │   ├── DocumentInteractorTests.cs
│       │   └── DueCostInteractorTests.cs
│       └── Ez.Bank.UnitTests.csproj
├── demo/
│   └── ez-bank-demo/
│       └── ...
├── .github/
│   └── workflows/
│       ├── api-ci.yml
│       └── demo-ci.yml
└── docs/
    ├── PRD-claims-system.md
    └── technical-design.md
```

---

## 4. Data Storage Design

### Engine

Azure Table Storage via `Azure.Data.Tables` SDK. All IDs are GUID strings. Each entity type gets its own Azure Table.

### Table Schema

| Table | PartitionKey | RowKey | Primary Query |
|---|---|---|---|
| `Banks` | `"BANK"` | `{Id}` | All banks (small set) |
| `Currencies` | `"CURRENCY"` | `{Code}` | By ISO code |
| `Creditors` | `{BankId}` | `{Id}` | By bank |
| `Debtors` | `"DEBTOR"` | `{Kennitala}` | By kennitala (most common lookup) |
| `Claims` | `{DebtorKennitala}` | `{Id}` | Claims for debtor |
| `ClaimsByCreditor` | `{CreditorId}` | `{ClaimId}` | Claims for creditor (secondary index) |
| `Sequences` | `"SEQUENCE"` | `"ClaimReference"` | Atomic counter for claim references |
| `Payments` | `{ClaimId}` | `{Id}` | Payments for claim |
| `ClaimStatusHistory` | `{ClaimId}` | `{Timestamp}_{Id}` | History for claim (chronological) |
| `ClaimDocuments` | `{ClaimId}` | `{Id}` | Documents for claim |
| `SystemConfig` | `"CONFIG"` | `{Key}` | Config by key |

### Key Design Decisions

- **DebtorKennitala as Claim PartitionKey** — highest-traffic query is "claims by debtor".
- **ClaimsByCreditor** — denormalized secondary index, dual-written on claim create/update.
- **Sequence counter** — ETag-based optimistic increment for claim reference generation.
- **Concurrency** — Table Storage ETags for optimistic concurrency on payment recording.
- **Continuation tokens** — replace page-number pagination (Table Storage limitation).

### Entity Model Notes

- All IDs are `string` (GUIDs), not auto-increment integers.
- `CurrencyCode` (string, e.g. "ISK") replaces `CurrencyID` (int FK).
- `InterestRate` is `double` (Table Storage doesn't support decimal).
- SQL `RowVersion` is replaced by Table Storage ETags at the data access layer.
- `InsertedBy`/`UpdatedBy` are `string` (JWT user IDs).

### Seed Data

Seeded idempotently by `DataSeeder.cs` at application startup:

**Currencies:**
- ISK (Icelandic króna, 0 decimals)
- EUR (Euro, 2 decimals)
- USD (US Dollar, 2 decimals)
- GBP (British Pound, 2 decimals)
- DKK (Danish krone, 2 decimals)
- SEK (Swedish krona, 2 decimals)
- NOK (Norwegian krone, 2 decimals)

**System Config:**
- `DefaultLateFee` = `950`
- `DefaultInterestRate` = `12.00`

### Concurrency Strategy

Payments use optimistic concurrency via Table Storage ETags:

```
1. Read Claim entity (receive ETag)
2. Validate: status allows payments, amount <= remaining, currency matches
3. Update Claim (PaidAmount, Status) with expected ETag
   → If ETag mismatch: return 409 (concurrent modification)
4. Insert Payment entity
5. Insert ClaimStatusHistory (if status changed)
```

Currently the interactors use `"*"` as the ETag (accept any version) for simplicity. This can be tightened to use actual ETags for stricter concurrency control in production.

---

## 5. Authentication

### JWT Token Flow

Authentication uses simple JWT tokens (HS256) issued by a built-in token endpoint.

#### Token Endpoint: `POST /api/auth/token`

Request:
```json
{
  "password": "...",
  "bankId": "1",
  "userId": "42",
  "userRole": "Creditor"
}
```

- Validates password against `AUTH_PASSWORD` env var (constant-time comparison via `CryptographicOperations.FixedTimeEquals`).
- Validates `userRole` is one of `Creditor`, `Debtor`, `Bank`.
- Returns JWT (HS256, signed with `JWT_SECRET` env var, configurable expiry).

JWT claims: `sub` (userId), `bankId`, `role` (userRole), `iss` ("ez-bank-claims"), `exp`, `iat`.

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-03-07T01:00:00Z"
}
```

#### AuthenticationMiddleware

- Skips `/api/auth/token` and `/api/health`.
- Extracts `Bearer` token from `Authorization` header.
- Validates signature + expiry via `IJwtTokenService`.
- Attaches `CallerContext(UserId, BankId, UserRole)` to `FunctionContext.Items`.
- Returns 401 on failure.

---

## 6. API Design

### Host Configuration

Azure Functions Isolated Worker model (.NET 8). All functions are HTTP-triggered except the overdue processing timer.

**host.json:**

```json
{
  "version": "2.0",
  "extensions": {
    "http": {
      "routePrefix": "api"
    }
  }
}
```

### Dependency Injection (Program.cs)

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(worker =>
    {
        worker.UseMiddleware<ExceptionHandlingMiddleware>();
        worker.UseMiddleware<AuthenticationMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        // Azure Table Storage
        services.AddSingleton(new TableServiceClient(
            config["AZURE_STORAGE_CONNECTION"]));

        // JWT Auth
        services.AddSingleton<IJwtTokenService>(new JwtTokenService(new AuthSettings
        {
            Password = config["AUTH_PASSWORD"]!,
            JwtSecret = config["JWT_SECRET"]!,
            JwtExpiryMinutes = int.Parse(config["JWT_EXPIRY_MINUTES"] ?? "60")
        }));

        // Data access (Singleton — TableClient is thread-safe)
        services.AddSingleton<IClaimDataAccess, ClaimTableAccess>();
        services.AddSingleton<IPaymentDataAccess, PaymentTableAccess>();
        services.AddSingleton<ICreditorDataAccess, CreditorTableAccess>();
        services.AddSingleton<IDebtorDataAccess, DebtorTableAccess>();
        services.AddSingleton<IBankDataAccess, BankTableAccess>();
        services.AddSingleton<ICurrencyDataAccess, CurrencyTableAccess>();
        services.AddSingleton<IClaimDocumentDataAccess, ClaimDocumentTableAccess>();
        services.AddSingleton<ISystemConfigDataAccess, SystemConfigTableAccess>();

        // Document storage
        if (config.GetValue<bool>("USE_LOCAL_STORAGE"))
            services.AddSingleton<IDocumentStorage, LocalDocumentStorage>();
        else
            services.AddSingleton<IDocumentStorage, BlobDocumentStorage>();

        // Interactors
        services.AddScoped<ClaimInteractor>();
        services.AddScoped<PaymentInteractor>();
        services.AddScoped<DocumentInteractor>();
        services.AddScoped<DueCostInteractor>();

        // Reference data cache
        services.AddMemoryCache();
    })
    .Build();

// Seed reference data
await DataSeeder.SeedAsync(host.Services);

host.Run();
```

### Middleware

#### ExceptionHandlingMiddleware

Maps domain exceptions to HTTP responses:

| Exception | HTTP Status |
|---|---|
| `BusinessRuleException` | 400 Bad Request |
| `NotFoundException` | 404 Not Found |
| `ConflictException` | 409 Conflict |
| `UnauthorizedAccessException` | 403 Forbidden |
| Unhandled | 500 Internal Server Error (log full details, return generic message) |

### Request/Response DTOs

Functions map between API DTOs and domain entities. Domain entities never leak into HTTP responses directly.

```
Request DTO → Interactor (domain entities) → Response DTO
```

### Pagination

Table Storage uses continuation tokens instead of page numbers.

**Request:** `?pageSize=50&continuationToken={base64}`

**Response:**
```json
{
  "items": [...],
  "pageSize": 50,
  "continuationToken": "...",
  "hasMore": true
}
```

### Error Response Format

All error responses use a consistent shape:

```json
{
  "error": {
    "code": "BAD_REQUEST",
    "message": "Payment of 20000 exceeds remaining balance of 10000."
  }
}
```

### Environment Variables

| Variable | Required | Default | Purpose |
|---|---|---|---|
| `AUTH_PASSWORD` | Yes | — | Password for auth endpoint |
| `JWT_SECRET` | Yes | — | HMAC-SHA256 signing key (≥32 chars) |
| `JWT_EXPIRY_MINUTES` | No | `60` | Token lifetime |
| `AZURE_STORAGE_CONNECTION` | Yes | — | Table + Blob connection string |
| `USE_LOCAL_STORAGE` | No | `false` | Use local FS for documents |
| `LOCAL_STORAGE_PATH` | No | `./local-storage/claim-documents` | Local FS path |
| `BLOB_CONTAINER_NAME` | No | `claim-documents` | Blob container name |

---

## 7. Claim Lifecycle

### State Machine

```
                  ┌──────────┐
                  │ Created  │
                  └────┬─────┘
                       │
                  ┌────▼─────┐
         ┌────── │   Sent   │ ◄──────────────────────┐
         │       └────┬─────┘                         │
         │            │                               │
         │       ┌────▼─────┐                    (re-open)
         │       │  Viewed  │                         │
         │       └──┬───┬───┘                    ┌────┴──────┐
         │          │   │                        │  Disputed │
         │          │   │    ┌───────────────┐   └────┬──────┘
         │          │   └───►│ PartiallyPaid ├───────►│
         │          │        └───┬───────────┘        │
         │          │            │                    │
         │          │       ┌────▼─────┐              │
         │          └──────►│   Paid   │              │
         │                  └──────────┘              │
         │                                            │
         │       ┌───────────┐                        │
         │       │  Overdue  │◄── (daily timer)       │
         │       └─────┬─────┘                        │
         │             │ (can transition to            │
         │             │  Paid/PartiallyPaid/          │
         │             │  Cancelled/Disputed)          │
         │             │                              │
         ▼             ▼                              ▼
    ┌──────────────────────────────────────────────────┐
    │                  Cancelled                        │
    └──────────────────────────────────────────────────┘
```

### Transition Matrix

| From \ To | Created | Sent | Viewed | PartiallyPaid | Paid | Overdue | Cancelled | Disputed |
|---|---|---|---|---|---|---|---|---|
| **Created** | - | yes | - | - | - | - | yes | - |
| **Sent** | - | - | yes | - | - | yes | yes | - |
| **Viewed** | - | - | - | yes | yes | yes | yes | yes |
| **PartiallyPaid** | - | - | - | - | yes | yes | yes | yes |
| **Overdue** | - | - | - | yes | yes | - | yes | yes |
| **Disputed** | - | yes | - | - | - | - | yes | - |
| **Paid** | - | - | - | - | - | - | - | - |
| **Cancelled** | - | - | - | - | - | - | - | - |

`Paid` and `Cancelled` are terminal states.

### Payment Completeness Rule

A claim is considered fully paid when:

- **Before due costs are calculated:** `PaidAmount >= Amount`
- **After due costs are calculated:** `PaidAmount >= TotalDue` where `TotalDue = Amount + LateFee + AccruedInterest`

The interactor checks: if `LateFee > 0` or `AccruedInterest > 0`, compare against `TotalDue`. Otherwise compare against `Amount`. This keeps the simple case simple and the overdue case correct.

---

## 8. Data Access Layer

### Approach

Azure Table Storage via `Azure.Data.Tables` SDK. No ORM. Each data access class inherits from `TableAccessBase` which provides shared `TableClient` access and helper methods.

### Connection Management

All data access implementations receive `TableServiceClient` via constructor injection (Singleton). `TableClient` instances are thread-safe and reused across requests.

### Data Access Interface Pattern

```csharp
public interface IClaimDataAccess
{
    Task<Claim?> GetByIdAsync(string id);
    Task<PagedResult<Claim>> GetByDebtorAsync(string debtorKennitala, int pageSize, string? continuationToken);
    Task<PagedResult<Claim>> GetByCreditorAsync(string creditorId, ClaimStatus? status, int pageSize, string? continuationToken);
    Task InsertAsync(Claim claim);
    Task<bool> UpdateAsync(Claim claim, string expectedETag);
    Task<string> GetNextClaimReferenceAsync();
    Task InsertStatusHistoryAsync(ClaimStatusHistory history);
    Task<List<ClaimStatusHistory>> GetHistoryAsync(string claimId);
    Task<List<Claim>> GetOverdueClaimsAsync(int batchSize);
}
```

The `UpdateAsync` method returns `false` when the ETag doesn't match, signaling a concurrent modification to the interactor.

### Dual-Write Pattern (Claims)

When a claim is created or updated, the `ClaimTableAccess` writes to both:
1. `Claims` table (PK = DebtorKennitala, RK = ClaimId) — for debtor queries
2. `ClaimsByCreditor` table (PK = CreditorId, RK = ClaimId) — for creditor queries

This denormalization is necessary because Table Storage doesn't support secondary indexes.

### Sequence Counter (Claim References)

Claim references follow the format `CLM-NNNNNN`. The `Sequences` table stores an atomic counter:

```
Table: Sequences
PK: "SEQUENCE"
RK: "ClaimReference"
Value: 42 (current counter)
```

Incrementing uses ETag-based optimistic concurrency with retry on conflict.

### Reference Data Caching

Currencies and system config are cached in-memory with a 5-minute TTL using `IMemoryCache`. These entries change extremely rarely and are read on every claim operation.

---

## 9. Document Storage

### Interface

```csharp
public interface IDocumentStorage
{
    Task<string> UploadAsync(string claimId, string fileName, Stream content);
    Task<Stream> DownloadAsync(string storagePath);
    Task DeleteAsync(string storagePath);
}
```

### Blob Storage Implementation

- Container: `claim-documents`
- Blob path: `claims/{claimId}/{guid}-{fileName}`
- Access tier: Hot (documents are frequently downloaded shortly after upload)
- No public access. Downloads are streamed through the API.

### Local Storage Implementation (Development)

- Base path: `./local-storage/claim-documents/`
- Same path structure as blob storage.
- Enabled via `USE_LOCAL_STORAGE=true`.

### Upload Validation

Performed in the `DocumentInteractor` before reaching storage:

1. Content type must be `application/pdf`.
2. Validate PDF magic bytes — first 5 bytes must be `%PDF-` (hex: `25 50 44 46 2D`).
3. File size <= 10 MB.
4. Claim has < 5 active (non-deleted) documents.
5. Claim status is not `Cancelled`.

---

## 10. Overdue Processing

### Timer Trigger

```csharp
[Function("OverdueProcessing")]
public async Task Run(
    [TimerTrigger("0 0 1 * * *")] TimerInfo timer)  // Daily at 01:00 UTC
```

### Logic

1. Query claims where `DueDate < UtcNow` and status is Sent, Viewed, or PartiallyPaid.
2. Process in batches of 500 to avoid function timeout.
3. For each claim: update status to `Overdue`, insert `ClaimStatusHistory` row.
4. Log batch size and duration.

### Idempotency

The query filters by status, so claims already marked `Overdue` are skipped. Safe to re-run.

---

## 11. Observability

### Application Insights

All Azure Functions telemetry (requests, dependencies, exceptions, traces) flows to Application Insights automatically via the worker SDK.

### Structured Logging

Use `ILogger<T>` with structured message templates:

```csharp
_logger.LogInformation(
    "Payment {PaymentId} recorded for claim {ClaimId}. Amount: {Amount}, NewPaidAmount: {PaidAmount}",
    payment.Id, claim.Id, payment.Amount, claim.PaidAmount);
```

### Health Check

`GET /api/health` verifies:
1. Table Storage connectivity (queries the Banks table).
2. Returns `200 OK` with component status or `503 Service Unavailable`.

---

## 12. Testing Strategy

### Unit Tests

- **Target:** >80% line coverage on all interactors.
- **Framework:** xUnit + Moq.
- **Scope:** Business logic in `ClaimInteractor`, `PaymentInteractor`, `DocumentInteractor`, `DueCostInteractor`.
- **Pattern:** Mock all `IDataAccess` ports. Assert business rule enforcement, status transitions, error cases.

Key test cases for `PaymentInteractor`:

| Test | Assertion |
|---|---|
| Payment within balance | Status transitions correctly, PaidAmount updated |
| Payment completes balance | Status → Paid |
| Overpayment | Throws `BusinessRuleException` |
| Concurrent payment conflict | Returns false from UpdateAsync, throws `ConflictException` |
| Payment on cancelled claim | Throws `ConflictException` |
| Currency mismatch | Throws `BusinessRuleException` |

Key test cases for `DueCostInteractor`:

| Test | Assertion |
|---|---|
| Calculate on overdue claim | LateFee + AccruedInterest computed correctly |
| Late fee not re-applied | LateFee stays at original value on second calculation |
| Interest formula accuracy | `floor(Amount * Rate/100 * Days/365)` matches expected |
| Calculate on paid claim | Throws `BusinessRuleException` |

### Integration Tests (future)

Not in v1 scope. When added, use Azurite for local Table/Blob storage emulation with test data cleaned up after each run.

---

## 13. Demo Application (React)

### Architecture

```
demo/ez-bank-demo/
├── src/
│   ├── api/
│   │   └── client.ts           # Fetch wrapper with base URL, JWT auth
│   ├── components/
│   │   ├── ClaimStatusBadge.tsx
│   │   ├── CurrencyAmount.tsx   # Formats amount based on DecimalPlaces
│   │   ├── Pagination.tsx
│   │   └── ErrorAlert.tsx
│   ├── pages/
│   │   ├── Dashboard.tsx
│   │   ├── CreateClaim.tsx
│   │   ├── ClaimsList.tsx
│   │   └── ClaimDetail.tsx
│   ├── types/
│   │   └── index.ts            # Shared TypeScript interfaces matching API DTOs
│   ├── App.tsx                  # Router setup
│   └── main.tsx
├── .env                        # VITE_API_BASE_URL=http://localhost:7071/api
└── package.json
```

### API Client

A thin wrapper around `fetch` that:

- Prepends `VITE_API_BASE_URL` to all paths.
- Sets `Authorization: Bearer {token}` header from stored JWT.
- Throws on non-2xx responses with parsed error body.
- Handles multipart/form-data for document uploads.

### Deployment

Azure Static Web Apps. Build output from Vite (`dist/`) deployed via GitHub Actions.

---

## 14. CI/CD

### API Pipeline (`api-ci.yml`)

```
trigger: push to main, paths: api/**

1. dotnet restore
2. dotnet build --configuration Release
3. dotnet test --configuration Release --collect:"XPlat Code Coverage"
4. Check coverage threshold (≥80% on interactors)
5. dotnet publish Ez.Bank.FunctionsApi
6. Deploy to Azure Functions (staging slot)
7. Run smoke tests against staging
8. Swap staging → production
```

### Demo Pipeline (`demo-ci.yml`)

```
trigger: push to main, paths: demo/**

1. npm ci
2. npm run lint
3. npm run build
4. Deploy to Azure Static Web Apps
```

---

## 15. Security Considerations

### Authentication

JWT tokens (HS256) issued by `POST /api/auth/token`. Password validated against `AUTH_PASSWORD` environment variable using constant-time comparison.

| Phase | Mechanism |
|---|---|
| v1 (current) | JWT via built-in token endpoint (shared password per environment) |
| v1.1 | JWT via Azure AD B2C or per-bank client credentials |
| v2 | Mutual TLS for bank-to-bank communication |

### Authorization Rules

Enforced in interactors, not in functions. Each interactor method receives the `CallerContext` (userId, bankId, userRole) and validates:

| Action | Allowed roles |
|---|---|
| Create claim | Creditor |
| View claim | Creditor (own claims), Debtor (own claims), Bank (affiliated claims) |
| Record payment | Debtor's bank |
| Cancel claim | Creditor, Creditor's bank |
| Dispute claim | Debtor, Debtor's bank |
| Upload document | Creditor, Debtor |
| Delete document | Uploader only |

### Data Protection

- Kennitala is PII. In v1, it appears in query strings. The production recommendation is to move debtor search to `POST /api/claims/search` with a JSON body to keep PII out of URL logs.
- Azure Table Storage: Server-side encryption enabled by default.
- Azure Blob Storage: Server-side encryption enabled by default.

### Input Validation

All input validation happens in the interactor layer:

- Amount > 0, fits in `long`.
- DueDate >= tomorrow.
- Currency code exists in the Currencies table.
- Kennitala is exactly 10 digits (`^\d{10}$`).
- CategoryCode is one of: `UTILITY`, `TELECOM`, `LOAN`, `OTHER`.
- Description length <= 500 characters.
- File content type is `application/pdf` and starts with `%PDF-` magic bytes.
- File size <= 10 MB.

---

## 16. Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Table Storage over SQL | Azure Table Storage | Simpler, cheaper for v1. No schema migrations. Scales automatically. Can migrate to SQL later if needed. |
| JWT over Azure AD | Simple JWT with shared password | Minimal infrastructure for v1. Allows full auth flow testing without external identity provider. |
| Env vars over Key Vault | Environment variables | Simpler local development. Key Vault can be added later by referencing env vars from Key Vault secrets. |
| GUID string IDs over auto-increment | GUID strings | Required by Table Storage (no auto-increment). Globally unique without coordination. |
| Continuation tokens over page numbers | Continuation tokens | Required by Table Storage. More efficient than OFFSET-based pagination. |
| Dual-write for creditor index | ClaimsByCreditor table | Table Storage has no secondary indexes. Denormalization is the standard pattern. |
| ETag-based sequence counter | Optimistic retry loop | Generates claim references without distributed locks. Low contention expected. |
| Optimistic concurrency via ETags | Table Storage ETags | Natural fit for Table Storage. Avoids pessimistic locks. |
| Azure Functions over App Service | Functions (Isolated) | Per-request billing, auto-scaling, timer triggers for overdue processing. |
| `IDocumentStorage` abstraction | Interface with two implementations | Allows local development without Azure Blob dependencies. Single toggle in config. |
| No message queue in v1 | Direct Table Storage writes | Simpler to build and debug. Queue-based event publishing can be added in v2. |

---

## 17. Known Limitations (v1)

1. **Simplified authentication.** Shared password for all users. Do not use in production without per-user credentials.
2. **No rate limiting.** The API is open to abuse. Add Azure API Management or middleware-based throttling before production.
3. **Single-region deployment.** No geo-redundancy. Acceptable for Iceland-only traffic in v1.
4. **No bulk operations.** Banks needing to create or reconcile claims in bulk must call endpoints one at a time.
5. **No webhook/event notifications.** Bank B has no way to know about new claims except polling.
6. **Overdue processing runs once daily.** A claim that passes its due date at 02:00 won't be marked overdue until 01:00 the next day (up to ~23-hour delay).
7. **Payment idempotency not enforced.** Duplicate `POST /api/claims/{id}/payments` calls with the same `paymentReference` will create duplicate payment records.
8. **Relaxed concurrency.** Interactors currently use `"*"` ETags (accept any version). Tighten to actual ETags for production use.
9. **No Table Storage transactions.** Dual-writes to Claims + ClaimsByCreditor are not atomic. A failure between writes could leave inconsistent state.
10. **No cross-partition queries.** Looking up a claim by ID requires scanning all partitions unless the debtor kennitala is known.
