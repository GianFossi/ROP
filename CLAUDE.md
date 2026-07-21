# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build ROP.sln

# Run all tests
dotnet run --project Test/Test.fsproj

# Run tests matching a name fragment (Expecto --filter)
dotnet run --project Test/Test.fsproj -- --filter "Returns - bind"

# Publish the release DLL (optimized Release build, output to ./publish)
dotnet publish ROP/ROP.fsproj --configuration Release --output ./publish
```

## Architecture

An F# library (.NET 8) implementing Railway-Oriented Programming around a custom `Returns<'TSuccess,'TMessage>` type — richer than the standard `Result` type because it tracks warnings alongside the success value.

### Core Type

`Returns<'TSuccess, 'TMessage>` (defined in `ROP/Returns.fs`) has two states:

```fsharp
| Success of 'TSuccess * 'TMessage list   // value + zero-or-more warnings
| Failure of 'TMessage list               // one-or-more errors
```

### Module Overview

- **`Returns.fs`** — the core type, all combinators, operators, and the `ReturnsBuilder` CE (`returns { ... }`). Operator groups:
  - *Sequential* (short-circuit on first failure): `>>=` (bind), `>=>` / `<=<` (Kleisli)
  - *Applicative*: `<!>` (map), `<*>` (apply)
  - *Parallel* (accumulate all failures): `&&&`, `validateAll`
  - *Collection*: `traverseList`, `sequenceList`, `partition`, `fold`

- **`Validation.fs`** — a higher-level DSL for record/property validation. Entry point: `createValidatorFor<'T>()`. Provides combinators like `validate`, `validateWhen`, `validateRequired`, and primitive checks (`isGreaterThan`, `isNotEmpty`, `isNotEmptyOrWhitespace`).

- **`Result.Extension.fs`**, **`Choice.Extension.fs`**, **`Option.Extension.fs`** — standalone utility modules extending the standard F# `Result`, `Choice`, and `option` types; usable independently of `Returns`.

### Two Composition Strategies

The library draws a deliberate distinction:

1. **Sequential** (`>>=`, `>=>`) — short-circuits at the first failure; warnings accumulated before the failure are preserved in the `Failure` case.
2. **Parallel** (`&&&`, `<*>`, `and!`) — all branches run regardless of individual failures; all error lists are concatenated.

The `returns { ... }` CE supports `and!` for parallel binding, unlike standard `let!` which is sequential.

### Tests

`Test/Program.fs` uses Expecto. Tests are grouped by topic (e.g., `Returns - Creation`, `Returns - &&&`, `Validation - ValidatorBuilder`, `Integration - End-to-end`). The executable collects all lists into a single `All Tests` suite and prints a tree-structured report.
