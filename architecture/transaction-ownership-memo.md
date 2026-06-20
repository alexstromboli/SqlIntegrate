# Transaction Ownership in Generated `DbProc` — Problem & Proposed Fix

Status: design memo, not yet implemented.
Scope: SqlIntegrate generator + the hand-written part of `DbProc` it depends on.
Last touched: 2026-05-05.

## 1. What's there today

Each generated `DbProc` exposes:

```csharp
public class DbProc
{
    public NpgsqlConnection Conn;

    public async ValueTask<NpgsqlTransaction> BeginTransactionOptionalAsync ()
    {
        try
        {
            return await Conn.BeginTransactionAsync ();
        }
        catch (InvalidOperationException)
        {
            return await ValueTask.FromResult<NpgsqlTransaction> (null);
        }
    }

    // ... per-schema accessors, ctor ...
}
```

Every generated proc wrapper in the same file is shaped like:

```csharp
using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
{
    using (var Cmd = Conn.CreateCommand ()) { ... }
    using (var ResCmd = Conn.CreateCommand ()) { ... FETCH ALL IN ... }
    using (var cmdClose = Conn.CreateCommand ()) { ... CLOSE ... }

    if (Tran != null)
    {
        await Tran.CommitAsync ();
    }
}
```

Goal of `BeginTransactionOptionalAsync`: open a transaction only when one isn't
already open on the connection (Postgres has no nested transactions). When the
caller is already inside a transaction, return `null` so the wrapper's
`if (Tran != null) Commit` skips and the outer scope keeps ownership.

## 2. Why the current shape is not great

### 2.1 Exception-driven control flow on a hot path
"Already in a transaction" is normal, predictable state, not an error. The
current code detects it by *attempting* `BeginTransactionAsync` and catching the
`InvalidOperationException` Npgsql throws on a nested begin. Every nested call
pays for an exception throw + unwind, and the intent is invisible to a reader
of the generated file.

### 2.2 The catch is too broad — it silently swallows real failures
`NpgsqlConnection.BeginTransactionAsync` throws `InvalidOperationException` for
several reasons besides "transaction already in progress":

- Connection is not `Open` (closed, broken, disposed).
- Connection is enlisted in an ambient `System.Transactions` scope.
- A concurrent operation is in progress on the same connection
  (`NpgsqlOperationInProgressException` derives from `InvalidOperationException`).

In all of these the method silently returns `null`. The wrapper then runs
`Cmd.ExecuteNonQueryAsync()` against a connection that isn't usable, and the
caller sees a confusing downstream error instead of the real root cause. A
real bug ("connection isn't open") is reported as "no outer transaction" —
exactly the kind of bug-masking the catch was not intended to do.

### 2.3 Reliance on a driver implementation detail
Whether nested `BeginTransaction` throws — and what type it throws — is an
implementation choice of Npgsql, not a contract. Generated code shouldn't
hinge on it.

### 2.4 Cosmetic
`return await ValueTask.FromResult<NpgsqlTransaction>(null)` is `return null`
with extra steps; there's no async work in that branch.

## 3. The detection challenge with externally-opened transactions

A naive fix — "track the active transaction in a field on `DbProc` and check
the field" — is incomplete. Real callers in the host project open
transactions directly on `NpgsqlConnection`, not through `DbProc`. Concrete
example:

```csharp
await using var Tx = await Db.Connection.BeginTransactionAsync (Ct);

var Inserted = await Db.DbProc._public.processed_webhooks_try_insertAsync (...);

await StripeWebhookDispatcher.DispatchAsync (...);

await Tx.CommitAsync (Ct);
```

The transaction is owned by the connection, not by `DbProc`. A field on
`DbProc` would still be `null` when the proc wrapper inside
`processed_webhooks_try_insertAsync` runs, the wrapper would try to begin a
new transaction, and Npgsql would throw — leaving the exception-catch problem
unsolved.

There is no clean public Npgsql API for "does this connection currently have
a transaction?" — `NpgsqlConnection` doesn't expose its active transaction
publicly (it lives on the internal `Connector`). The realistic options are:

- **A.** Centralize: route *all* transaction creation through `DbProc`, so
  the field is authoritative.
- **B.** Hybrid: field first, fall back to a discriminating exception catch
  (filter by message for the "nested" case only). Brittle across Npgsql
  versions; needs a pinned version + regression test.
- **C.** Probe Postgres with `SHOW transaction_status` per call. Reliable,
  but adds a round-trip to every read-only proc call. Bad for hot paths.

Of these, **A is recommended.** The rest of this memo details it.

## 4. Option A — `DbProc` is the single transaction owner

### 4.1 Invariant
Every transaction on the underlying `NpgsqlConnection` is opened through
`DbProc`. Code that today does
`Db.Connection.BeginTransactionAsync(Ct)` is migrated to
`Db.DbProc.BeginTransactionAsync(Ct)`.

### 4.2 Hand-written part of `DbProc` (lives in the SqlIntegrate generator)

```csharp
public class DbProc
{
    public NpgsqlConnection Conn;

    // Active transaction owned by DbProc. Cleared on commit/rollback/dispose
    // by the OwnedTransaction wrapper returned to the caller.
    public NpgsqlTransaction CurrentTransaction { get; private set; }

    // Always begins a new transaction. Throws if one is already active.
    // Use when the caller wants to be the owner.
    public async ValueTask<OwnedTransaction> BeginTransactionAsync (
        CancellationToken Ct = default)
    {
        if (CurrentTransaction != null)
        {
            throw new InvalidOperationException (
                "DbProc already has an active transaction. Use " +
                "BeginTransactionOptionalAsync to participate in it.");
        }

        if (Conn.State != ConnectionState.Open)
        {
            throw new InvalidOperationException ("Connection is not open.");
        }

        var Tx = await Conn.BeginTransactionAsync (Ct);
        CurrentTransaction = Tx;
        return new OwnedTransaction (this, Tx);
    }

    // Begins a new transaction only if none is active on this DbProc.
    // Returns null when a transaction is already in flight — the caller
    // must not commit, dispose, or rollback in that case (`using (null)`
    // and `if (Tran != null) Commit()` make this a no-op).
    public async ValueTask<NpgsqlTransaction> BeginTransactionOptionalAsync (
        CancellationToken Ct = default)
    {
        if (CurrentTransaction != null)
        {
            return null;
        }

        if (Conn.State != ConnectionState.Open)
        {
            throw new InvalidOperationException ("Connection is not open.");
        }

        var Tx = await Conn.BeginTransactionAsync (Ct);
        CurrentTransaction = Tx;
        // Note: returns the raw NpgsqlTransaction so the generated wrappers
        // continue to work unchanged — they `using` it and conditionally
        // Commit. The CurrentTransaction field is cleared in
        // ClearTransaction below, called from OwnedTransaction.Dispose
        // for the public path. For the wrapper path (raw NpgsqlTransaction
        // returned), see 4.4 for how clearing is handled.
        return Tx;
    }

    internal void ClearTransaction (NpgsqlTransaction Tx)
    {
        if (ReferenceEquals (CurrentTransaction, Tx))
        {
            CurrentTransaction = null;
        }
    }
}

public sealed class OwnedTransaction : IAsyncDisposable, IDisposable
{
    readonly DbProc DbProc;
    readonly NpgsqlTransaction Tx;
    bool Done;

    internal OwnedTransaction (DbProc DbProc, NpgsqlTransaction Tx)
    {
        this.DbProc = DbProc;
        this.Tx = Tx;
    }

    public NpgsqlTransaction Inner => Tx;

    public async ValueTask CommitAsync (CancellationToken Ct = default)
    {
        await Tx.CommitAsync (Ct);
        Done = true;
        DbProc.ClearTransaction (Tx);
    }

    public async ValueTask RollbackAsync (CancellationToken Ct = default)
    {
        await Tx.RollbackAsync (Ct);
        Done = true;
        DbProc.ClearTransaction (Tx);
    }

    public async ValueTask DisposeAsync ()
    {
        try
        {
            await Tx.DisposeAsync ();
        }
        finally
        {
            if (!Done)
            {
                DbProc.ClearTransaction (Tx);
            }
        }
    }

    public void Dispose ()
    {
        try
        {
            Tx.Dispose ();
        }
        finally
        {
            if (!Done)
            {
                DbProc.ClearTransaction (Tx);
            }
        }
    }
}
```

### 4.3 What changes for callers
- `BeginTransactionOptionalAsync` is a pure boolean check — no exceptions thrown
  on the nested path. Real connection-state errors propagate instead of being
  swallowed.
- A new `BeginTransactionAsync` is the hook external callers use when they
  want to own a transaction. Rule: "anyone who wants a transaction on this
  connection asks `DbProc` for it."
- The generated wrappers continue to compile unchanged — they still see a
  `NpgsqlTransaction` (or `null`) from `BeginTransactionOptionalAsync` and
  use it the same way.

### 4.4 Subtlety: clearing `CurrentTransaction` on the wrapper path
The generated wrappers receive the raw `NpgsqlTransaction` (not the
`OwnedTransaction` wrapper) and `using` it directly. Their dispose path
therefore doesn't run our `ClearTransaction` hook. Two ways to handle this:

1. **Subscribe to the transaction's dispose.** `NpgsqlTransaction` doesn't
   expose a dispose event. Either use `ConditionalWeakTable` to associate a
   sentinel object whose finalizer clears the field (smelly), or — better —
2. **Have the wrappers go through a small generated helper** that returns an
   `OwnedTransaction` and uses `await using`. The wrapper's existing
   `if (Tran != null) Commit` becomes
   `if (Tran != null) await Tran.CommitAsync (...)` against the
   `OwnedTransaction`. This is a small generator change — search the
   wrapper template for `BeginTransactionOptionalAsync` and the matching
   `if (Tran != null) await Tran.CommitAsync ()`.

Option 2 is cleaner and is what this memo proposes. The change is local to
the wrapper template in SqlIntegrate.

### 4.5 Migration of existing host code
Audit the host repos for direct uses of
`Connection.BeginTransactionAsync` / `BeginTransaction` against connections
that are also driving a `DbProc`. Migration is mechanical:

```csharp
// before
await using var Tx = await Db.Connection.BeginTransactionAsync (Ct);
...
await Tx.CommitAsync (Ct);

// after
await using var Tx = await Db.DbProc.BeginTransactionAsync (Ct);
...
await Tx.CommitAsync (Ct);
```

`OwnedTransaction` is `IAsyncDisposable`-shaped, so `await using` keeps
working. `Tx.CommitAsync()` and `Tx.RollbackAsync()` keep working.

### 4.6 Tests / verification
- A unit test that opens a transaction via `DbProc.BeginTransactionAsync`,
  then calls a wrapped read-only proc, and asserts:
  - the wrapper returns its data,
  - `CurrentTransaction` stays the *outer* transaction the whole time,
  - the wrapper performs no commit (committed only when the outer does).
- A unit test for the negative case: try to open two `BeginTransactionAsync`
  in a row on the same `DbProc`, assert clear `InvalidOperationException`
  with the message we wrote (no swallowed driver exception).
- A regression test that puts `Conn` into a non-open state and asserts
  `BeginTransactionOptionalAsync` throws `InvalidOperationException("Connection is not open.")`
  rather than silently returning `null`.

## 5. Why not Option B
Keeping the exception fallback narrow (filter by message text) was tempting
because it requires no host-code changes. It's rejected here because:

- The filter is brittle across Npgsql versions — any wording change silently
  re-breaks the contract.
- It still pays for a thrown exception on every webhook call (or whatever
  externally-opens-tx caller), which is exactly the perf complaint we wanted
  to eliminate.
- Migrating the few `Connection.BeginTransactionAsync` call sites is small
  and gives a stronger invariant going forward.

## 6. Open questions / followups
- Should `DbProc.BeginTransactionAsync` accept `IsolationLevel`? Probably
  yes — match `NpgsqlConnection.BeginTransactionAsync(IsolationLevel, CancellationToken)`.
- Do we also want a `DbProc.UseExternalTransaction (NpgsqlTransaction Tx)`
  for cases where some non-`DbProc` framework (EF Core, Dapper) owns the
  transaction? Out of scope for this memo, but the field design supports
  adding it later.
- SqlIntegrate currently lives separately from the host generator output.
  When this lands, the generator version (and the version of the
  hand-written `DbProc` snippet that `imps` produces) need to step
  together.

## 7. TL;DR
Replace exception-driven detection with explicit ownership tracked on
`DbProc`. Add `DbProc.BeginTransactionAsync` as the single entry point for
opening transactions on a `DbProc`-managed connection. Migrate host
callers (e.g. `StripeConnectWebhookHandler`) off
`Connection.BeginTransactionAsync`. The hot read path stops throwing
exceptions, real connection errors stop being swallowed, and "who owns the
transaction" is expressed in code instead of in driver behaviour.
