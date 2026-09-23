# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build ROP.sln

# Run all tests (once per target framework)
dotnet run --project Test/Test.fsproj --framework net8.0
dotnet run --project Test/Test.fsproj --framework net10.0

# Run tests matching a name fragment (Expecto --filter)
dotnet run --project Test/Test.fsproj --framework net8.0 -- --filter "Returns - bind"

# Publish the release DLL (optimized Release build, output to ./publish/<tfm>)
dotnet publish ROP/ROP.fsproj --configuration Release --framework net8.0 --output ./publish/net8.0
dotnet publish ROP/ROP.fsproj --configuration Release --framework net10.0 --output ./publish/net10.0
```

The library and test project multi-target `net8.0` (LTS) and `net10.0`, so commands that build for both (`dotnet build`) don't need `--framework`, but `dotnet run` and `dotnet publish` are ambiguous across multiple target frameworks and require `--framework net8.0` or `--framework net10.0` to pick one. `LangVersion` is pinned per target framework in the `.fsproj` files (`8.0` for `net8.0`, `10.0` for `net10.0`) so each build only uses language features available at that framework's release.

## Architecture

An F# library (multi-targets .NET 8 LTS and .NET 10) implementing Railway-Oriented Programming around a custom `Returns<'TSuccess,'TMessage>` type — richer than the standard `Result` type because it tracks warnings alongside the success value.

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
  - *Collection*: `traverseList`/`traverseArray` (accumulate all failures), `traverseListFailFast`/`traverseArrayFailFast` (stop at the first), `sequenceList`, `partition`, `fold`
  - *Warnings/context*: `warnIfLazy`, `warnIfWith`, `dedupeWarnings`, `summariseWarnings`, `withContextBy`
  - *Post-conditions/recovery*: `filter`, `filterWith`, `recover`

- **`Testing.fs`** — `ROP.Testing`: framework-agnostic assertions for tests (`getOrFail`, `expectFailure`, `expectNoWarnings`, ...); failures raise a plain exception rendering every message.

- **`Validation.fs`** — a higher-level DSL for record/property validation. Entry point: `createValidatorFor<'T>()`. Provides combinators like `validate`, `validateWhen`, `validateRequired`, and primitive checks (`isGreaterThan`, `isNotEmpty`, `isNotEmptyOrWhitespace`).

- **`Result.Extension.fs`**, **`Choice.Extension.fs`**, **`Option.Extension.fs`** — standalone utility modules extending the standard F# `Result`, `Choice`, and `option` types; usable independently of `Returns`.

### Two Composition Strategies

The library draws a deliberate distinction:

1. **Sequential** (`>>=`, `>=>`) — short-circuits at the first failure; warnings accumulated before the failure are preserved in the `Failure` case.
2. **Parallel** (`&&&`, `<*>`, `and!`) — all branches run regardless of individual failures; all error lists are concatenated.

The `returns { ... }` CE supports `and!` for parallel binding, unlike standard `let!` which is sequential.

### Tests

`Test/Program.fs` uses Expecto. Tests are grouped by topic (e.g., `Returns - Creation`, `Returns - &&&`, `Validation - ValidatorBuilder`, `Integration - End-to-end`). The executable collects all lists into a single `All Tests` suite and prints a tree-structured report.
