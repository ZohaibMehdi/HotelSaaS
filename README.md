# HotelSaaS — Multi-Tenant Hotel & Guest House Management SaaS

A production-oriented, multi-tenant SaaS platform for hotels, guest houses and hostels,
implemented from the PRD (`Hotel_Guest_House_SaaS_PRD.md`). Database-first design,
API-first backend, React SPA frontend.

## Stack

| Layer     | Technology                                                        |
|-----------|-------------------------------------------------------------------|
| Database  | Microsoft SQL Server 2022 (`HotelSaaS_DB`) — schema-first scripts |
| Backend   | ASP.NET Core 9 Web API · EF Core (explicit mappings) · JWT + RBAC |
| Frontend  | React 18 + TypeScript + Vite + Tailwind CSS + React Router        |
| Tests     | xUnit unit tests + scripted end-to-end acceptance suite           |

```
database/01_schema.sql      # Source of truth: 28 tables, keys, constraints, indexes
database/02_seed.sql        # Roles, demo tenants/users/bookings (idempotent)
backend/
  src/HotelSaaS.Domain          # Entities + enums (no dependencies)
  src/HotelSaaS.Application     # DTOs, FluentValidation, services, business rules
  src/HotelSaaS.Infrastructure  # DbContext (DB-first mappings), JWT, seeder, DI
  src/HotelSaaS.Api             # Controllers, middleware, Swagger, rate limiting
  tests/HotelSaaS.Tests         # Unit tests (pricing, hashing, agreement rules…)
frontend/                   # React SPA (role-aware UI for every module)
scripts/                    # Helper PowerShell scripts
scripts/test-api.ps1        # 34-assertion E2E suite incl. tenant-isolation checks
```

## Quick start

Prerequisites: .NET 9 SDK, Node 20+, SQL Server with Windows auth (localhost).

```powershell
# 1) Database (creates HotelSaaS_DB + schema + seed; safe to re-run)
sqlcmd -S localhost -E -i database\01_schema.sql
sqlcmd -S localhost -E -i database\02_seed.sql

# 2) Backend  → http://localhost:5080  (Swagger at /swagger)
cd backend\src\HotelSaaS.Api ; dotnet run

# 3) Frontend → http://localhost:5173  (proxies /api → :5080)
cd frontend ; npm install ; npm run dev
```

Or use the helpers:

```powershell
powershell -File scripts\setup-database.ps1   # reset/recreate the database
powershell -File scripts\run-backend.ps1
powershell -File scripts\run-frontend.ps1
```

> Node note: Vite warns below Node 20.19 — it still works on 20.17; upgrade if convenient.

### Demo accounts (seeded password `Admin@123`)

| Role            | Email                        |
|-----------------|------------------------------|
| SuperAdmin      | superadmin@hotelsaas.com     |
| TenantAdmin A   | admin@saibaan.com            |
| Manager A       | manager@saibaan.com          |
| FrontDesk A     | frontdesk@saibaan.com        |
| Finance A       | finance@saibaan.com          |
| HR A            | hr@saibaan.com               |
| Housekeeping A  | housekeeping@saibaan.com     |
| TenantAdmin B   | admin@hotelabc.com           |

Seeded tenants **Saibaan Guest House** and **Hotel ABC** ship with room types, rooms,
guests, an ABC Corporation B2B agreement (contracted rates), employees, and live demo
activity (in-house stay, upcoming bookings, a fully-paid invoice) so dashboards are
populated immediately.

## Modules implemented (PRD §9)

Platform admin (tenants, onboarding, platform users, system dashboard) · Company info ·
Property settings · Room types · Rooms (+ housekeeping status lifecycle) · Guests ·
Availability · Bookings (multi-room capable) · Check-in / walk-in / Check-out · Folio
charges (food, laundry…) · Invoices (+ printable view) · Payments (overpay-guarded) ·
Corporate customers · B2B agreements with contracted rates auto-applied · Employees &
departments · Payroll runs (immutable once finalized) · Housekeeping task board ·
Tenant & platform dashboards · Reports (occupancy, revenue, receivables, sources,
guests, payroll) · In-app notifications · Append-only audit log.

## Key engineering guarantees

* **Tenant isolation** — identity comes from JWT claims only; every tenant-owned query
  is scoped by claim tenant; cross-tenant access returns 404/403 (verified by E2E).
* **No double booking** — availability is re-checked inside a SERIALIZABLE transaction
  immediately before commit; booking/invoice numbers come from atomic counter updates.
* **Financial integrity** — invoices/payments are never deleted, only reversed/cancelled;
  overpayments rejected unless explicitly allowed; deposit transfers become traceable
  payments at checkout.
* **B2B correctness** — contracted rates apply automatically only while the agreement is
  Active and covers the stay dates.
* **Security** — PBKDF2-SHA256 password hashing, lockout after repeated failures,
  refresh-token rotation, role-based authorization on every endpoint, consistent error
  envelope, no secrets in logs.

### Deliberate assumptions / deviations (documented per PRD §71 Rule 1)

* Food bills are modelled as folio charges of type *Food* plus the `vw_FoodBills` view.
* Email/SMS providers are interface-backed log sinks in v1 (swap implementations later).
* Password reset tokens are returned in API responses **only in Development**
  (no SMTP provider is wired yet).
* Retry-on-failure execution strategy is intentionally disabled because serializable
  booking transactions must not replay silently.
* Seeded users receive the default password via first-run seeder — change after deploy.

## Tests

```powershell
dotnet test backend\HotelSaaS.sln                 # unit tests (pricing/hashing/rules)
# with API running:
powershell -ExecutionPolicy Bypass -File scripts\test-api.ps1   # 34 E2E assertions
```

The E2E suite covers PRD §60 acceptance criteria: cross-tenant read/write denial,
overlap rejection (409), check-in/out transitions, invoice totals, payment states,
contracted-rate application, suspended-agreement blocking, dashboards, reports, RBAC.

## Production notes

Set strong `Jwt:SecretKey` via environment/secrets, restrict CORS origins, serve the
SPA behind TLS, change all seeded passwords, and point `ConnectionStrings` at a proper
SQL instance with backups. Audit table writes should additionally be protected at the
SQL role level in hardened deployments.
