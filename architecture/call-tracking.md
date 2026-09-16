# Call Tracking in Generated Wrappers — `IDbProcTracker<T>`

Status: implemented.
Scope: `Runtime/IDbProcTracker.cs`, `Wrapper/GeneratorOptions.cs`, `Wrapper/Generator.cs`, and the
code they make `TestWrapper` emit.

## 1. What it is for

A generated wrapper is otherwise a closed box. A caller sees that
`Db.alexey.get_aggregates (1.7f)` returned and nothing else: not how long the round trip took, not
how many rows came out of which cursor, not which command text went to the server. The only way to
get any of it was to hand-wrap every call site, which is what generating the call sites was meant to
avoid.

Tracking adds one observation seam, off by default. Turned on, every generated caller reports itself
— entry, the `ExecuteNonQuery`, each cursor fetch, exit — to an object the host supplies. The
obvious use is a metric: take a high-resolution timestamp on entry, take another on exit, log the
difference against the procedure's name.

It observes only. A tracker cannot cancel a call, substitute a result, retry, or redirect a
connection, and it is never consulted about whether to make a call at all. Everything it is handed
is either already in scope at the call site or a compile-time string literal. A wrapper built with
tracking on and a no-op tracker costs, per call, one `T` allocation, six interface calls for a
procedure with one cursor (two more per additional cursor), and one or two `int` locals — no extra
round trip, no extra allocation per row, and nothing added inside a read loop.

## 2. The contract

`Runtime/IDbProcTracker.cs`, namespace `SqlIntegrate`:

```csharp
public interface IDbProcTracker<T> where T : IDisposable
{
	T OnEnter (string Schema, string Procedure);
	void OnBeforeExecute (T Tracker, NpgsqlCommand Command);
	void OnAfterExecute (T Tracker, NpgsqlCommand Command, int RowsAffected);
	void OnBeforeFetchCursor (T Tracker, string CursorName, NpgsqlCommand Command);
	void OnAfterFetchCursor (T Tracker, string CursorName, NpgsqlCommand Command, int RowCount);
	void OnExit (T Tracker);
}
```

One instance serves a whole `DbProc`, for the `DbProc`'s whole life. It therefore holds nothing
about any single call: `T` does. `OnEnter` makes one, every other method is handed it back, and it
is disposed when the call leaves. That is what makes a single tracker instance safe to share across
concurrent calls on different connections — the interface has no place to put per-call state other
than `T`, so an implementation cannot accidentally keep it in a field.

| Member | When | What it is handed |
|---|---|---|
| `OnEnter` | first statement of the call, before any command exists | the schema and procedure **native** names |
| `OnBeforeExecute` | after the command text and every parameter are set, before the call goes out | the `NpgsqlCommand` about to run |
| `OnAfterExecute` | immediately after `ExecuteNonQuery`/`ExecuteNonQueryAsync` returns | the same command, and its return value |
| `OnBeforeFetchCursor` | after `FETCH ALL IN "…"` is set, before the reader opens | the cursor name and the fetch command |
| `OnAfterFetchCursor` | after the reader is closed and the set is fully built | the same, plus how many rows arrived |
| `OnExit` | last thing in the call, in a `finally`, before `T` is disposed | the state object |

Notes on the parameters:

- **`Schema` and `Procedure` are the native names**, the ones that go into the command text
  (`"alexey"`, `"get_aggregates"`), not the C# identifiers. That is deliberate: what a tracker
  reports has to line up with what the database's own logs say, and `ValidCsName ()` can rename.
- **`RowsAffected`** is whatever `ExecuteNonQuery` returned, passed through uninterpreted. For the
  `call proc (…)` form the wrapper uses, PostgreSQL generally reports `-1`; the value is given
  because it is free, not because it means much here.
- **`RowCount`** is per cursor, not per call. A procedure with three result sets calls
  `OnAfterFetchCursor` three times, and a tracker that wants a total adds them up itself — see
  `TryWrapper/SampleTracker.cs`.
- **`Command`** on the fetch pair is the `FETCH ALL IN` command, not the one that ran the procedure.

An implementation must not throw. Nothing in the generated code catches what a tracker raises, so an
exception out of `OnBeforeExecute` fails the procedure call, and one out of `OnExit` replaces
whatever the call was already throwing.

### 2.1 Why the file lives in `Runtime/`, outside every project

`Runtime/` has no `.csproj` and is compiled by no library of its own. The interface names
`NpgsqlCommand`; `Wrapper` deliberately carries no Npgsql reference, because the generator works in
CLR and PostgreSQL type *names* and never in the types themselves (see the comment atop
`Wrapper/ClrType.cs`). Putting the contract in `Wrapper` would drag a dependency into the generator
that exists only for the code the generator writes, and would not help the consumer anyway: a
project that compiles a generated wrapper — `TryWrapper` is the example — references no SqlIntegrate
project at all. It just compiles the `.cs` file.

So consuming projects pull the source in, the same way `Wrapper` pulls its DbAnalysis sources:

```xml
<Compile Include="..\Runtime\IDbProcTracker.cs">
  <Link>IDbProcTracker.cs</Link>
</Compile>
```

Namespace `SqlIntegrate` is independent of whatever namespace a wrapper is generated into, and the
generator adds `using SqlIntegrate;` to a tracked file's using list.

## 3. Turning it on

`Wrapper/GeneratorOptions.cs`:

```csharp
public string TrackerStateType = null;
```

Null — the default — means no tracking. **Not "tracking with a null tracker": no tracking.** The
emitted text is then character for character what it was before the feature existed, which is
checked (§6). Set it to the fully qualified C# name of the state type, the `T` of
`IDbProcTracker<T>`:

```csharp
GeneratorOptions Options = new GeneratorOptions
{
	TrackerStateType = "TryWrapper.SampleTracker.Call"
};
```

`TestWrapper` exposes it as `--tracker=<TypeName>`, which also decides whether the third generation
run happens at all (§5).

### 3.1 Why a type name, and not a generic `DbProc<T>`

A generic `DbProc<TTracker>` would need no option — but the per-schema classes hold the `DbProc`, so
they would have to be generic too, and result and enum classes are nested *inside* the schema
classes. `Generated.alexey.get_aggregates_Result` would become
`alexey<MyApp.CallMetrics>.get_aggregates_Result`, and `Generated.alexey.app_status.active` would
become `alexey<MyApp.CallMetrics>.app_status.active`, in every consumer, tracked or not. Naming the
type once at generation keeps `DbProc` and every schema class non-generic, so nothing a consumer
already writes changes spelling.

The cost is that the wrapper is generated against one tracker state type. A host wanting two must
generate twice — which is exactly what the test does, and what `TrackedNamespaceCodeProcessor` is
for.

## 4. What gets emitted

### 4.1 `DbProc`

The tracker is a public field and a constructor parameter, directly after `Conn`:

```csharp
public class DbProc
{
	public NpgsqlConnection Conn;
	public IDbProcTracker<TryWrapper.SampleTracker.Call> Tracker;
	public Func<byte[], byte[]> Encryptor;     // contributed by a processor
	public Func<byte[], byte[]> Decryptor;     // contributed by a processor

	public DbProc (NpgsqlConnection Conn, IDbProcTracker<TryWrapper.SampleTracker.Call> Tracker,
		Func<byte[], byte[]> Encryptor, Func<byte[], byte[]> Decryptor)   // one line, as emitted
	{ … }
}
```

Neither the field nor the parameter is emitted by hand. The generator pushes a `DbProcProperty` of
its own — the same channel `EncryptionCodeProcessor` uses for `Encryptor` and `Decryptor` — before
the `OnCodeGenerationStarted` pass, so the existing plumbing emits the field, the parameter and the
assignment, and the tracker lands ahead of anything a processor contributes.

**The tracker is required, not optional, once the option is on.** There are no null checks in the
generated code; a host that wants tracking off at runtime passes a no-op tracker, whose calls the
JIT can inline away. A per-call-site `if (Tracker != null)` would cost more than it saves and would
make "tracking is on" mean two different things.

### 4.2 A procedure caller

Real output, from `TryWrapper/dbproc_tracked.cs` — a single-row, single-column result set:

```csharp
public async Task<string> get_scalarAsync ()
{
	string Result = null;

	using (var Tracking = DbProc.Tracker.OnEnter ("alexey", "get_scalar"))
	{
		try
		{
			using (var Tran = await DbProc.BeginTransactionOptionalAsync ())
			{
				using (var Cmd = Conn.CreateCommand ())
				{
					Cmd.CommandText = "call \"alexey\".\"get_scalar\" (@partial);";
					Cmd.Parameters.Add (new NpgsqlParameter ("@partial", NpgsqlDbType.Refcursor) { … });

					DbProc.Tracker.OnBeforeExecute (Tracking, Cmd);
					int Affected = await Cmd.ExecuteNonQueryAsync ();
					DbProc.Tracker.OnAfterExecute (Tracking, Cmd, Affected);

					using (var ResCmd = Conn.CreateCommand ())
					{
						ResCmd.CommandText = "FETCH ALL IN \"partial\";";
						string Set = null;
						int Rows = 0;

						DbProc.Tracker.OnBeforeFetchCursor (Tracking, "partial", ResCmd);

						using (var Rdr = await ResCmd.ExecuteReaderAsync ())
						{
							if (Rdr.Read ())
							{
								Set = Rdr["name"] as string;
								Rows = 1;
							}
						}

						DbProc.Tracker.OnAfterFetchCursor (Tracking, "partial", ResCmd, Rows);

						Result = Set;
					}

					using (var cmdClose = Conn.CreateCommand ())
					{
						cmdClose.CommandText = "CLOSE \"partial\";";
						await cmdClose.ExecuteNonQueryAsync ();
					}

					if (Tran != null)
					{
						await Tran.CommitAsync ();
					}
				}
			}

			return Result;
		}
		finally
		{
			DbProc.Tracker.OnExit (Tracking);
		}
	}
}
```

Everything between the `Result` declaration and the `return` is inside the tracked scope, including
the `return` itself. The `Result` local stays outside it, where it already was.

### 4.3 The four decisions inside that shape

**`try`/`finally`, so `OnExit` runs on the throwing path.** Without it, a failed call would dispose
`T` but never report an exit, and a tracker measuring latency would average only the calls that went
well — which is the wrong number, and wrong in the flattering direction. Order on the way out is
`OnExit`, then `Dispose`, as the interface promises.

**Only the real body is tracked; the sync forwarder is not.** Every procedure gets a sync method,
and one additionally gets an `…Async` twin when it has no OUT arguments (`ref` cannot cross an
`async` boundary). Where the twin exists, the sync method is a single forwarding line:

```csharp
public string get_scalar ()
{
	return get_scalarAsync ().Result;
}
```

That line is left alone. Tracking it as well would report two entries and two exits for one round
trip, and the inner pair would be nested inside the outer. A procedure *with* OUT arguments has no
twin, so its sync method is the real body and is tracked there, with `Cmd.ExecuteNonQuery ()` and no
`await` — see `test_out` in the generated file.

**`RowsAffected` costs a local only when tracking is on.** Untracked, the statement is still
`await Cmd.ExecuteNonQueryAsync ();` with the result discarded, exactly as before.

**Row count is counted two different ways, each the cheapest correct one for its shape.** A list
result set reports `Set.Count`, which is free. A single-row set (`# 1` in the procedure's comments)
gets an `int Rows = 0;` local set to `1` beside the assignment, because its `Set` is not a list: for
a single-column set it is the column's own CLR type, and a `MapTo` can make that a type that does
not compare to `null`. `Set != null ? 1 : 0` would compile for every type in the corpus today and
break on the first host that maps a column to a struct.

**What is not tracked:** `BeginTransactionOptionalAsync` and the commit, and the `CLOSE "cursor"`
commands. The first is not the procedure, and the second is teardown rather than a fetch; both would
make the pairs stop corresponding to anything a reader would want to count.

### 4.4 Local names the generated body introduces

`Tracking`, `Affected`, and `Rows`. Like the `Cmd`, `Result`, `Set`, `Rdr`, `ResCmd` and `cmdClose`
the template already uses, these can in principle collide with a procedure argument of the same
name. That risk predates tracking and is not addressed.

## 5. Generating and consuming one

`TestWrapper` makes three files, the third only when `--tracker=` is given:

| Target | Processors | Namespace / class |
|---|---|---|
| `dbproc.cs` | `ChangeNameCodeProcessor` | `FirstSolution.Proxy` |
| `dbproc_sch_noda.cs` | NodaTime, tagger, encryption | `Generated.DbProc` |
| `dbproc_tracked.cs` | the same three, plus `TrackedNamespaceCodeProcessor` | `GeneratedTracked.DbProc` |

The tracked run reuses the `dbproc_sch_noda.cs` chain on purpose, so tracking is exercised beside
the parameter and column rewriting those processors do rather than on a bare wrapper. Only the
namespace moves; the class stays `DbProc`, which is what lets both files live in one assembly.

> Renaming the *class* of a tracked wrapper does not work today, and not because of tracking:
> `Generator.cs` emits the literal `"DbProc"` for the schema class's back-reference instead of
> `Database.CsClassName`, so `dbproc.cs` — which renames the class to `Proxy` — does not compile
> either. Changing only the namespace sidesteps it.

`TryWrapper` is the worked consumer end to end:

- `TryWrapper.csproj` links `..\Runtime\IDbProcTracker.cs`.
- `SampleTracker.cs` implements `IDbProcTracker<SampleTracker.Call>`. `Call` holds the schema and
  procedure names, a `Stopwatch.GetTimestamp ()` from `OnEnter`, and a running row total;
  `OnAfterFetchCursor` adds to the total, and `OnExit` prints the elapsed milliseconds.
- `Program.cs` builds one beside the untracked wrapper:

  ```csharp
  var Tracked = new GeneratedTracked.DbProc (Conn, new SampleTracker (), XorCryptor, XorCryptor);
  var tracked_scalar = Tracked.alexey.get_scalar ();
  ```

  which prints, against the fixture database:

  ```
  tracked: alexey.get_scalar took 53.9 ms, 1 row(s)
  ```

## 6. How it is verified

`test/run_test.sh` gained one check, after the report hash and before the failure-contract section:

```bash
if BUILD_OUTPUT="$(dotnet build ../TryWrapper/TryWrapper.csproj -v q 2>&1)"; then
    report_ok "success: the generated wrappers compile"
else
    report_failed "failed: the generated wrappers do not compile"
    echo "$BUILD_OUTPUT"
fi
```

This is the first thing in the suite that ever hands generated code to a compiler. A matching report
says nothing about whether the code built from it is C#, and `TryWrapper` now carries both flavours,
so one build covers the untracked wrapper and the tracked one — whose every caller calls into
`IDbProcTracker`, so a signature drift between the contract and the emitted calls fails here. It is
guarded rather than left to `set -e`, because a failure is one more red line, not the end of the
run.

**The no-option guarantee** is checked separately and is the one worth re-running after any change
to `Generator.cs`: `TryWrapper/dbproc_sch_noda.cs` is git-tracked and rewritten by every test run, so

```bash
git diff --stat TryWrapper/dbproc_sch_noda.cs
```

must be empty. Anything there means the tracking code leaked outside its condition. For a change
that is *supposed* to move untracked output, build the generator at the previous commit in a
throwaway worktree and diff the two generated files directly.

## 7. Open followups

- The tracker is reported to synchronously, on the calling thread, including from inside `async`
  methods. A tracker that wants to do I/O has to hand off to something else; the interface has no
  async form and gaining one would put an `await` on every call site.
- `OnExit` gets no indication of whether the call succeeded. Adding the in-flight exception would
  let a tracker separate failure latency from success latency, at the cost of a `catch`/`throw;` or
  an `ExceptionDispatchInfo` on every caller.
- Nothing is reported around `BeginTransactionOptionalAsync` or the commit. If the transaction
  rework in [transaction-ownership-memo.md](transaction-ownership-memo.md) lands, that is the moment
  to decide whether a transaction is worth a pair of its own.
