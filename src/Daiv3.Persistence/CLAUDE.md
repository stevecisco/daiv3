# Daiv3.Persistence – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

SQLite-based persistence layer. Implements the repository pattern for all durable application state: chat sessions, learning metrics, knowledge base metadata, configuration, and entity storage. Handles schema migrations and transaction management.

## Project Type

Library

## Target Framework

`net10.0` (cross-platform; uses `Microsoft.Data.Sqlite` which is pre-approved).

## Key Responsibilities

- `IDatabaseContext` — central database abstraction, owns connection lifetime
- Repository implementations for each entity type
- Schema migration runner (applied on `InitializeAsync()`)
- Transaction support across multiple repository operations
- `NULL`-safe SQL reader patterns (always use `IsDBNull()` before typed getters on aggregation columns)

## Patterns

- All repositories accept `IDatabaseContext` (injected) and `ILogger<T>`
- Integration tests use a per-test SQLite file (not in-memory) and delete it in `DisposeAsync()` with retry logic
- **Never use `File.Delete()` directly** — use `DeleteFileWithRetryAsync()` to handle async handle release on Windows (see AI-Instructions.md § 3.9)

## Test Projects

```powershell
# Unit tests
dotnet test tests/unit/Daiv3.Persistence.Tests/Daiv3.Persistence.Tests.csproj --nologo --verbosity minimal

# Integration tests (uses real SQLite file on disk)
dotnet test tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj --nologo --verbosity minimal
```

## Common Pitfalls

- `GetInt32()` / `GetInt64()` throw on `NULL` from `SUM`/`COUNT` with zero rows — always guard with `reader.IsDBNull(n)`
- Integration test `DisposeAsync()` must dispose `IDatabaseContext` **before** deleting the `.db`, `-wal`, and `-shm` files
- Test classes must be `sealed`, implement both `IAsyncLifetime` and `IDisposable` — see AI-Instructions.md § 3.6
