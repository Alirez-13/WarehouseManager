# Warehouse Manager

A **multi-tenant warehouse inventory management REST API** built with **ASP.NET Core (.NET 10)** and **Entity Framework Core 10**, designed around a clean, layered architecture with a **database-per-tenant** persistence model, **optimistic concurrency control**, **atomic stock movement pipelines**, and **immutable audit snapshots** for financial-grade transaction history.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![EF Core](https://img.shields.io/badge/EF%20Core-10.0-512BD4)](https://learn.microsoft.com/en-us/ef/core/)
[![SQLite](https://img.shields.io/badge/SQLite-WAL%20mode-003B57)](https://www.sqlite.org/wal.html)
[![Tests](https://img.shields.io/badge/tests-xUnit%20%2B%20FluentAssertions-512BD4)]()

---

## Table of Contents

- [Overview](#overview)
- [Key Features](#key-features)
- [Architecture](#architecture)
- [Domain Model](#domain-model)
- [How It Works](#how-it-works)
- [API Reference](#api-reference)
- [Getting Started](#getting-started)
- [Running the Tests](#running-the-tests)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Roadmap](#roadmap)

---

## Overview

Warehouse Manager is a backend service that lets multiple independent businesses (tenants) track **products, stock levels, supplier receipts, and customer sales** through a single API — with strict physical data isolation between tenants.

Each tenant gets its **own SQLite database file**, automatically provisioned and migrated on first use. Every request is routed to the correct database via a simple `X-Tenant-ID` header — no connection strings in secrets, no manual setup.

The system treats inventory movements like a **ledger**: stock is only ever mutated inside explicit database transactions, every sale line snapshots the product's identity at write time, and concurrent stock updates are detected with **optimistic concurrency tokens** and surfaced to clients as proper `409 Conflict` responses.

## Key Features

| Feature | Description |
|---|---|
| **Multi-tenancy (DB-per-tenant)** | One isolated SQLite database per tenant, resolved per-request from the `X-Tenant-ID` header. New tenants are auto-provisioned with EF Core migrations on first access. |
| **Optimistic concurrency control** | `InventoryItem` rows carry a GUID `ConcurrencyVersion` token, regenerated on every write. Conflicting parallel updates throw `DbUpdateConcurrencyException`, mapped to **409 Conflict** via dedicated middleware. |
| **Atomic stock pipelines** | Inbound receipts and outbound sales run inside explicit EF Core transactions (`Begin/Commit/Rollback`). A failed sale (e.g. insufficient stock) rolls back **the entire operation** — no phantom invoices, no partially consumed stock. |
| **Immutable audit snapshots** | Transaction lines store `Sku`, product name, unit, and customer name/phone **as they existed at transaction time**, so financial history survives product renames, master-data purges, and customer edits. |
| **Concurrency-safe SQLite tuning** | Per-tenant databases run with `journal_mode = WAL` and `synchronous = NORMAL` pragmas, shared cache, foreign keys, and a busy timeout — enabling concurrent readers alongside writers. |
| **Customer auto-provisioning** | Sales deduplicate customers by phone number (natural key): existing customers are reused, new ones are created on the fly. |
| **Global exception → HTTP mapping** | Domain exceptions (`ConcurrencyConflictException`, `InsufficientStockException`) are translated to `409` / `422` by a single middleware at the top of the pipeline. |
| **Security hardening** | Tenant IDs from headers are sanitized against path traversal before being used in database file paths. |
| **Soft delete & auditing** | `IsActive` flags on products/customers plus `CreatedAtUtc` / `UpdatedAtUtc` audit columns on every entity via a shared `BaseEntity`. |
| **Integration test suite** | 8 end-to-end tests via `WebApplicationFactory` against **real SQLite databases** — covering tenant isolation, concurrency conflicts, rollback semantics, and customer search. |

## Architecture

The solution follows a **layered / Clean Architecture hybrid** with strict dependency inversion — the domain layer has **zero NuGet dependencies** and owns all abstractions:

```
                    ┌─────────────────────────┐
                    │      Warehouse.API       │  Presentation (ASP.NET Core)
                    │  Controllers · Middleware│  Tenant resolution · Swagger
                    └───────────┬─────────────┘
                        │       │
              depends on ▼       ▼ depends on
                    ┌─────────────────────────┐
                    │ Warehouse.Infrastructure │  EF Core · SQLite · Services
                    │ DbContext · Migrations   │  InventoryService, CustomerService
                    └───────────┬─────────────┘
                                │ depends on
                                ▼
                    ┌─────────────────────────┐
                    │      Warehouse.Core     │  Domain (pure C#, no deps)
                    │ Entities · Commands     │  IInventoryService, ITenantProvider
                    │ Interfaces · Exceptions │
                    └─────────────────────────┘

                    ┌─────────────────────────┐
                    │    Warehouse.Tests      │  xUnit integration tests
                    │ WebApplicationFactory   │
                    └─────────────────────────┘
```

- **`Warehouse.Core`** — the domain heart: entities, command records (CQRS-style immutable DTOs), service/tenant abstractions, and domain exceptions. Nothing depends outward.
- **`Warehouse.Infrastructure`** — EF Core `WarehouseDbContext`, fluent entity configurations, migrations, the multi-tenant `TenantDbContextFactory`, and implementations of the domain services.
- **`Warehouse.API`** — thin presentation layer: controllers, `HeaderTenantProvider` (implements a Core interface), the concurrency exception middleware, and DI/middleware composition.
- **`Warehouse.Tests`** — full-stack integration tests booting the real app in-process.

Key architectural decisions:

- **Dependency rule respected at project level** — Infrastructure and API both point *toward* Core; Core points nowhere.
- **No repository abstraction** — services consume `WarehouseDbContext` directly, with EF Core itself acting as the Unit of Work; transactions are managed explicitly where atomicity matters.
- **Per-request tenant resolution** — a scoped `WarehouseDbContext` is built from the tenant ID resolved by `ITenantProvider` (implemented by header middleware in the API layer).

## Domain Model

```
Product 1───1 InventoryItem          InboundTransaction 1───* InboundTransactionLine
 (Sku, Name, Unit,                    (ReferenceNumber,          (ProductId?, product
  IsActive/soft-delete)                SupplierName, ...)         snapshots, Qty, Price)

Customer 1───* OutboundTransaction    OutboundTransaction 1───* OutboundTransactionLine
 (Name, Phone = natural key,           (InvoiceNumber unique,      (same shape as inbound,
  IsActive/soft-delete)                 customer snapshots)         + qty/price/line total)
```

- Every entity inherits **`BaseEntity`** (`Id`, `CreatedAtUtc`, `UpdatedAtUtc`).
- `InventoryItem` holds `QuantityOnHand` `decimal(18,4)` and the GUID **`ConcurrencyVersion`** token.
- Money is `decimal(18,2)`, quantities are `decimal(18,4)` — precision is configured explicitly in EF Core mappings.
- Transaction lines keep **nullable** `ProductId` + snapshot columns, so audit history survives even if the master record is deleted.

## How It Works

### 1. Multi-tenant request flow

```
Client ──► X-Tenant-ID header ──► HeaderTenantProvider (API middleware)
                                        │
                                        ▼
                              TenantDbContextFactory (singleton)
                                        │  sanitizes tenant ID (path-traversal guard)
                                        ▼
                        EnsureInitialized (per-tenant, ConcurrentDictionary-guarded)
                                        │  first use? → apply EF migrations + WAL pragmas
                                        ▼
                        scoped WarehouseDbContext ──► tenant's own .db file
```

Every logical tenant reads and writes a **completely separate database** — enforced and proven by the `TenantIsolationTests`.

### 2. Atomic stock movement (e.g. outbound sale)

`InventoryService` executes every sale as a pipeline inside an explicit transaction:

1. **Begin transaction** on the tenant DbContext.
2. **Find-or-create the customer** by phone number (natural key dedup).
3. **Load & validate** each line's `InventoryItem` with row data.
4. **Verify sufficient stock** — otherwise throw `InsufficientStockException` → middleware returns **422** and the whole transaction **rolls back** (stock untouched, no invoice row created).
5. **Debit stock**, regenerate concurrency tokens (done automatically in `SaveChangesAsync`).
6. **Persist** the `OutboundTransaction` + lines with product/customer **snapshots**, compute line totals and invoice total.
7. **Commit.** If another request won the concurrency race, `DbUpdateConcurrencyException` is wrapped as `ConcurrencyConflictException` → middleware returns **409 Conflict**.

### 3. Concurrency token lifecycle

- `ConcurrencyItemConfiguration` marks `ConcurrencyVersion` with `IsConcurrencyToken()`.
- `WarehouseDbContext.SaveChangesAsync` regenerates the GUID (and stamps `LastStockUpdateUtc`) on every modified `InventoryItem`.
- Two parallel sales hitting the same stock row → exactly one succeeds, the loser gets a clean `409`, never a corrupted stock level. This is verified by a real parallel-request integration test.

## API Reference

Base URL (development): `http://localhost:5274` · Interactive docs: **`/swagger`**

All requests require the **`X-Tenant-ID`** header (any string, e.g. `acme-corp`). Missing header → `400`.

### `GET /api/customers/search?q={query}&limit=10`

Search customers by **name (infix match)** or **phone (prefix match)**, active only, ordered by name.

```http
GET /api/customers/search?q=robert&limit=10
X-Tenant-ID: acme-corp
```

```json
[
  { "id": 3, "name": "Zamen Ahoo", "phone": "+98999999999", "email": "yare@example.com", "address": "Mashhad, Imam Reza Street" }
]
```

### `POST /api/inbound/receipt`

Record a supplier receipt and increment stock. **202 Accepted** on success.

```http
POST /api/inbound/receipt
Content-Type: application/json
X-Tenant-ID: acme-corp
```

```json
{
  "referenceNumber": "PO-2026-0042",
  "supplierName": "Northwind Traders",
  "notes": "Morning delivery",
  "items": [
    { "productId": 1, "quantity": 50, "unitPrice": 12.50 }
  ]
}
```

Response: `"Inbound batch restocked successfully."`

### `POST /api/sales/outbound`

Record a customer sale, auto-provision the customer by phone, and decrement stock atomically. **202 Accepted** on success.

```http
POST /api/sales/outbound
Content-Type: application/json
X-Tenant-ID: acme-corp
```

```json
{
  "invoiceNumber": "INV-2026-0007",
  "customerName": "Robert Smith",
  "customerPhone": "+12065550142",
  "customerAddress": "Seattle, WA",
  "notes": null,
  "items": [
    { "productId": 1, "quantity": 20, "unitPrice": 19.99 }
  ]
}
```

Response: `"Outbound sale successfully recorded and stock adjusted."`

### Error responses (via `ConcurrencyExceptionMiddleware`)

| Status | Condition | Body |
|---|---|---|
| `400` | Missing/invalid `X-Tenant-ID` | — |
| `409 Conflict` | Concurrent stock modification lost the race | `{ "error": "..." }` |
| `422 Unprocessable Entity` | Insufficient stock (operation fully rolled back) | `{ "error": "..." }` |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- (Optional) [EF Core CLI](https://learn.microsoft.com/en-us/ef/core/miscellaneous/cli/dotnet) for migrations: `dotnet tool install --global dotnet-ef`

### 1. Restore & build

```bash
git clone https://github.com/Alirez-13/WarehouseManager.git
cd WarehouseManager
dotnet restore
dotnet build
```

### 2. Run the API

```bash
dotnet run --project Warehouse.API
```

The server starts on:

- HTTP: `http://localhost:5274`
- HTTPS: `https://localhost:7259`
- Swagger UI: `http://localhost:5274/swagger`

Tenant database files are created under the configured `TenantSettings:DatabaseDirectory` (`/data/warehouses` by default, falling back to `<app>/TenantData` in development) and are **migrated automatically on first use** — no manual migration step required.

### 3. Try it

```bash
# Search customers for tenant "acme-corp"
curl "http://localhost:5274/api/customers/search?q=robert" \
     -H "X-Tenant-ID: acme-corp"

# Restock 50 units of product 1
curl -X POST "http://localhost:5274/api/inbound/receipt" \
     -H "Content-Type: application/json" \
     -H "X-Tenant-ID: acme-corp" \
     -d '{"referenceNumber":"PO-1","supplierName":"Acme Supply","items":[{"productId":1,"quantity":50,"unitPrice":12.5}]}'

# Sell 20 units to a (new or existing) customer
curl -X POST "http://localhost:5274/api/sales/outbound" \
     -H "Content-Type: application/json" \
     -H "X-Tenant-ID: acme-corp" \
     -d '{"invoiceNumber":"INV-1","customerName":"Robert Smith","customerPhone":"+12065550142","items":[{"productId":1,"quantity":20,"unitPrice":19.99}]}'
```

> Tip: products and their `InventoryItem` rows are tenant data — insert them via your tenant DB or a seed script, then use the endpoints above to move stock.

### Database migrations (optional)

Migrations are applied automatically at runtime. To create new schema changes:

```bash
dotnet ef migrations add <Name> \
  --project Warehouse.Infrastructure \
  --startup-project Warehouse.API
```

## Running the Tests

The test suite consists of **full-stack integration tests** (`Warehouse.Tests`) that boot the real API in-process via `WebApplicationFactory<Program>` and hit **real SQLite databases** (isolated in a temp directory per test run) — no mocks, no InMemory provider.

```bash
dotnet test
```

### Coverage highlights

| Test class | Verifies |
|---|---|
| `InboundTransactionTests` | Receipt increments stock (0 → 50), persists immutable SKU/name/unit snapshots, computes transaction totals |
| `OutboundTransactionTests` | Sale debits stock (100 → 80), auto-creates customer; **reuses** existing customer by phone (no duplicates); insufficient stock → **422 + full rollback** (stock unchanged, no invoice row) |
| `ConcurrencyConflictTests` | Two parallel sales on the same stock row → exactly one `202`, other conflicts (409 path); explicit stale-token `DbUpdateConcurrencyException` handling |
| `TenantIsolationTests` | Missing `X-Tenant-ID` → `400`; data written in tenant A is **invisible** in tenant B (physical DB isolation) |
| `CustomerSearchTests` | Name infix matching, phone prefix matching, result limit enforcement |

With code coverage (coverlet):

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Technology Stack

| Area | Technology |
|---|---|
| Runtime / Language | .NET 10, C# 14 (records, top-level statements, nullable reference types) |
| Web framework | ASP.NET Core Web API, minimal hosting |
| ORM | Entity Framework Core 10 (SQLite provider, fluent configurations, migrations) |
| Database | SQLite (WAL journal mode, shared cache, foreign keys, busy timeout) |
| API docs | Swashbuckle / Swagger UI |
| Testing | xUnit, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory), coverlet code coverage |
| DI / composition | Built-in Microsoft.Extensions.DependencyInjection |

## Project Structure

```
WarehouseManager/
├── WarehouseManager.sln
├── Warehouse.Core/                    # Domain layer — zero dependencies
│   ├── Common/BaseEntity.cs           #   Id + audit timestamps
│   ├── Commands/                      #   Immutable command records (DTOs)
│   ├── Entities/                      #   Product, Customer, transactions & lines, InventoryItem
│   ├── Exceptions/                    #   ConcurrencyConflict, InsufficientStock
│   └── Interfaces/                    #   IInventoryService, ICustomerService, ITenantProvider
├── Warehouse.Infrastructure/          # Persistence + domain service implementations
│   ├── Configurations/                #   EF Core fluent mappings (precisions, FKs, indexes)
│   ├── Persistence/
│   │   ├── WarehouseDbContext.cs      #   7 DbSets, concurrency-token regeneration on save
│   │   ├── TenantDbContextFactory.cs  #   DB-per-tenant provisioning, WAL pragmas, traversal guard
│   │   └── Migrations/                #   Initial schema + snapshot
│   └── Services/                      #   InventoryService (atomic pipelines), CustomerService
├── Warehouse.API/                     # Presentation layer
│   ├── Controllers/                   #   Customers, Inbound, Sales
│   ├── Middleware/                    #   ConcurrencyExceptionMiddleware, HeaderTenantProvider
│   └── Program.cs                     #   DI + pipeline composition
└── Warehouse.Tests/                   # Integration tests (xUnit + WebApplicationFactory)
    ├── Fixtures/WarehouseTestFactory.cs
    └── Tests/                         #   Inbound, Outbound, Concurrency, TenantIsolation, CustomerSearch
```

## Roadmap

Ideas for future iterations:

- JWT authentication & role-based authorization
- Product & catalog management endpoints
- Pagination and richer filtering on all queries
- FluentValidation for command contracts
- Docker support & CI pipeline
- Reporting endpoints (stock valuation, sales history)

---

* Designed and implemented end-to-end: domain model, persistence, multi-tenant infrastructure, API, and integration tests.*
