# ROP (Railway-Oriented Programming in F#)

This repository contains an F# library that implements a Railway-Oriented Programming model around a custom `Returns<'TSuccess,'TMessage>` type, plus helper modules, validation utilities, examples, and an Expecto test suite.

[![CI](https://github.com/GianFossi/ROP/actions/workflows/ci.yml/badge.svg)](https://github.com/GianFossi/ROP/actions/workflows/ci.yml)
[![Publish NuGet](https://github.com/GianFossi/ROP/actions/workflows/publish.yml/badge.svg)](https://github.com/GianFossi/ROP/actions/workflows/publish.yml)

## What this codebase provides

- A custom **result container** with two states, richer than the stock `Result` type because it also tracks non-fatal warnings:
  - `Success(value, warnings)`
  - `Failure(errors)`
- Functional combinators to compose validations and business rules:
  - sequential (`>>=`, `>=>`, `<=<`)
  - applicative (`<!>`, `<*>`)
  - parallel validation (`&&&`, `and!` in the computation expression)
- Utility extensions for standard F# types, each independently usable:
  - `Result` extensions
  - `Choice` extensions
  - `Option` extensions
- A separate `Validation` module for record/property-level validators.

---

## What's new in 1.1.0

This release only adds; code written against 1.0.x compiles unchanged.

- `warnIfLazy` builds the warning message only when the predicate holds.
- `ROP.Testing` provides test assertions that work with any test framework.
- `dedupeWarnings` and `summariseWarnings` collapse repeated warnings.
- `traverseListFailFast`, `traverseArray` and `traverseArrayFailFast` complement `traverseList`, which accumulates every failure.
- `withContextBy` builds breadcrumb trails on failures.
- `ofPlainResult` and `toPlainResult` convert to and from plain `Result`.
- `bind` and `>>=` allocate about 4× less per step (see [Zero-allocation paths](#zero-allocation-paths)).

---

## Prerequisites

To **build, test, or reference** this library you need:

| Requirement | Notes |
| --- | --- |
| **.NET 10 SDK** (`10.0.x`), plus the **.NET 8 runtime** | The library multi-targets `net8.0` (LTS) and `net10.0`. The .NET 10 SDK builds both, and running the `net8.0` tests needs the .NET 8 runtime (or install both SDKs). F# tooling (`dotnet fsi`, the F# compiler) ships bundled with the .NET SDK — no separate install is required. |
| **Git** | To clone the repository and (if publishing) to tag releases. |
| An editor with F# support (optional, but recommended) | [VS Code](https://code.visualstudio.com/) + [Ionide](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp), **Visual Studio 2022** (17.8+) with the *".NET desktop development"* workload, or **JetBrains Rider**. |

To **publish a new version to NuGet.org via the GitHub Actions workflow** (recommended path) you additionally need:

| Requirement | Notes |
| --- | --- |
| A [nuget.org](https://www.nuget.org/) account | Used to configure a Trusted Publishing policy — no API key needed for this path. |
| A Trusted Publishing policy on nuget.org | See [Option A](#option-a--automated-via-github-actions-recommended) below for the exact fields. |
| Push access to this GitHub repository | Needed to push a version tag that triggers the publish workflow, and to configure the `NUGET_USER` repository secret. |

To **publish manually from the command line** instead (Option B below), you need a classic NuGet API key rather than Trusted Publishing: create one at nuget.org → your profile → *API Keys* → *Create*, scoped to the `Ganfoss.ROP` package (or "push new packages and package versions" while it doesn't exist yet).

Verify your local setup with:

```bash
dotnet --version        # should print 10.0.x
dotnet --list-sdks
dotnet --list-runtimes  # should include Microsoft.NETCore.App 8.0.x and 10.0.x
```

### What the PC administrator has to do

On a locked-down corporate machine, the following steps typically require **administrator/elevated rights** and should be done once per machine:

1. **Install the .NET 10 SDK and the .NET 8 runtime.**
   - Windows: `winget install Microsoft.DotNet.SDK.10` and `winget install Microsoft.DotNet.Runtime.8` (or the MSI installers from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)). The installer writes to `Program Files` and registers the SDK machine-wide, which needs admin rights.
   - The installer also adds `dotnet` to the **system** `PATH`; a user-level install (`dotnet-install.ps1 -InstallDir <user folder>`) is possible without admin rights but then `PATH` must be adjusted per user.
2. **Allow outbound HTTPS access** (through the corporate firewall / proxy) to:
   - `api.nuget.org` and `www.nuget.org` — required for `dotnet restore` and for publishing packages.
   - `github.com` and `objects.githubusercontent.com` — required to clone/pull and for GitHub Actions runners (if self-hosted).
3. **Configure a corporate NuGet proxy/feed**, if the organization mandates one, by adding a `NuGet.Config` (solution-level or `%APPDATA%\NuGet\NuGet.Config`) pointing at the internal feed, with any required authentication (PAT, API key, or Windows auth).
4. **(Optional) Install the IDE** — Visual Studio 2022 (with the F#/".NET desktop development" workload) or VS Code — both typically require admin rights to install machine-wide, though VS Code also offers a per-user installer that does not.
5. **Grant the developer permission to set environment variables / repository secrets**, if the publish workflow is to be used:
   - A machine/user environment variable (e.g. holding a classic API key) if publishing manually from the command line.
   - Or, for the CI publish workflow (which uses Trusted Publishing, not a stored API key), a **GitHub repository secret** named `NUGET_USER` holding the nuget.org username (Settings → Secrets and variables → Actions → New repository secret) — this requires *admin/maintainer* rights on the GitHub repository, not on the PC itself.

None of the day-to-day `dotnet build` / `dotnet test` / `dotnet pack` commands below require admin rights once the SDK is installed and network access is allowed.

---

## Working philosophy

This library follows a small number of deliberate design principles — understanding them makes the API predictable rather than a grab-bag of combinators:

1. **Errors are values, not exceptions.** Business/validation failures are ordinary data flowing through an explicit "error track", not `try`/`with`. Exceptions are reserved for truly exceptional, unrecoverable conditions (see `Returns.tryCatch` / `Result.tryCatch` for the boundary where real exceptions get converted into values).
2. **Warnings are first-class, not just errors-in-waiting.** Most `Result`-style libraries only have "success" and "failure". `Returns<'TSuccess,'TMessage>` adds a third dimension: a `Success` can still carry non-fatal `warnings` — useful for "this succeeded, but here's something you should know" (deprecation notices, best-effort corrections, near-limit alerts, etc.) without forcing a failure.
3. **Two composition strategies, chosen explicitly — never accidentally mixed:**
   - **Sequential** (`>>=`, `>=>`, `<=<`, `let!` in `returns { }`) — short-circuits on the first failure. Use this when step 2 genuinely depends on step 1 having succeeded (e.g. "parse the input, *then* look it up in the database").
   - **Parallel** (`&&&`, `<*>`, `and!` in `returns { }`) — every branch always runs, and *all* failures are collected. Use this for independent checks you want to report all at once (e.g. form validation: don't make the user fix one field, resubmit, then discover the next error).

   Picking the wrong one is a common bug class in hand-rolled validation code (accidentally short-circuiting form validation so users only ever see one error at a time, or accidentally running dependent steps in parallel). This library makes the choice a one-operator decision (`>>=` vs `&&&`) instead of an accident of control flow.
4. **Composition over inspection.** Prefer gluing small `'a -> Returns<'b,'msg>` "switch functions" together with operators (`>=>`, `&&&`, `bind`) over repeatedly pattern-matching on `Success`/`Failure` in application code. Pattern matching (or the `Pass|Warn|Fail` active pattern) is for the edges of your system — where you finally need to *do* something with the result — not for the middle of a pipeline.
5. **Small, independent extension layers.** `Result.Extension.fs`, `Choice.Extension.fs`, and `Option.Extension.fs` are not built on top of `Returns` — they are self-contained utility modules for the *standard* F# `Result`, `Choice`, and `option` types (largely mirroring [FSharpPlus](https://github.com/fsprojects/FSharpPlus)). Use them on their own in code that doesn't need warnings at all; reach for `Returns` only when you specifically need the warning channel or its accumulating combinators.
6. **Accumulate errors is a design decision, not a default.** `Returns`'s error channel is always a list, so accumulation (`&&&`, `apply`, `merge`, `validateAll`, `traverseList`) is built-in. The plain `Result<'T,'Error>` extensions, by contrast, are first-error-wins by default (matching `Result`'s usual semantics) — the `...With` combinators (`apply2With`, `map2With`, `zipWith`, …) exist specifically for callers who want accumulation on a raw `Result<'T,'Error list>` without switching to `Returns`.
7. **No hidden mutation, no hidden I/O.** Every combinator in `Returns.fs`/`Validation.fs` is a pure function over its inputs. Side effects (logging, telemetry) are opt-in via `tee`/`successTee`/`failureTee`/`log`, which always return the original value unchanged — so adding a log line never changes your pipeline's behavior.

---

## Technology stack

- **Language**: F#
- **Runtime**: .NET 8 LTS and .NET 10 (`TargetFrameworks: net8.0;net10.0`; `LangVersion` pinned per framework)
- **Library project**: `ROP/ROP.fsproj` (NuGet package id `Ganfoss.ROP`)
- **Tests**: Expecto (`Test/Test.fsproj`)
- **Solution**: `ROP.sln`

---

## Repository structure

```text
ROP/
├── ROP.sln
├── README.md
├── CLAUDE.md
├── .github/workflows/
│   ├── ci.yml                 # build + test + upload DLL artifact on every push/PR
│   └── publish.yml            # pack + publish to NuGet.org + GitHub Release on a `v*` tag
├── ROP/
│   ├── ROP.fsproj
│   ├── Returns.fs             # core type + operators + `returns { }` CE builder
│   ├── Testing.fs             # framework-agnostic test assertions (`ROP.Testing`)
│   ├── Validation.fs          # record/property validator DSL
│   ├── Result.Extension.fs    # standalone Result<'T,'Error> helpers
│   ├── Choice.Extension.fs    # standalone Choice<'T,'U> helpers
│   ├── Option.Extension.fs    # standalone option<'T> helpers
│   ├── Example.Result.fsx
│   ├── Examples.Returns.General.fsx
│   ├── Examples.Returns.Validation.Series.fsx
│   ├── Examples.Returns.Validation.Parallel.1.fsx
│   ├── Examples.Returns.Validation.Parallel.2.fsx
│   └── Examples.Returns.HeatExchanger.fsx
├── docs/
│   └── ZERO-ALLOC.md          # hot paths vs ROP: measured costs and the boundary
├── Bench/
│   └── AllocProbe/            # allocation probe backing ZERO-ALLOC.md (not in ROP.sln)
├── Test/
│   ├── Test.fsproj
│   └── Program.fs             # Expecto test suite (267+ tests)
└── Setup/
    └── Setup.vdproj           # legacy Visual Studio Installer project (not part of the build)
```

---

## How the core library is organized

### 1) `Returns.fs` (core)

`Returns.fs` is the central file. It defines:

- `type Returns<'TSuccess,'TMessage> = Success of ... | Failure of ...`
- Module `Returns` with constructors, transformations, composition, and utility helpers.
- Operators under `Returns.Operators`.
- `ReturnsBuilder` computation expression (`returns { ... }`) with support for `and!` parallel binding.

#### Key functional groups inside `Returns`

- Creation: `ok`, `warn`, `warnmany`, `fail`, `failmany`
- Classification: `isSucceeded`, `isFailure`, `hasWarnings`
- Conversion: `toOption/ofOption`, `toChoice/ofChoice`, `toResult/ofResult` (Ok carries `value * warnings`), `ofPlainResult/toPlainResult` (plain `Result<'T,'M>` / `Result<'T,'M list>`; **`toPlainResult` discards warnings**)
- Composition:
  - Sequential: `bind`, `compose`, `>>=`, `>=>`, `<=<`
  - Applicative: `apply`, `map`, `map2`, `map3`, `map4`, `<!>`, `<*>`
  - Parallel: `plus`, `&&&`, `validateAll`
- Message handling: `jointMessages`, `mapMessages`, `mapWarnings`, `mapErrors`, `warnIf`, `warnIfLazy` (message built only when the predicate holds)
- Warning aggregation: `dedupeWarnings` (drop repeats, keep first-occurrence order), `summariseWarnings` (group by key, with counts)
- Failure provenance: `withContextBy` (annotate every error with a context label, building a breadcrumb trail)
- Collection helpers: `partition`, `zip`, `fold`, and the traversals:

  | | Accumulates every failure (validation) | Stops at the first failure (sequential computation) |
  | --- | --- | --- |
  | list | `traverseList`, `sequenceList` | `traverseListFailFast` |
  | array | `traverseArray` | `traverseArrayFailFast` |

  The array forms write into a pre-sized array instead of building and reversing an intermediate list.

#### Testing helpers (`ROP.Testing`)

Assertion helpers that don't depend on any test framework. On a mismatch they raise a plain exception whose message renders **every** message in the container, which xUnit, NUnit and Expecto all report as a failure:

```fsharp
open ROP.Testing

getOrFail r                                 // value, or fail with every error listed
getWithWarnings r                           // value * warnings
expectFailure r                             // the error list
expectFailureMatching (function NotFound _ -> true | _ -> false) r
expectWarningMatching ((=) Extrapolated) r  // value, if some warning matches
expectNoWarnings r                          // value, if a Success with no warnings
```

#### Hot paths

See [Zero-allocation paths](#zero-allocation-paths) below.

---

### 2) `Validation.fs` (validator DSL)

`Validation.fs` defines a validator builder for richer object/record validation:

- `ValidationItem`, `ValidationState` (`Ok | Errors`)
- Validator combinators for scalar, optional, collection, and nested values
- `createValidatorFor<'T>() { ... }` DSL

Example capabilities include:

- `validate`, `validateWhen`
- `validateRequired`, `validateUnrequired`, `validateRequiredWhen`, `validateUnrequiredWhen`
- `validateSingleCaseUnion`, `validateUnion`
- Primitive validators like `isGreaterThan`, `isNotEmpty`, `isNotEmptyOrWhitespace`, `hasLengthOf`, `eachItemWith`
- Composition helpers `withFunction`, `withValidator`, `withValidatorWhen` for nesting child-record validators

---

### 3) Extension modules

- `Result.Extension.fs`: additional helpers for `Result<'T,'E>` — `defaultValue`, `either`, `apply`, `mapError`, `bimap`, `bindError`/`catch`, `toChoice`, `zip`/`zip3`/`unzip`/`unzip3`, accumulating `apply2With`/`map2With`/`zipWith` combinators, and the `Check` module of smart-constructor-style validators.
- `Choice.Extension.fs`: helpers for `Choice<'T,'E>` — `apply`, `map2`, `map3`, `bind`, `bindChoice2Of2`, `either`, accumulating `apply2With`/`apply3With`.
- `Option.Extension.fs`: helpers for `option<'T>` — `apply`, `zip`/`zip3`, `either`, `toResultWith`, `protect`.

These modules are independent utility layers and can be used outside the `Returns` workflow.

---

## Examples

All snippets below are runnable as-is (given `open ROP`) and are drawn from the `.fsx` scripts and the Expecto test suite, so they reflect the library's actual current behavior.

### 1. Creating `Returns` values

```fsharp
open ROP

Returns.ok<int,string> 10                          // Success (10, [])
Returns.warn<int,string> "near the limit" 10        // Success (10, ["near the limit"])
Returns.warnmany<int,string> ["w1"; "w2"] 10        // Success (10, ["w1"; "w2"])
Returns.fail<int,string> "boom"                     // Failure ["boom"]
Returns.failmany<int,string> ["e1"; "e2"]           // Failure ["e1"; "e2"]
```

### 2. Sequential composition (`>>=`, `>=>`) — short-circuit on first failure

```fsharp
open System
open ROP

type Messages =
    | IsNegativeValue
    | AbsoluteValueExceed of int
    | IsOdd
    | IsEven

let checkIsEven (x:int) =
    if x % 2 = 0 then Returns.fail Messages.IsEven
    else Returns.warn Messages.IsOdd x

let checkIsNegative (x:int) =
    if x < 0 then Returns.fail Messages.IsNegativeValue
    else Returns.ok x

let checkIsLargerThan10 (x:int) =
    if Math.Abs(x) > 10 then Returns.fail (Messages.AbsoluteValueExceed 10)
    else Returns.ok x

// Piping values through binds:
Returns.ok 9 >>= checkIsEven >>= checkIsNegative >>= checkIsLargerThan10
// Success (9, [IsOdd])

Returns.ok -20 >>= checkIsEven >>= checkIsNegative >>= checkIsLargerThan10
// Failure [IsNegativeValue; IsOdd]   <-- warnings collected so far are kept even on failure

// Or compose the switch functions themselves (point-free, "Kleisli" composition):
let pipeline = checkIsEven >=> checkIsNegative >=> checkIsLargerThan10
-11 |> pipeline    // Failure [IsNegativeValue; IsOdd]
```

### 3. Parallel composition (`&&&`) — accumulate every failure

```fsharp
open ROP

type Request = { name: string; email: string }

let validateNameNotBlank (r: Request) =
    if r.name = "" then Returns.fail "Name must not be blank" else Returns.ok r

let validateNameLength (r: Request) =
    if r.name.Length > 10 then Returns.fail "Name must not be longer than 10 chars" else Returns.ok r

let validateEmailNotBlank (r: Request) =
    if r.email = "" then Returns.fail "Email must not be blank" else Returns.ok r

let validateRequest = validateNameNotBlank &&& validateNameLength &&& validateEmailNotBlank

validateRequest { name = "Alice Vien Dal Mare"; email = "" }
// Failure ["Name must not be longer than 10 chars"; "Email must not be blank"]
// -- both problems reported in one pass, instead of only the first one.

validateRequest { name = "Alice"; email = "good" }
// Success ({ name = "Alice"; email = "good" }, [])
```

### 4. Applicative construction (`<!>`, `<*>`) — validate constructor arguments independently

```fsharp
open ROP

let checkX x = if x >= 0 then Returns.ok x else Returns.fail "x<0!"
let checkY y = if y >= 0 then Returns.ok y else Returns.fail "y<0!"

let create x y =
    fun a b -> (a, b)
    <!> checkX x
    <*> checkY y

create  10  20   // Success ((10, 20), [])
create -10  20   // Failure ["x<0!"]
create -10 -20   // Failure ["x<0!"; "y<0!"]   <-- both arguments' errors accumulated
```

### 5. The `returns { }` computation expression — `let!` (sequential) vs `and!` (parallel)

```fsharp
open ROP

// Sequential: short-circuits on the first failure.
let sequential : Returns<int,string> =
    returns {
        let! x = Returns.warn "wx" 3
        let! y = Returns.ok (x + 1)
        return x + y
    }
// Success (7, ["wx"])

// Parallel: both branches always run, and both branches' warnings/errors accumulate.
let parallel : Returns<int,string> =
    returns {
        let! x = Returns.warn "wx" 3
        and! y = Returns.warn "wy" 4
        return x + y
    }
// Success (7, ["wx"; "wy"])

let bothFail : Returns<int,string> =
    returns {
        let! _ = Returns.fail "e1"
        and! _ = Returns.fail "e2"
        return 0
    }
// Failure ["e1"; "e2"]   <-- and! never short-circuits, unlike let!
```

### 6. Record/property validation DSL (`Validation.fs`)

```fsharp
open ROP.Validation

type Person = { name: string; age: int; email: string option }

let validatePerson = createValidatorFor<Person>() {
    validate (fun p -> p.name) [ isNotEmptyOrWhitespace ]
    validate (fun p -> p.age)  [ isGreaterThanOrEqualTo 0; isLessThan 150 ]
    validateRequiredWhen (fun p -> p.age > 18) (fun p -> p.email) [ isNotEmptyOrWhitespace ]
}

validatePerson { name = "Alice"; age = 30; email = Some "alice@test.com" }
// Ok

validatePerson { name = ""; age = 200; email = None }
// Errors [ { property = "name"; errorCode = "isNotEmptyOrWhitespace"; ... }
//          { property = "age";  errorCode = "isLessThan"; ... }
//          { property = "email"; errorCode = "validatorRequired"; ... } ]
// -- every failing property reported at once, not just the first one.
```

### 7. Standalone `Result`/`Option` extensions (no `Returns` involved)

```fsharp
open ROP   // brings Result / Choice / Option extension modules into scope

// Recover from an Error instead of just transforming it:
let withFallback = Result.bindError (fun _ -> Ok 0) (Error "not found")
// Ok 0

// Accumulate errors on a plain Result<'T, string list> without switching to Returns:
let combined =
    Result.apply2With (@) (fun a b -> a + b) (Error ["e1"]) (Error ["e2"])
// Error ["e1"; "e2"]

// Option parity helper:
Option.either (fun x -> string x) (fun () -> "none") (Some 42)   // "42"
```

More end-to-end scenarios (including a realistic domain example with layered, remapped error messages) live in `ROP/Examples.Returns.HeatExchanger.fsx`.

---

## Zero-allocation paths

The full design note is [docs/ZERO-ALLOC.md](docs/ZERO-ALLOC.md). In short:

**Decision:** there is no `[<Struct>]` variant of `Returns`. Code that must not allocate per evaluation (per node, per iteration, per integration point) does not use ROP. It uses plain struct values and a `[<Flags>]` diagnostics enum, and converts to `Returns` **once**, at the edge (per solve or request).

**Why not a struct variant.** A struct prototype allocates nothing on the clean path, but:

- every warning still allocates a list cell and usually the message too;
- making warnings allocation-free means a flags enum, which is what the kernels already have;
- the whole API (operators, CE, traversals, `Validation`) would have to be duplicated.

**What `Returns` costs**, in bytes allocated per call, measured with `Bench/AllocProbe`:

| Scenario | v1.0.2 net8.0 | v1.0.2 net10.0 | v1.1.0 net8.0 | v1.1.0 net10.0 |
| --- | ---: | ---: | ---: | ---: |
| `Returns.ok x` | 32 B | 32 B | 32 B | 32 B |
| `ok x >>= s >>= s >>= s`, no warnings | 560 B | 408 B | **128 B** | **128 B** |
| `returns { let! … ×3 }`, no warnings | 448 B | 349 B | **160 B** | **160 B** |
| `warnIf` (false) vs `warnIfLazy` (false), interpolated message | 312 B | 312 B | 312 B → **32 B** | 312 B → **32 B** |

Since v1.1.0, a clean pipeline costs one `Success` (32 B) per step on both runtimes. That is the minimum for a reference-type union.

**The boundary:**

| Layer | Runs | Uses |
| --- | --- | --- |
| Kernel | per node / iteration | struct state + flags enum, combined with `\|\|\|`; **no `Returns`** |
| Edge | once per solve / request | decode flags into `Returns.warnmany` / `Returns.fail`, then ordinary ROP upstream |
| Warm paths | per solve | `Returns` freely; prefer `warnIfLazy`, `traverseArray*`, and `dedupeWarnings`/`summariseWarnings` before reporting |

Combining flags with `|||` also removes duplicate warnings for free: 100 out-of-range nodes set the same bit once. The design note has a worked example of the kernel/edge split.

To reproduce the figures:

```bash
dotnet run -c Release --project Bench/AllocProbe --framework net8.0
dotnet run -c Release --project Bench/AllocProbe --framework net10.0
```

---

## Test organization

Tests are in `Test/Program.fs` and grouped by behavior:

- `Returns - Creation`, `Predicates`, `Conversions`, `bind`, `apply`, `map`, `&&&`, `fold`, etc.
- `ReturnsBuilder - computation expression` and `and! parallel binding`
- `Result.Extension`, `Choice.Extension`, `Option.Extension`
- `Validation - Basic Validators`, `Collection Validators`, `ValidatorBuilder`
- `Integration - End-to-end`

The test entry point composes all test lists into a single `All Tests` suite.

---

## Build and run

From the repository root:

```bash
# Build the whole solution
dotnet build ROP.sln

# Run the Expecto test suite, once per target framework
# (dotnet run needs --framework because the project multi-targets)
dotnet run --project Test/Test.fsproj --framework net8.0
dotnet run --project Test/Test.fsproj --framework net10.0

# Run only tests whose name contains a given fragment
dotnet run --project Test/Test.fsproj --framework net8.0 -- --filter "Returns - bind"
```

---

## How to publish to NuGet

### Option A — Automated, via GitHub Actions (recommended)

`.github/workflows/publish.yml` builds, tests, packs, and pushes the package whenever a tag matching `v*` or `V*` is pushed, then also creates a GitHub Release with the `.nupkg` and a zipped DLL attached.

Publishing to NuGet.org uses **[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)** (OIDC) instead of a long-lived API key: the workflow exchanges a short-lived GitHub Actions token for a ~1-hour NuGet API key at push time via the [`NuGet/login`](https://github.com/NuGet/login) action, so there's no secret API key to store or rotate for this path.

**One-time setup:**

1. Log into [nuget.org](https://www.nuget.org/) → click your username → **Trusted Publishing** → add a new policy with:
   - **Repository Owner:** `GianFossi`
   - **Repository:** `ROP`
   - **Workflow File:** `publish.yml` (just the file name, not the `.github/workflows/` path)
   - **Environment:** leave blank (this workflow doesn't use a GitHub Environment)
2. In the GitHub repository, go to **Settings → Secrets and variables → Actions → New repository secret**, name it `NUGET_USER`, and set it to your nuget.org profile/username (**not** your email address). (Requires admin/maintainer rights on the repo — see [What the PC administrator has to do](#what-the-pc-administrator-has-to-do) if you don't have them.) This isn't a secret in the sensitive sense — it's just kept out of the workflow file so it isn't hardcoded in a public file.
3. If the repository is private, a freshly created Trusted Publishing policy is only *temporarily* active for 7 days until the first successful publish confirms the repo/owner IDs — publish once within that window, or restart the 7-day window from the nuget.org UI.

**Every release:**

```bash
# Bump to whatever version you want to publish, e.g. 1.2.3
git tag v1.2.3
git push origin v1.2.3
```

Pushing the tag triggers the workflow, which:

1. Restores, builds (`Release`), and runs the full test suite — the publish is aborted if any test fails.
2. Extracts the version number from the tag (`v1.2.3` → `1.2.3`).
3. Runs `dotnet pack ROP/ROP.fsproj -p:PackageVersion=1.2.3` to produce the `.nupkg`.
4. Trades the job's GitHub OIDC token for a short-lived NuGet API key, then pushes to `https://api.nuget.org/v3/index.json` with `--skip-duplicate`.
5. Publishes a GitHub Release for the tag with the `.nupkg` and a `ROP-1.2.3-dll.zip` (containing `ROP.dll` + `ROP.xml`) attached.

### Option B — Manual publish from your own PC

Trusted Publishing currently only covers GitHub Actions — publishing from the command line still needs a classic API key from nuget.org (your account → *API Keys*).

```bash
# 1. Build & test first
dotnet build ROP.sln --configuration Release
dotnet run --project Test/Test.fsproj --configuration Release --framework net8.0
dotnet run --project Test/Test.fsproj --configuration Release --framework net10.0

# 2. Pack, specifying the version explicitly (-p:PackageVersion overrides the
#    <Version> in the .fsproj, which is only the fallback for local packs)
dotnet pack ROP/ROP.fsproj --configuration Release -p:PackageVersion=1.2.3 --output ./nupkgs

# 3. Push to NuGet.org with your API key
dotnet nuget push ./nupkgs/Ganfoss.ROP.1.2.3.nupkg `
  --api-key <YOUR_NUGET_API_KEY> `
  --source https://api.nuget.org/v3/index.json `
  --skip-duplicate
```

> On Windows PowerShell, replace the trailing `` ` `` line continuations with a single line, or use PowerShell's backtick as shown; on bash/macOS/Linux use `\` instead.

---

## Publishing / consuming locally (no NuGet.org involved)

Useful while developing the library alongside a consuming project, or on a machine without NuGet.org access.

### Option 1 — Direct project reference (fastest, for active development)

From the consuming project:

```bash
dotnet add reference ../path/to/ROP/ROP/ROP.fsproj
```

Changes to the library are picked up on the next build — no packing/publishing step at all.

### Option 2 — A local NuGet folder feed

1. Pack the library into a local folder instead of pushing anywhere:

   ```bash
   dotnet pack ROP/ROP.fsproj --configuration Release -p:PackageVersion=0.0.1-local --output C:\local-nuget-feed
   ```

2. Register that folder as a NuGet package source (once per machine, or per solution via a `NuGet.Config`):

   ```bash
   dotnet nuget add source C:\local-nuget-feed --name ROP-local
   ```

3. From the consuming project, install it like any other package:

   ```bash
   dotnet add package Ganfoss.ROP --version 0.0.1-local --source ROP-local
   ```

Re-run step 1 with a bumped version (NuGet caches by version number, so reusing the same version after a rebuild won't pick up new bits) whenever you want the consumer to see local changes.

### Option 3 — Seed your local NuGet cache directly

If you'd rather not maintain a folder feed, you can drop the packed `.nupkg` straight into your user-wide NuGet cache so it resolves like any restored package:

```bash
dotnet nuget push ./nupkgs/Ganfoss.ROP.0.0.1-local.nupkg --source %USERPROFILE%\.nuget\packages
```

(On Windows the cache is `%USERPROFILE%\.nuget\packages`; on macOS/Linux it's `~/.nuget/packages`.) This requires no admin rights — it's entirely within your user profile.

---

## Where to start reading the code

1. `ROP/Returns.fs` — core type + operators + CE builder
2. `ROP/Validation.fs` — validator DSL
3. `ROP/Examples.Returns.General.fsx` — quick mental model
4. `Test/Program.fs` — behavior coverage and edge cases
