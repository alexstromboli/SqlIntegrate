# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build entire solution
dotnet build SqlIntegrate.sln

# Build specific project
dotnet build ParseProcs/ParseProcs.csproj

# Clean build
dotnet clean SqlIntegrate.sln
```

## Running Tests

```bash
cd test/
./run_test.sh        # Full test with database recreation
./run_test.sh -c     # Quick test without dropping/recreating database
```

The test suite creates a PostgreSQL database `dummy01`, loads schema from `dummy01.sql`, runs ParseProcs to generate JSON analysis, then generates C# wrapper code via TestWrapper. Output is validated against `correct_output.json` via SHA1 hash.

## Architecture

SqlIntegrate analyzes PostgreSQL databases and generates type-safe C# wrapper code for stored procedures and functions.

**Data Flow:**
```
PostgreSQL Database → ReadDatabase.LoadContext() → Analyzer (DbAnalysis)
→ JSON Module Report → Generator.GGenerateCode<T>() → CodeProcessor chain → C# Wrapper Code
```

**Solution Projects:**
- **DbAnalysis** - Core library: SQL parsing (Sprache), PostgreSQL type system, database introspection
- **Wrapper** - Code generation engine: Generator.cs produces C# wrappers, CodeProcessor chain for extensibility
- **ParseProcs** - Console app: Entry point that reads PostgreSQL databases and generates JSON analysis
- **TestWrapper** - Console app: Validates generated code
- **TryWrapper** - Console app: Example usage of generated database wrappers
- **TryPsql** - Console app: Direct Npgsql testing without wrappers

**Key Design Patterns:**
- Generic programming with template-based data models (`GModule<TSqlType, TProcedure, TColumn, TArgument, TResultSet>`)
- Parser combinators (Sprache) for SQL parsing in `Analyzer.cs`
- Chain of responsibility via `CodeProcessor` handlers for code generation hooks

**Core Classes:**
- `Analyzer` (DbAnalysis/Analyzer.cs) - SQL parser with expression parsing, CASE expressions, function calls
- `Generator.GGenerateCode<T>()` (Wrapper/Generator.cs) - Main code generation with type mapping
- `PSqlType` (DbAnalysis/PSqlType.cs) - PostgreSQL type metadata and relationships
- `DatasetStructs.cs` - Generic container types for procedures, columns, result sets

## Naming Conventions

- Use PascalCasing for all names (classes, methods, properties). Possible exception is for small scope variables, that can have short all-lowercase names, or for names that come from other sources, like third party APIs and SDKs.
- Generic type parameters prefixed with T (e.g., `TSqlType`, `TProcedure`)
- Small scope variables may use short lowercase names
- Always put a space between method name and opening brace after it.
- Always take instruction blocks in curly braces even if they are just one instruction (like following 'if', 'while', 'for', etc).

## Shortcuts for Commands

- 'ggc' will stand for 'Give git commit'. This means, summarize all the changes and print a 'git commit -m' command, which I can review and use to commit the changes. Claude should not perform commit, only print a command line on screen. Message body must be given as a string literal, not a heredoc. No '-a' flag should be used, only '-m'.
  
- 'stru' will mean structurize the following knowledge about the project (its business domain, architecture, development tooling, procedures, styling, etc), and incorporate pieces of it accordingly into CLAUDE.md, or .md files linked therein, or make a new file in 'architecture' folder and link it up from existing .md file.

- 'iad' will mean 'Implement and adjust documents', i.e. implement the latest discussed points in code, and then adjust documents to reflect that.
