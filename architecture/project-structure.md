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
- `DbAnalysis.Cache` - Procedure analysis caching (HashUtils, IProcedureStateCache, LocalUserCache, VoidCache)
- `DbAnalysis.Sources` - Source tracking (ISource, Sourced, TableSource, FunctionSource, CompositeTypeSource, CalculatedSource, TextSpanSource, DefinitionSource)

**SQL Parsing Infrastructure:**

| File | Purpose |
|------|---------|
| `SpracheUtils.cs` | Parser combinator utilities |
| `CustomInput.cs` | Input stream for parser |
| `TextSpan.cs` | Source location tracking |
| `SelectStatement.cs` | SELECT statement parsing |
| `OrdinarySelect.cs` | Simple SELECT handling |
| `FullSelectStatement.cs` | Complex SELECT with CTEs, UNION |
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
ParseProcs --no-cache "host=/var/run/postgresql;database=mydb;Integrated Security=true" output.json
```

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
│  │  → Red: FAIL (keep temp file for debugging)                  │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

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
| `get_operators` | ALL, ANY, BETWEEN, unary operators |
| `test_loops` | FOR, WHILE, FOREACH with arrays |
| `get_returning` | INSERT/UPDATE/DELETE with RETURNING |
| `test_out` | INOUT parameters with arrays |
| `test_json` | JSON/JSONB handling |
| `get_composite` | Nested composite access with destructuring |

### Test Coverage Areas (from test_points.txt)

- **SQL Types:** Tables, arguments, variables, type casts
- **Type Features:** Arrays, lengths, qualifiers
- **FROM Sources:** Table, CTE, select, function, VALUES, UNNEST
- **Combinations:** UNION, JOIN variations, DISTINCT, window functions
- **Name Resolution:** Conflicts, aliases, qualification
- **Array Operations:** Literals, unnest, indexing, aggregation
- **Aggregate Functions:** SUM, AVG, COUNT
- **Complex Expressions:** CASE, NULL handling, operators
- **DML with RETURNING:** INSERT, UPDATE, DELETE
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
