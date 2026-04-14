## 📸 Screenshots
<img width="1833" height="637" alt="6" src="https://github.com/user-attachments/assets/7ceab250-0851-495b-a1f3-e5dcd74a7329" />
<img width="1820" height="872" alt="5" src="https://github.com/user-attachments/assets/4e9241e0-a8b7-4326-91a3-e8adb16864f1" />
<img width="1806" height="746" alt="3" src="https://github.com/user-attachments/assets/179e3bc9-3fd8-4bfb-99de-7eb4cf8d2614" />
<img width="1817" height="682" alt="2" src="https://github.com/user-attachments/assets/0028c714-03a2-428a-b0e5-430d19ff57a6" />
<img width="1445" height="726" alt="1" src="https://github.com/user-attachments/assets/a874b239-96e9-4ea1-9ce9-1330c952e317" />


## Introduction

LedgerFlow is a **financial document management and recovery API**. It is aimed at teams or systems that need to issue and track money-related documents (for example sales and purchase invoices) in a way that mirrors common **ERP and accounting practice**: you treat financial records as **immutable documents** rather than rows you edit in place.

**What it is for**

- Creating and listing **sales** and **purchase** invoices (and related totals at header and line level).
- When something is wrong, **recovering** using a controlled process: you do not silently change a posted invoice; you **reverse** it with a linked **credit memo**, then optionally **reprocess** by creating a new corrected invoice that keeps history in the chain.
- **Governance**: JWT-based auth with roles (**Admin**, **Accountant**, **User**), reversal flows that require approval before execution, and an **audit trail** plus **outbox-style events** so you can see who did what and integrate externally over time.

**How it works (high level)**

1. **Documents** – Clients create invoices through the API. A **validation engine** checks required fields, line math, taxes, and business rules; documents get **automatic numbering** and a lifecycle status (for example Open, Closed, Cancelled, Reversed).
2. **Reversals** – Only privileged roles start a reversal. The API creates a **reversal approval** (Pending → Approved → **Executed**). On execute, the system creates a **credit memo** linked to the original, marks the original as **Reversed**, and records audit and domain events.
3. **Reprocessing** – After a reversal, you can submit **corrected** data; the API creates a **new** invoice linked to the prior document so originals stay untouched.
4. **Reporting and exports** – Endpoints expose reversed and active activity, summaries, and **CSV** export for analysis.
5. **Reliability** – **Idempotent** `POST` handling (via `Idempotency-Key`), soft deletes on documents, **optimistic concurrency** on updates where applicable, **Serilog** request logging, and **Hangfire** jobs to process queued outbox events (logging plus optional webhook URL).

**Stack**

Built with **ASP.NET Core**, **Clean Architecture**, **EF Core (SQL Server)**, **JWT + ASP.NET Core Identity**, **Hangfire**, and **Serilog**.

## Run locally

1. Ensure SQL Server is available (default: LocalDB connection string in `LedgerFlow.Api/appsettings.json`).
2. From the repo root:

```powershell
dotnet run --project LedgerFlow.Api
```

3. Open Swagger (`/swagger`). Migrations and demo users are applied on startup.

### Seeded users

| Email | Password | Roles |
|-------|----------|-------|
| admin@ledgerflow.local | Admin123! | Admin |
| accountant@ledgerflow.local | Accountant123! | Accountant |
| user@ledgerflow.local | User123! | User |

## Docker

```powershell
docker compose up --build
```

API: `http://localhost:8080/swagger`  
SQL: `localhost,1433` (sa / `Your_password123` from `docker-compose.yml`)

Override JWT signing key and connection string via environment variables in `docker-compose.yml`.

## Solution layout

- `LedgerFlow.Domain` – entities and enums  
- `LedgerFlow.Application` – use cases, validation, ports  
- `LedgerFlow.Infrastructure` – EF Core, Identity, JWT, Hangfire, idempotency store  
- `LedgerFlow.Api` – HTTP API, middleware, Swagger  
- `tests/LedgerFlow.UnitTests` – unit tests  
- `tests/LedgerFlow.IntegrationTests` – smoke tests  

## Tests

```powershell
dotnet test LedgerFlow.slnx
```
