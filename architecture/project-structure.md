# SqlIntegrate Project Structure

This document provides comprehensive documentation of the .NET solution structure and test infrastructure.

## Solution Overview

SqlIntegrate is a .NET 8.0 solution that analyzes PostgreSQL databases and generates type-safe C# wrapper code for stored procedures and functions.

**Solution File:** `SqlIntegrate.sln`

**Projects (6 total):**
- 2 class libraries (DbAnalysis, Wrapper)
- 4 console applications (ParseProcs, TestWrapper, TryWrapper, TryPsql)

## Project Dependency Graph

```
┌─────────────────────────────────────────────────────────────────────┐
│                        SqlIntegrate.sln                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│   ┌──────────────┐         ┌──────────────┐                         │
│   │  ParseProcs  │────────▶│  DbAnalysis  │◀──────┐                 │
│   │  (console)   │         │  (library)   │       │                 │
│   └──────────────┘         └──────────────┘       │ linked files    │
│                                   ▲               │                 │
│                                   │               │                 │
│   ┌──────────────┐         ┌──────────────┐       │                 │
│   │ TestWrapper  │────────▶│   Wrapper    │───────┘                 │
│   │  (console)   │         │  (library)   │                         │
│   └──────────────┘         └──────────────┘                         │
│                                                                     │
│   ┌──────────────┐         ┌──────────────┐                         │
│   │  TryWrapper  │         │   TryPsql    │    (standalone)         │
│   │  (console)   │         │  (console)   │                         │
│   └──────────────┘         └──────────────┘                         │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## NuGet Dependencies

| Project     | Package             | Version |
|-------------|---------------------|---------|
| DbAnalysis  | Newtonsoft.Json     | 13.0.1  |
| DbAnalysis  | Npgsql              | 6.0.5   |
| DbAnalysis  | Sprache             | 2.3.1   |
| Wrapper     | Newtonsoft.Json     | 13.0.1  |
| ParseProcs  | Newtonsoft.Json     | 13.0.1  |
| TestWrapper | Newtonsoft.Json     | 13.0.1  |
| TryWrapper  | Newtonsoft.Json     | 13.0.1  |
| TryWrapper  | NodaTime            | 3.1.0   |
| TryWrapper  | Npgsql              | 6.0.5   |
| TryWrapper  | Npgsql.NodaTime     | 6.0.5   |
| TryPsql     | Npgsql              | 6.0.5   |

## Projects Detail

### DbAnalysis (Core Library)

**Location:** `DbAnalysis/`

The core library providing SQL parsing, PostgreSQL type system representation, and database introspection.

**Key Files:**

| File | Purpose |
|------|---------|
| `Analyzer.cs` | SQL parser using Sprache parser combinators. Parses procedure bodies, expressions, CASE statements, function calls |
| `ReadDatabase.cs` | PostgreSQL introspection via pg_catalog queries. Loads types, procedures, functions, enums, composite types |
| `DatabaseContext.cs` | Container for introspected database metadata |
| `PSqlType.cs` | PostgreSQL type system representation. Handles base types, arrays, enums, composites. CLR type mapping |
| `DatasetStructs.cs` | Generic container types: Column, GSqlType, GResultSet, Argument, GProcedure, GModule |
| `Procedure.cs` | Procedure metadata (extends SchemaEntity) |
| `PSqlOperatorPriority.cs` | Operator precedence definitions for expression parsing |

**Namespaces:**
- `DbAnalysis` - Main namespace
- `DbAnalysis.Datasets` - Generic data structure templates
- `DbAnalysis.Cache` - Procedure analysis caching (HashUtils, CachedAnalysis, IProcedureStateCache, LocalUserCache, VoidCache)

**The analysis cache:**

`IProcedureStateCache` has two implementations: `LocalUserCache`, one JSON file per procedure under
`~/.sqlintegrate/cache`, and `VoidCache`, which stores nothing and is what `--no-cache` selects.
Entries not read for 30 days are swept at construction, and a read touches the file's mtime, so an
entry stays alive as long as it is used.

An entry is a `CachedAnalysis`: the `Datasets.Procedure` report, plus the closure of callee
signatures it was inferred against. The two are validated differently, because they answer to
different kinds of input.

*The key* is the file name, `{analyzer}_{data layout}_{procedure}` — everything the analysis
depends on that is known **before** the parse:

| Segment | From | Covers |
|---|---|---|
| analyzer | `HashUtils.AnalyzerHash` | The `DbAnalysis` module version id, content-derived under the SDK's deterministic build |
| data layout | `ComputeDatabaseDataLayoutHash` | Custom types (enum values, composite properties), tables with their columns, and the schema order |
| procedure | `ComputeProcedureHash` | The procedure's schema, name, argument names and types, and its source |

The analyzer belongs there because the grammar is an input to the analysis: an entry written by a
different analyzer describes a procedure that may now infer different types, and returning it would
produce a silently wrong wrapper from a run that looks clean. A version attribute would not do the
job — `AssemblyVersion` is a constant, and a git-derived informational version does not move while a
change is being iterated on, which is exactly when the cache must not be trusted.

The schema order belongs there because it resolves every bare name in every procedure: reorder the
search path and an unqualified table can point at a different table entirely. It is a property of
the database, not of any one procedure, and it moves about as rarely as the tables do.

*The callee closure* is the input the key **cannot** carry. A procedure's inferred types depend on
the return types of the functions it calls, and none of that appears in the procedure's own source,
the tables or the custom types — so a key built from those alone hands back the old types while
`git diff` shows nothing and the run exits 0. The callee set is not available in time to be keyed
on either: it is a result of the parse the key selects. So `ModuleContext` records every function
resolution as it happens — the name as written and what it resolved to — and the entry carries that
list. `CachedAnalysis.MatchesCallees` replays each lookup against the current database before the
entry is trusted, and any type that comes back different makes it a miss.

Replaying the lookup rather than comparing a stored key is what makes the check complete: resolution
walks the schema order, so a function newly created earlier on the path counts as a change too. A
name resolving to nothing is recorded as its own state, distinct from every return type, so a
function created later invalidates the entry rather than matching it.

Folding signatures into the data-layout hash instead would also be correct, and is why the closure
is worth the two-stage shape: that hash is shared by every procedure in the database, so one
signature change anywhere would discard all of them — and signature changes are routine during
exactly the work the cache helps most. An entry with no recorded closure is refused, since it cannot
be told apart from one whose callees all still match.

Called *procedures* need no such treatment: `CALL` parses its target but never resolves it, so no
procedure's signature is an input to another's analysis.

Storing happens only after a procedure analyses successfully, so a failure is never cached and is
retried on the next run.
- `DbAnalysis.Sources` - Source tracking (ISource, Sourced, TableSource, FunctionSource, CompositeTypeSource, CalculatedSource, TextSpanSource, DefinitionSource)

**SQL Parsing Infrastructure:**

| File | Purpose |
|------|---------|
| `SpracheUtils.cs` | Parser combinator utilities |
| `CustomInput.cs` | Input stream for parser |
| `TextSpan.cs` | Source location tracking |
| `SelectStatement.cs` | SELECT statement parsing |
| `OrdinarySelect.cs` | Simple SELECT handling |
| `FullSelectStatement.cs` | Complex SELECT with CTEs, UNION. A CTE level is resolved to a table and pushed into the request context before the body is resolved, so later levels and the body can refer to it |
| `FromTableExpression.cs` | FROM clause parsing |
| `ValuesBlock.cs` | VALUES clause parsing |
| `OperatorProcessor.cs` | Operator precedence handling |

---

### Wrapper (Code Generation Library)

**Location:** `Wrapper/`

Code generation engine that transforms analyzed database metadata into C# wrapper code.

**Key Files:**

| File | Purpose |
|------|---------|
| `Generator.cs` | Main code generation engine. `GGenerateCode<T>()` method builds type maps and invokes processor chain |
| `Database.cs` | Code generation data structures. Organizes schemas, procedures, types, properties |
| `CodeProcessor.cs` | Chain of Responsibility pattern. Base `GCodeProcessor<T>` with virtual hooks |
| `GNodaTimeCodeProcessor.cs` | Example processor for NodaTime type mappings (timestamptz -> Instant?, etc.) |
| `CodeGenerationUtils.cs` | Utilities for file generation and content management |

**Linked Files from DbAnalysis:**
- `DatasetStructs.cs`
- `PSqlType.cs`
- `Utils.cs`

**Namespaces:**
- `Wrapper` - Code generation engine
- `Utils.CodeGeneration` - Code generation utilities

**A type with no C# mapping names where it came from.** Every reported type name is turned into a
mapping through one lookup, `TypeMappingUtils.Mapping`, which raises `UnmappedTypeException` naming
the procedure and the column, argument or property that carried the type. The dictionary's own miss
carries the type alone, and the type is the half a reader already knows; a console app catches the
exception and prints it as a report, because a stack trace through the LINQ that walked there names
neither the site nor anything to act on.

**CodeProcessor Hooks:**

```csharp
// Available virtual methods in GCodeProcessor<T>:
OnHaveModule()              // Module loaded
OnHaveTypeMap()             // Type mapping ready
OnHaveWrapper()             // Database wrapper created
OnCodeGenerationStarted()   // Generation begins
OnEncodingParameter()       // Transform parameter type for encoding
OnPassingParameter()        // Wrap parameter value when passing
OnReadingParameter()        // Transform when reading parameter
OnReadingResultSetColumn()  // Transform result set column
```

---

### ParseProcs (Entry Point Console App)

**Location:** `ParseProcs/`

Main entry point that reads PostgreSQL databases and generates JSON analysis.

**Entry Point:** `ParseProcs/Program.cs`

**Workflow:**

```csharp
// 1. Load database context
DatabaseContext context = ReadDatabase.LoadContext (connectionString);

// 2. Run analysis
Module report = Analyzer.Run (cache, dataLayoutHash);

// 3. Write JSON output
File.WriteAllText (outputFileName, JsonConvert.SerializeObject (ModuleReport));
```

**Command Line:**
```bash
ParseProcs [--no-cache] [--tolerate-failures] "host=/var/run/postgresql;database=mydb;Integrated Security=true" output.json
```

`--no-cache` analyses every procedure afresh instead of consulting `~/.sqlintegrate/cache`.
`--tolerate-failures` keeps the exit code at 0 when procedures were dropped. An unrecognised
option is refused with exit 2 rather than ignored, so a mistyped flag cannot read as a request
it is not.

**Exit codes:** `0` every procedure analysed, `1` one or more were dropped, `2` the command line
was malformed. The report is written either way, so a partial one is available for inspection;
it carries no record of what is missing, because a dropped procedure is simply absent from it —
indistinguishable there from one the database does not have. The exit code is the only channel
that distinguishes them, which is why generating code from a report is only safe after checking
it. Each dropped procedure is named as it is dropped, and again in a summary on stderr at the
end, where a truncating `| tail` still shows it.

The summary line is the diagnostic that survives a long run, so it has to be actionable on its own.
It carries a **kind** — a category, so the same reason reads the same way across procedures — and,
where the category alone says nothing a reader can act on, a **detail** naming what was specific to
that procedure. The two resolution kinds are the cases that need one: the category says resolution
failed, and only the name says which lookup to go and fix.

**A call the analyzer cannot type fails in one of two ways, and they stay apart.** `ReadDatabase`
reads every function the database has, including the ones whose return type the type map does not
cover; those are recorded under the name the lookup uses, against the unmapped type. Dropping such a
row on the way in would make it indistinguishable from a name that matches nothing, and the two need
opposite advice — `unresolved function` is fixed by the `search_path` or by creating the function,
`unmapped return type` by teaching the type map the type the message names. Collapsing them points
the reader away from the fix.

**A name is not a function.** The overloads of one name are separate entries, in catalogue order,
because what a call returns is decided from the argument types written beside it and nothing else
can tell them apart: `date_trunc` is four functions and three return types, so a name-keyed
catalogue would type every `date_trunc` in a corpus the same and be wrong about two of the three.
The parser therefore carries a call's arguments out with its name, and `DatabaseContext.ResolveFunction`
picks the overload from their types.

Resolution **narrows rather than decides**, and every step of it is built around that. An argument
is an expression, so evaluating one can reach a name the surrounding context does not carry — an
argument was never asked for its type before — and an argument that cannot be typed therefore
contributes no constraint instead of failing the call. The same goes for a declared type the type
map has no answer for, and for a declared pseudo-type, which accepts a value of any type: none of
the three may reject an overload, and the pseudo-type case scores nothing either, which is what
keeps `lower(text)` ahead of `lower(anyrange)` for a text argument. An argument that reaches its
declared type only through a cast PostgreSQL makes without being asked counts for less than one
that names it outright, because a string literal reaches a `text` argument that way and an overload
rejected over it would leave the arguments deciding nothing.

What answers where the arguments genuinely decide nothing is **catalogue order** — the last overload
that names a usable type, and only failing that the first that does not, with `specific_name`
ordering the overloads so the choice is the same on every run rather than the planner's. A candidate
carrying a type nothing can hold never displaces one that does, or every `lower()` in a corpus types
as `anyelement` on the strength of the range overload sorting last. Ranking ends on that same
ordering, so a call the arguments do not separate answers exactly as it would have without being
ranked at all.

The overload a call selects is part of what a cached analysis depends on, so a recorded callee
carries the argument types it was resolved against as well as the name, and the replay resolves
against them again. An overload appearing or disappearing beside a callee moves the inference with
nothing the cache key describes having moved, and a replay that consulted the name alone would both
miss that and refuse every entry whose call the arguments and the catalogue order disagree about.

**A pseudo-type is not a gap in the type map.** `anyelement`, `anyarray`, `record` and `void` stand
for whatever the call site resolved them to, so no mapping could describe one, and a column carrying
one is a column no wrapper can declare. `ReadDatabase` reads `typtype` and treats such a return type
as unusable — reported with the type named, exactly like a type the map does not cover, because the
reader's next step in both cases is the expression rather than the `search_path`.

**A name that resolves to no function is not by itself a drop.** Resolution yields no type, and the
call carries that absence onward; most calls sit where nothing ever asks what they return — a
predicate, a discarded argument — and those procedures analyse in full. The refusal happens at the
one place the absence would become visible, a **result column with no type**, because the report has
no way to mark a column unknown and a wrapper carrying a made-up type is indistinguishable there
from one that was really looked up. A missing wrapper method is the honest outcome, and the exit
code is what reports it.

---

### TestWrapper (Test Validation Console App)

**Location:** `TestWrapper/`

Validates generated code by consuming JSON module reports and generating C# wrappers with custom processors.

**Entry Point:** `TestWrapper/Program.cs`

**Key Files:**

| File | Purpose |
|------|---------|
| `Program.cs` | Main entry, orchestrates code generation with processor chains |
| `EncryptionCodeProcessor.cs` | Custom processor adding encryption/decryption support |

**Workflow:**

```csharp
// 1. Deserialize module
AugModule module = JsonConvert.DeserializeObject<AugModule> (json);

// 2. Generate code with processors
string code = Generator.GGenerateCode (module, processors);

// 3. Write to file
CodeGenerationUtils.EnsureFileContents (targetFile, code, lineEnding, encoding);
```

**Custom Processors:**
- `ChangeNameCodeProcessor` - Renames namespace and class
- `TaggerCodeProcessor` - Adds comments with type tags
- `EncryptionCodeProcessor` - Adds encryption/decryption for sensitive fields

**EncryptionCodeProcessor Details:**

```csharp
// Detects parameters/columns matching pattern
Regex pattern = new Regex (@"^(p_)?enc_pi_");

// Adds to generated class:
public Func<string, string> Encryptor { get; set; }
public Func<string, string> Decryptor { get; set; }

// Generated helper methods:
T ReadEncrypted<T> (IDataReader reader, int ordinal) { ... }
object WriteEncrypted<T> (T value) { ... }
```

---

### TryWrapper (Example Usage App)

**Location:** `TryWrapper/`

Example application demonstrating usage of generated database wrappers.

**Entry Point:** `TryWrapper/Program.cs`

**Key Files:**

| File | Purpose |
|------|---------|
| `Program.cs` | Usage examples calling stored procedures |
| `dbproc_sch_noda.cs` | Generated wrapper code with NodaTime support |

**Usage Example:**

```csharp
// Create wrapper with connection and encryption functions
var wrapper = new Generated.DbProc (connection, encryptor, decryptor);

// Call procedure: Schema.ProcedureName pattern
var result = wrapper.alexey.get_composite (town);
```

---

### TryPsql (Direct Npgsql Testing App)

**Location:** `TryPsql/`

Direct PostgreSQL testing without generated wrappers. Useful for debugging and exploring raw Npgsql behavior.

**Entry Point:** `TryPsql/Program.cs`

**Key Files:**
- `Program.cs` - Main entry with direct Npgsql examples
- `Program_composite.cs` - Composite type handling examples

---

## Generic Programming Model

SqlIntegrate uses a sophisticated generic type system enabling type-safe code generation with customizable mappings.

**Core Generic Template:**

```csharp
public class GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>
    where TColumn : Column, new()
    where TArgument : Argument, new()
    where TResultSet : GResultSet<TColumn>, new()
    where TProcedure : GProcedure<TColumn, TArgument, TResultSet>, new()
    where TSqlType : GSqlType<TColumn>, new()
{
    public List<TSqlType> Types { get; set; }
    public List<TProcedure> Procedures { get; set; }
}
```

**Type Hierarchy:**

```
GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>
├── Types: List<TSqlType>
│   └── GSqlType<TColumn>
│       ├── Schema, Name, IsEnum
│       └── Properties: List<TColumn>
│           └── Column (Name, DataType, Ordinal)
└── Procedures: List<TProcedure>
    └── GProcedure<TColumn, TArgument, TResultSet>
        ├── Schema, Name, ReturnType
        ├── Arguments: List<TArgument>
        │   └── Argument (Name, DataType, IsOut)
        └── ResultSets: List<TResultSet>
            └── GResultSet<TColumn>
                └── Columns: List<TColumn>
```

**Type Parameters Explained:**

| Parameter | Constraint | Purpose |
|-----------|------------|---------|
| `TSqlType` | `GSqlType<TColumn>` | SQL type definition (enum, composite) |
| `TProcedure` | `GProcedure<TColumn, TArgument, TResultSet>` | Stored procedure metadata |
| `TColumn` | `Column` | Column/property definition |
| `TArgument` | `Argument` | Procedure parameter |
| `TResultSet` | `GResultSet<TColumn>` | Procedure result set |

---

## Data Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              DATA FLOW                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   ┌─────────────────┐                                                       │
│   │   PostgreSQL    │                                                       │
│   │    Database     │                                                       │
│   └────────┬────────┘                                                       │
│            │ Npgsql queries to pg_catalog                                   │
│            ▼                                                                │
│   ┌─────────────────────────────────────┐                                   │
│   │  ReadDatabase.LoadContext()         │                                   │
│   │  (DbAnalysis/ReadDatabase.cs)       │                                   │
│   └────────┬────────────────────────────┘                                   │
│            │ creates                                                        │
│            ▼                                                                │
│   ┌─────────────────────────────────────┐                                   │
│   │  DatabaseContext                    │                                   │
│   │  (types, procedures, functions,     │                                   │
│   │   enums, composite types)           │                                   │
│   └────────┬────────────────────────────┘                                   │
│            │ consumed by                                                    │
│            ▼                                                                │
│   ┌─────────────────────────────────────┐                                   │
│   │  Analyzer.Run(cache, hash)          │                                   │
│   │  (DbAnalysis/Analyzer.cs)           │                                   │
│   │  - Parses procedure bodies          │                                   │
│   │  - Analyzes types                   │                                   │
│   │  - Builds module report             │                                   │
│   └────────┬────────────────────────────┘                                   │
│            │ produces                                                       │
│            ▼                                                                │
│   ┌─────────────────────────────────────┐                                   │
│   │  Module (JSON-serializable)         │                                   │
│   │  - Types with properties            │                                   │
│   │  - Procedures with arguments        │                                   │
│   │  - Result sets with columns         │                                   │
│   └────────┬────────────────────────────┘                                   │
│            │ written to file                                                │
│            ▼                                                                │
│   ┌─────────────────────────────────────┐                                   │
│   │  module.json                        │                                   │
│   └────────┬────────────────────────────┘                                   │
│            │ read by                                                        │
│            ▼                                                                │
│   ┌─────────────────────────────────────┐                                   │
│   │  Generator.GGenerateCode()          │                                   │
│   │  (Wrapper/Generator.cs)             │                                   │
│   │  - Builds type maps                 │                                   │
│   │  - Applies CodeProcessor chain      │                                   │
│   └────────┬────────────────────────────┘                                   │
│            │ produces                                                       │
│            ▼                                                                │
│   ┌─────────────────────────────────────┐                                   │
│   │  C# Wrapper Code                    │                                   │
│   │  (dbproc.cs)                        │                                   │
│   └────────┬────────────────────────────┘                                   │
│            │ compiled and used as                                           │
│            ▼                                                                │
│   ┌─────────────────────────────────────┐                                   │
│   │  DbProc class                       │                                   │
│   │  - Schema-organized methods         │                                   │
│   │  - Type-safe procedure calls        │                                   │
│   │  - IDataReader results              │                                   │
│   └─────────────────────────────────────┘                                   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Test Infrastructure

### Directory Structure

```
test/
├── run_test.sh              # Test orchestration script (37 lines)
├── dummy01.sql              # Primary test schema (1,320 lines)
├── dummy01_2.sql            # User-specific schema additions (15 lines)
├── correct_output.json      # Expected output for validation (2,031 lines)
├── test_points.txt          # Test coverage checklist
└── .gitignore               # Ignores temp_*.json files
```

### Test Flow (run_test.sh)

```bash
# Usage:
./run_test.sh        # Full test with database recreation
./run_test.sh -c     # Quick test without dropping/recreating database
```

**Step-by-Step Execution:**

```
┌─────────────────────────────────────────────────────────────────────┐
│                        TEST FLOW                                     │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Step 1: Database Setup (unless -c flag)                            │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  dropdb dummy01                                              │    │
│  │  createdb dummy01                                            │    │
│  │  psql -f dummy01.sql -v DBNAME=dummy01 -v owner=$USER        │    │
│  │  psql -f dummy01_2.sql (with SCHEMA/DBNAME replacements)     │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                          │                                          │
│                          ▼                                          │
│  Step 2: Generate Output File Path                                  │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  OUTPUT_JSON_FILE=temp_$(openssl rand -hex 4).json           │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                          │                                          │
│                          ▼                                          │
│  Step 3: Run ParseProcs                                             │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  ParseProcs --no-cache \                                     │    │
│  │    "host=/var/run/postgresql;database=dummy01;..." \         │    │
│  │    "$OUTPUT_JSON_FILE"                                       │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                          │                                          │
│                          ▼                                          │
│  Step 4: Post-process JSON (inject custom metadata)                 │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  sed -i 's/indirectly_used_enum"$/&, "GenerateEnum": true/'  │    │
│  │  sed -i 's/monetary"$/&, "Tag": "financial"/'                │    │
│  │  sed -i 's/city_locale"$/&, "MapTo": "TryWrapper.Town"/'     │    │
│  │  sed -i 's/mapped"$/&, "MapTo": "TryWrapper.CardType", ...'  │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                          │                                          │
│                          ▼                                          │
│  Step 5: Run TestWrapper                                            │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  TestWrapper "$OUTPUT_JSON_FILE"                             │    │
│  │  → Generates dbproc.cs and dbproc_sch_noda.cs                │    │
│  │  → Copies dbproc_sch_noda.cs to TryWrapper/                  │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                          │                                          │
│                          ▼                                          │
│  Step 6: SHA1 Validation                                            │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  sed 's/$USER/USER/g' "$OUTPUT_JSON_FILE" > normalized       │    │
│  │  sha1sum normalized == sha1sum correct_output.json           │    │
│  │  → Green: PASS (delete temp file)                            │    │
│  │  → Red: FAIL (keep temp_actual_output.json to diff)          │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                          │                                          │
│                          ▼                                          │
│  Step 7: Failure contract (database dummy01_unanalysable)           │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  ParseProcs --no-cache "...dummy01_unanalysable..." out.json │    │
│  │  → must exit non-zero and name every dropped procedure       │    │
│  │  → an unresolved function must be named as such, with the    │    │
│  │    name that failed, not bucketed as "unknown issue"         │    │
│  │  → a function that exists with an unmappable return type     │    │
│  │    must be a kind of its own, naming the type                │    │
│  │  → a function returning a pseudo-type must report the same   │    │
│  │    way, naming the pseudo-type                               │    │
│  │  ParseProcs --no-cache --tolerate-failures ...               │    │
│  │  → must exit 0                                               │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                          │                                          │
│                          ▼                                          │
│  Step 8: Generator diagnostic (a doctored report)                   │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  a corpus report with one column's type replaced by an      │    │
│  │  unmappable one, handed to TestWrapper                      │    │
│  │  → must exit non-zero, and name the procedure, the column   │    │
│  │    and the type — never a bare dictionary miss              │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                          │                                          │
│                          ▼                                          │
│  Step 9: Cache round trip (HOME redirected to a scratch dir)        │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  two runs WITHOUT --no-cache against dummy01                 │    │
│  │  → the cached run must reproduce the fresh one               │    │
│  │  → every key must be {analyzer}_{layout}_{procedure}         │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                          │                                          │
│                          ▼                                          │
│  Step 10: Callee closure (database dummy01_callee)                  │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  run, change the callee's return type, run again             │    │
│  │  → the report must follow, and match a --no-cache one        │    │
│  │  doctor the stored result, run again                         │    │
│  │  → the doctored value must come back                         │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

Every step runs before the script reports, and the script exits non-zero if any of them failed —
one run tells you everything that is wrong rather than only the first thing. Steps 7 to 10 use
databases, a report and a `HOME` of their own so they cannot disturb the corpus comparison or the
real cache.

Step 8 has to doctor a report because the analyzer refuses an unusable type on the way in, so no
fixture can put one in front of the generator. It is worth the doctoring: the lookup happens per
column, per argument and per composite property, and each has to say where the type came from.

Step 10 needs both halves. The first alone is satisfied by a cache that never hits, which would be
correct and useless; doctoring the stored result to a type the database cannot produce separates the
two, because the sentinel comes back only if the entry was served. Its fixture sets the callee's
return type explicitly rather than inheriting it, so `./run_test.sh -c` does not start from whatever
the previous run left behind.

### Test Database Schema (dummy01.sql)

The test schema provides comprehensive coverage of PostgreSQL features:

**Schemas:**
- `ext` - External schema for shared tables
- `no_proc` - Schema with types but no procedures
- `:owner` - User-specific schema (variable substitution)

**Custom Types (Enums):**

| Type | Purpose |
|------|---------|
| `app_status` | Used in procedures for status handling |
| `mapped` | Mapped to `TryWrapper.CardType` |
| `indirectly_used_enum` | Used indirectly through composite types |
| `useless_enum` | Not used (coverage for unused types) |

**Custom Types (Composites):**

| Type | Purpose |
|------|---------|
| `indirectly_used_type` | Contains enum, tests indirect usage |
| `city_locale` | Mapped to `TryWrapper.Town` |
| `monetary` | Tagged with "financial" metadata |
| `payment` | Nested composite with arrays |
| `useless_struct` | Not used (coverage test) |

**Tables:**

| Table | Features Tested |
|-------|-----------------|
| `Persons` (ext) | UUIDs, status enum, composite data |
| `Rooms` | Integer arrays |
| `Own` | Foreign key relationships |
| `Depts` | Recursive structure |
| `VoidThings` | Test data insertion |
| `financial_history` | Nested composite types |
| `sensitive` | Encrypted data handling |
| `Aggre` | Numeric type aggregations |

**Procedures (30+ total):**

| Procedure | Features Tested |
|-----------|-----------------|
| `Persons_GetAll` | Multiple result sets, ROW_NUMBER, complex JOIN |
| `GetDeptChain` | CTE, RECURSIVE, UNION, comments |
| `RoomsForPerson` | INOUT parameters, arrays, type casts, defaults |
| `get_array` | Array handling, enum_range, array operations |
| `get_value_types` | Comprehensive types, expressions, CASE |
| `get_aggregates` | GROUP BY, aggregates, OVER clauses |
| `get_operators` | ALL, ANY, BETWEEN, unary operators, IS TRUE/IS NOT TRUE |
| `test_loops` | FOR, WHILE, FOREACH with arrays, DELETE USING, = ANY(array) |
| `get_returning` | INSERT/UPDATE/DELETE with RETURNING |
| `get_dml_cte_columns` | Data-modifying CTEs as a source: RETURNING selected by name, aggregated, aliased, `RETURNING *`, joined to a table, and absent |
| `test_out` | INOUT parameters with arrays |
| `test_json` | JSON/JSONB handling |
| `get_composite` | Nested composite access with destructuring |
| `test_distinct_on_several` | `DISTINCT ON` over a list of expressions, one of them nullable |

### Test Coverage Areas (from test_points.txt)

- **SQL Types:** Tables, arguments, variables, type casts
- **Type Features:** Arrays, lengths, qualifiers
- **FROM Sources:** Table, CTE (including data-modifying, with and without RETURNING), select, function, VALUES, UNNEST
- **Combinations:** UNION, JOIN variations, DISTINCT (bare, and `DISTINCT ON` over one expression or a list), window functions
- **Name Resolution:** Conflicts, aliases, qualification
- **Array Operations:** Literals, unnest, indexing, aggregation
- **Aggregate Functions:** SUM, AVG, COUNT
- **Complex Expressions:** CASE, NULL handling, operators
- **DML with RETURNING:** INSERT, UPDATE, DELETE
- **DML Extensions:** DELETE with USING clause, = ANY(array) conditions
- **Control Flow:** FOR, WHILE, FOREACH loops
- **Parameter Passing:** IN, OUT, INOUT with arrays
- **Composite Types:** Nested access, destructuring
- **Custom Mappings:** Encryption, type mapping

### Validation Mechanism

**correct_output.json Structure:**

```json
{
  "Types": [
    {
      "Schema": "ext",
      "Name": "app_status",
      "IsEnum": true,
      "Enum": ["pending", "active", "suspended"]
    },
    {
      "Schema": "ext",
      "Name": "city_locale",
      "IsEnum": false,
      "Properties": [
        {"Name": "city", "DataType": "text"},
        {"Name": "country", "DataType": "text"}
      ],
      "MapTo": "TryWrapper.Town"
    }
  ],
  "Procedures": [
    {
      "Schema": "ext",
      "Name": "Persons_GetAll",
      "Arguments": [...],
      "ResultSets": [...]
    }
  ]
}
```

**Validation Process:**
1. Generated JSON is normalized (username -> "USER" placeholder)
2. SHA1 hash computed and compared with `correct_output.json`
3. Byte-for-byte match required for test to pass
4. On failure, temporary file retained for debugging

---

## Key Design Patterns

### 1. Generic Programming

Enables type-safe operations across the data model with customizable concrete types:

```csharp
// Base generic module
public class GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>

// Concrete implementation with extensions
public class AugModule : GModule<AugType, AugProcedure, Column, Argument, GResultSet<Column>>
```

### 2. Parser Combinators (Sprache)

SQL parsing built from composable parser functions:

```csharp
// Example from Analyzer.cs
Parser<string> Identifier =
    from first in Parse.Letter.Or(Parse.Char('_'))
    from rest in Parse.LetterOrDigit.Or(Parse.Char('_')).Many().Text()
    select first + rest;
```

**Custom Combinator Utilities** (defined in `SpracheUtils.cs`):

| Combinator | Purpose | Example Syntax |
|------------|---------|----------------|
| `.CommaDelimitedST()` | Parse comma-separated list | `expr, expr, expr` |
| `.InParentsST()` | Wrap parser in parentheses | `(content)` |
| `.InBracketsST()` | Wrap parser in brackets | `[content]` |
| `SqlToken(token)` | Match exact token, optional whitespace/comments around it | `SqlToken("order")` |
| `AnyTokenST(...)` | Match any of listed token sequences | `AnyTokenST("asc", "desc")` |

**SqlToken vs AnyTokenST Details:**

- `SqlToken(token)` - Matches a given token exactly (no spaces/comments allowed inside), possibly surrounded by spaces (tabs, newlines, etc.) or comments. Example: `SqlToken("order")` matches the word "order" with optional whitespace around it.

- `AnyTokenST(tokens...)` - Matches any of the given sequences. Each sequence is split into words, where each word is a SqlToken. The "ST" suffix means spaces/comments are allowed between and around words. Example: `AnyTokenST("order by", "group by")` matches `ORDER BY`, `order  by`, `ORDER/*comment*/BY`, etc.

- `AnyTokenST("( )")` - Matches empty parentheses with optional space/comments between. Useful for empty argument lists like `mode()` or `mode( )`.

**Suffix Naming Convention:**
- `ST` suffix = "Space/Tab-aware" - parser handles whitespace and comments inside and around the construct

**Standard Sprache Combinators:**

| Combinator | Purpose |
|------------|---------|
| `.AtLeastOnce()` | Require 1+ matches |
| `.Many()` | Zero or more matches |
| `.Optional()` | Zero or one match |
| `.Or()` | Alternative parser |
| `.DelimitedBy()` | Items separated by delimiter |

**Idiomatic Function-Like Parsing:**

```csharp
// For: function_name(arg1, arg2, ...)
from f in AnyTokenST ("greatest", "least")
from args in PExpressionRefST.Get.CommaDelimitedST ().InParentsST ()
where args.Count () >= 2
select (Func<RequestContext, NamedTyped>)(rc =>
{
    return args.First ().GetResult (rc).WithName (f);
})
```

### A CTE is a source, whatever statement produces it

A CTE level is resolved to a table and pushed into the request context before the next
level and the body are resolved. That holds for a **data-modifying** CTE too — PostgreSQL
allows `INSERT`/`UPDATE`/`DELETE` inside `WITH`, and the statement's `RETURNING` list is the
CTE's column list:

```sql
WITH d AS (DELETE FROM t WHERE t.id = $1 RETURNING t.payload, t.kind)
SELECT COUNT (*)::int AS affected, MIN (d.payload) AS payload
FROM d
```

`PInsertFullST`, `PUpdateFullST` and `PDeleteFullST` each yield a `FullSelectStatement`
describing that list over the modified table, or `null` when the statement has no
`RETURNING` at all. `PCteLevelDmlRefST` carries that value; discarding it would let the
statement parse and then fail every reference to the CTE with `Not found d.payload`, which
reads as a missing column rather than as a source that was never given one.

The DML branch resolves through the whole `FullSelectStatement` rather than its `SelectBody`,
because a data-modifying statement may itself carry a `WITH` that has to stay in scope while
its `RETURNING` list is typed.

A data-modifying CTE with **no** `RETURNING` contributes an empty column list rather than a
refusal. It is legal SQL, and the query around it may still count its rows.

### 3. Chain of Responsibility (CodeProcessor)

Extensible code generation through processor chain:

```csharp
// Processor chain in TestWrapper
var processors = new List<IGCodeProcessor>
{
    new GNodaTimeCodeProcessor<...>(),    // NodaTime types
    new TaggerCodeProcessor<...>(),       // Add tags
    new EncryptionCodeProcessor<...>()    // Encryption
};

string code = Generator.GGenerateCode(module, processors);
```

### 4. Database Introspection

Runtime schema discovery via PostgreSQL catalog:

```csharp
// Queries pg_catalog for:
// - pg_type: Type definitions
// - pg_proc: Procedures/functions
// - pg_attribute: Columns
// - pg_enum: Enum values
```
