# Ganfoss.ROP: Railway-Oriented Programming for F#, with warnings

[![NuGet](https://img.shields.io/nuget/v/Ganfoss.ROP.svg)](https://www.nuget.org/packages/Ganfoss.ROP)
[![CI](https://github.com/GianFossi/ROP/actions/workflows/ci.yml/badge.svg)](https://github.com/GianFossi/ROP/actions/workflows/ci.yml)
[![Publish NuGet](https://github.com/GianFossi/ROP/actions/workflows/publish.yml/badge.svg)](https://github.com/GianFossi/ROP/actions/workflows/publish.yml)

`Ganfoss.ROP` is an F# library for **errors as values**. Its core type, `Returns<'TSuccess,'TMessage>`, is a `Result` with one more channel: a successful value can carry **warnings**, so "it worked, but you should know that…" does not have to become a failure or a log line nobody reads. On top of it the library provides:
- sequential and parallel composition;
- a `returns { }` computation expression;
- a record-validation DSL;
- test helpers;
- stand-alone extensions for `Result`, `Choice` and `option`.

**Current version: 1.2.0** (see the [Changelog](#14-changelog)). It targets .NET 8 (LTS) and .NET 10, and is released under the PolyForm Noncommercial License 1.0.0 (see [License](#15-license)).

This README is also the **user manual**:
- **Part I** is for people using the library.
- **Part II** is for people working on it.
- **Part III** covers the roadmap, the changelog and the license.

## Contents

**Part I: Using the library**
1. [Installation](#1-installation)
2. [Quick start](#2-quick-start)
3. [Core concepts](#3-core-concepts): the type, sequential vs parallel, where warnings and errors go, design principles
4. [Pros and cons](#4-pros-and-cons)
5. [API reference](#5-api-reference)
6. [Examples](#6-examples) (12 runnable scenarios)
7. [Pitfalls](#7-pitfalls)
8. [Performance and zero-allocation paths](#8-performance-and-zero-allocation-paths)

**Part II: Developing the library**

9. [Prerequisites](#9-prerequisites)
10. [Repository structure](#10-repository-structure)
11. [Build, run and test](#11-build-run-and-test)
12. [Publishing](#12-publishing)

**Part III: Project**

13. [TODO and future developments](#13-todo-and-future-developments)
14. [Changelog](#14-changelog)
15. [License](#15-license)

---

# Part I: Using the library

## 1. Installation

```bash
dotnet add package Ganfoss.ROP
```

or, in a project file:

```xml
<PackageReference Include="Ganfoss.ROP" Version="1.2.0" />
```

Then open the namespaces you need:

| Namespace | Gives you |
| --- | --- |
| `open ROP` | the `Returns` type, the `Returns.*` functions, the `returns { }` builder, and the `Result`/`Choice`/`Option` extension modules |
| `open ROP.Returns.Operators` | the operators `>>=`, `>=>`, `<=<`, `<!>`, `<*>`, `&&&`. **Not** brought in by `open ROP`, see [Pitfalls](#7-pitfalls) |
| `open ROP.Validation` | the record-validation DSL (`createValidatorFor`, `validate`, `isGreaterThan`, ...) |
| `open ROP.Testing` | assertion helpers for unit tests (`getOrFail`, `expectFailure`, ...) |

The `Returns` module has `[<RequireQualifiedAccess>]`: always write `Returns.ok`, `Returns.bind`, and so on.

## 2. Quick start

```fsharp
open ROP
open ROP.Returns.Operators

// Each step is a "switch function": 'a -> Returns<'b, string>.
let parse (s: string) : Returns<float,string> =
    match System.Double.TryParse s with
    | true, v -> Returns.ok v
    | _       -> Returns.fail $"'{s}' is not a number"

let checkRange (v: float) =
    if v < 0.0 then Returns.fail "negative value"
    else Returns.ok v |> Returns.warnIf (fun v -> v > 100.0) "above 100: outside the calibrated range"

let pipeline s = parse s >>= checkRange >>= (fun v -> Returns.ok (sqrt v))

pipeline "25"    // Success (5.0, [])
pipeline "400"   // Success (20.0, ["above 100: outside the calibrated range"])
pipeline "-4"    // Failure ["negative value"]
pipeline "abc"   // Failure ["'abc' is not a number"]

// At the edge of the program, and only there, look inside:
match pipeline "400" with
| Returns.Pass v        -> printfn "ok: %g" v
| Returns.Warn (v, ws)  -> printfn "ok: %g, but: %s" v (String.concat "; " ws)
| Returns.Fail errs     -> printfn "failed: %s" (String.concat "; " errs)
```

## 3. Core concepts

### The `Returns` type

```fsharp
type Returns<'TSuccess, 'TMessage> =
    | Success of 'TSuccess * 'TMessage list   // a value, plus zero or more warnings
    | Failure of 'TMessage list               // one or more errors
```

Warnings and errors share one message type, `'TMessage`. Most projects use a union such as `type Msg = OutOfRange of … | NotFound of …`, which keeps messages structured until the edge, where they are rendered. `string` is fine for scripts.

### Sequential vs parallel composition

These are the two ways to combine steps, and choosing between them is the main decision when using the library:

| | Sequential | Parallel |
| --- | --- | --- |
| Use when | step 2 needs step 1's result | the checks are independent |
| On failure | stops at the first one | runs everything, reports **every** failure |
| Operators / functions | `>>=`, `>=>`, `<=<`, `bind`, `let!`, `traverseListFailFast`, `traverseArrayFailFast`, `foldSteps` | `&&&`, `<!>`/`<*>`, `map2`–`map4`, `and!`, `validateAll`, `traverseList`, `traverseArray`, `sequenceList` |
| Typical use | a calculation: parse → look up → compute | form or record validation |

### Where warnings and errors go

Each combinator follows a precise rule. Every result below was produced by running the code, with `a = Returns.warn "a" 1`, `b = Returns.warn "b" 2`, and `ea`/`eb` failing with `"ea"`/`"eb"`.

| Combinator | Result | Rule |
| --- | --- | --- |
| `a >>= (fun _ -> b)`, and `let!` | `Success (2, ["b"; "a"])` | **newest first**: each later step's warnings go in front |
| `a >>= (fun _ -> eb)` | `Failure ["eb"; "a"]` | the new errors, then the earlier warnings |
| `a \|> Returns.filter (fun _ -> false) "f"` | `Failure ["f"; "a"]` | same rule as a failing `>>=` |
| `Returns.map2 (+) a b`, `and!`, `<*>`, `&&&` | `Success (3, ["a"; "b"])` | left to right |
| `Returns.map2 (+) ea eb`, `and!` | `Failure ["ea"; "eb"]` | every error, left to right |
| `Returns.zip ea eb` | `Failure ["ea"]` | **first failure wins**: `zip` does not accumulate |
| `traverseList id [a; b]`, `validateAll`, `for` in `returns { }` | `Success ([1; 2], ["a"; "b"])` | chronological |
| `traverseListFailFast id [a; b; eb]`, `foldSteps`, `for` | `Failure ["a"; "b"; "eb"]` | chronological: earlier warnings, **then** the errors |
| `traverseList id [a; ea; b; eb]`, `fold` | `Failure ["ea"; "eb"]` | every error; warnings are dropped |
| `Returns.flatten (Returns.warn "outer" (Returns.warn "inner" 1))` | `Success (1, ["inner"; "outer"])` | like `>>=`: the inner warnings first |

When the order matters to whoever reads the report, sort or group at the edge, for example with `summariseWarnings`, rather than relying on the composition style. Unifying these rules is planned for 2.0 (see [TODO](#13-todo-and-future-developments)).

### Design principles

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

## 4. Pros and cons

**Why use it**
- **Warnings are first-class.** Engineering and data code is full of "valid, but…" outcomes, such as an extrapolated correlation, a value near a limit, or a default substituted for a missing input. `Returns` carries them next to the value, through every combinator, up to the report.
- **The choice between sequential and parallel is explicit.** `>>=` and `&&&` read differently, so accidentally stopping at the first validation error, or running dependent steps as if they were independent, shows up in the code.
- **Batteries included.** You get the computation expression with `and!` and `for`, traversals in both styles, warning aggregation, failure context, post-conditions, recovery, a validation DSL, and framework-agnostic test helpers.
- **Pure and predictable.** No hidden I/O or mutation, and side effects (`tee`, `log`) are opt-in. The behaviour is pinned by more than 380 tests, including equivalence tests against earlier implementations.
- **Measured performance.** A clean pipeline costs one 32-byte `Success` per step, and everything is stack-safe and linear up to millions of elements. Load tests with time and memory budgets guard against regressions.
- **Small and dependency-free.** Only FSharp.Core is needed.

**Why not, or when to be careful**
- **It allocates.** `Returns` is a reference type, so the code inside a hot inner loop, which must not allocate per evaluation, should not use it (see [§8](#8-performance-and-zero-allocation-paths)).
- **Warnings and errors share one type.** You cannot tell from the type alone whether a message is a warning or an error. Where it sits (`Success` or `Failure`) decides.
- **The ordering rules differ.** `>>=` puts the newest warnings first, while traversals and loops keep them chronological (see the table above).
- **Some patterns are quadratic.** Warnings are immutable lists, so re-applying `warnIf` to one accumulating value in a loop, or chaining thousands of warning `&&&`s, is quadratic. The linear alternatives are `foldSteps` and `validateAll` (see [Pitfalls](#7-pitfalls)).
- **It is F#-first.** It works from C#, but curried functions, the operators and the computation expression are awkward to use there.
- **There is no async support yet.** Nothing like `returnsTask { }` exists for `Task<Returns<…>>`; it is on the roadmap.
- **It has its own ecosystem.** It is not interchangeable with FsToolkit.ErrorHandling or FSharpPlus. Conversions to and from `Result`, `Choice` and `option` exist for the boundaries.
- **The license is noncommercial.** Commercial use is not covered by the PolyForm Noncommercial license.

## 5. API reference

Signatures use `'T` for the success type and `'M` for the message type. Every function has an XML doc comment, so IntelliSense shows the details. The notes below are what you need to pick the right function.

### 5.1 Creating values

| Function | Signature | Notes |
| --- | --- | --- |
| `Returns.ok` | `'T -> Returns<'T,'M>` | Success, no warnings |
| `Returns.warn` | `'M -> 'T -> Returns<'T,'M>` | Success with one warning |
| `Returns.warnmany` | `'M seq -> 'T -> Returns<'T,'M>` | Success with several warnings |
| `Returns.fail` | `'M -> Returns<'T,'M>` | Failure with one error |
| `Returns.failmany` | `'M seq -> Returns<'T,'M>` | Failure with several errors; pass at least one |
| `Returns.tryCatch` | `('a -> 'b) -> 'a -> Returns<'b,exn>` | runs a function and turns an exception into a Failure; `OutOfMemoryException` is not caught |

### 5.2 Inspecting and leaving the railway

| Function | Signature | Notes |
| --- | --- | --- |
| `Returns.(\|Pass\|Warn\|Fail\|)` | active pattern | `Pass v` for no warnings, `Warn (v, ws)`, `Fail errs` |
| `Returns.isSucceeded` / `isFailure` / `hasWarnings` | `Returns<'T,'M> -> bool` | |
| `Returns.defaultValue` | `'T -> Returns<'T,'M> -> 'T` | **drops warnings and errors** |
| `Returns.defaultWith` | `('M list -> 'T) -> Returns<'T,'M> -> 'T` | fallback computed from the errors; drops warnings |
| `Returns.valueOrFailwith` | `Returns<'T,'M> -> 'T` | throws with every error joined; drops warnings |
| `Returns.either` | `('T * 'M list -> 'r) -> ('M list -> 'r) -> Returns<'T,'M> -> 'r` | the general eliminator |

### 5.3 Conversions

| Function | Signature | Notes |
| --- | --- | --- |
| `Returns.toResult` / `ofResult` | `Returns<'T,'M>` ↔ `Result<'T * 'M list, 'M list>` | lossless |
| `Returns.toPlainResult` | `Returns<'T,'M> -> Result<'T,'M list>` | **drops warnings** |
| `Returns.ofPlainResult` | `Result<'T,'M> -> Returns<'T,'M>` | for a `Result<'T,'M list>`, see its XML doc |
| `Returns.toOption` | `Returns<'T,'M> -> ('T * 'M list) option` | drops errors |
| `Returns.ofOption` | `'M -> 'T option -> Returns<'T,'M>` | `None` becomes a Failure of the given message |
| `Returns.toChoice` / `ofChoice` | `Returns<'T,'M>` ↔ `Choice<'T * 'M list, 'M list>` | lossless |

### 5.4 Sequential composition (stops at the first failure)

| Function / operator | Signature | Notes |
| --- | --- | --- |
| `Returns.bind`, `>>=` | `('a -> Returns<'b,'M>) -> Returns<'a,'M> -> Returns<'b,'M>` | warnings newest first |
| `Returns.compose`, `>=>` | `('a -> Returns<'b,'M>) -> ('b -> Returns<'c,'M>) -> 'a -> Returns<'c,'M>` | Kleisli composition; `<=<` is right-to-left |
| `Returns.flatten` | `Returns<Returns<'T,'M>,'M> -> Returns<'T,'M>` | `bind id` |
| `returns { let! … }` | computation expression | `let!`, `do!`, `return`, `return!`, `if`, `match`, `for`, `while`, `try … with`, `try … finally`, `use` |

### 5.5 Parallel composition (accumulates every failure)

| Function / operator | Signature | Notes |
| --- | --- | --- |
| `Returns.map`, `<!>` | `('a -> 'b) -> Returns<'a,'M> -> Returns<'b,'M>` | |
| `Returns.apply`, `<*>` | `Returns<'a -> 'b,'M> -> Returns<'a,'M> -> Returns<'b,'M>` | `f <!> a <*> b <*> c` |
| `Returns.map2` / `map3` / `map4` | `('a -> 'b -> 'c) -> Returns<'a,'M> -> Returns<'b,'M> -> Returns<'c,'M>` | cheaper than the operator form |
| `&&&` | `('a -> Returns<'b,'M>) -> ('a -> Returns<'c,'M>) -> 'a -> Returns<'b,'M>` | runs both checks on the same input and keeps the first value |
| `Returns.plus` | `addSuccess -> addFailure -> f1 -> f2 -> 'a -> Returns<…>` | the general form of `&&&` |
| `Returns.merge` | `addSuccess -> addFailure -> Returns<'a,'M> -> Returns<'b,'M> -> Returns<'c,'M>` | like `plus`, on values |
| `Returns.validateAll` | `('T -> Returns<unit,'M>) list -> 'T -> Returns<'T,'M>` | linear; prefer it to long `&&&` chains |
| `returns { let! … and! … }` | computation expression | `and!` accumulates, like `map2` |
| `Returns.zip` | `Returns<'a,'M> -> Returns<'b,'M> -> Returns<'a * 'b,'M>` | **exception: the first failure wins, no accumulation** |

### 5.6 Collections

| Function | Signature | Failures |
| --- | --- | --- |
| `Returns.traverseList` | `('a -> Returns<'b,'M>) -> 'a list -> Returns<'b list,'M>` | accumulates |
| `Returns.traverseArray` | `('a -> Returns<'b,'M>) -> 'a[] -> Returns<'b[],'M>` | accumulates; no intermediate list |
| `Returns.sequenceList` | `Returns<'T,'M> list -> Returns<'T list,'M>` | accumulates |
| `Returns.traverseListFailFast` / `traverseArrayFailFast` | as above | stop at the first; the later items are not visited |
| `Returns.foldSteps` | `('S -> 'a -> Returns<'S,'M>) -> 'S -> 'a seq -> Returns<'S,'M>` | stops at the first; **the linear way to thread a state and collect warnings** |
| `Returns.fold` | `('S -> 'T -> 'S) -> Returns<'S,'M> -> Returns<'T,'M> seq -> Returns<'S,'M>` | folds existing `Returns` values; accumulates errors |
| `Returns.partition` | `Returns<'T,'M> list -> ('T * 'M list) list * 'M list list` | splits into successes and failures |

### 5.7 Warnings, post-conditions and recovery

| Function | Signature | Notes |
| --- | --- | --- |
| `Returns.warnIf` | `('T -> bool) -> 'M -> Returns<'T,'M> -> Returns<'T,'M>` | the message is built even when not needed; do not re-apply it in a loop |
| `Returns.warnIfLazy` | `('T -> bool) -> (unit -> 'M) -> …` | builds the message only when the predicate holds |
| `Returns.warnIfWith` | `('T -> bool) -> ('T -> 'M) -> …` | like `warnIfLazy`, with the message built from the value |
| `Returns.filter` | `('T -> bool) -> 'M -> Returns<'T,'M> -> Returns<'T,'M>` | Success → Failure when the check fails; the earlier warnings are kept |
| `Returns.filterWith` | `('T -> bool) -> ('T -> 'M) -> …` | builds the message only on failure |
| `Returns.recover` | `('M list -> Returns<'T,'M>) -> Returns<'T,'M> -> Returns<'T,'M>` | Failure → fallback; mark the fallback with a warning |
| `Returns.failOnWarnings` | `Returns<'T,'M> -> Returns<'T,'M>` | a Success with warnings becomes a Failure ("strict mode") |
| `Returns.jointMessages` / `jointMessage` | `'M list -> Returns<'T,'M> -> Returns<'T,'M>` | appends messages to whichever case the value is in |

### 5.8 Messages: transform, aggregate, add context

| Function | Signature | Notes |
| --- | --- | --- |
| `Returns.mapMessages` | `('M1 -> 'M2) -> Returns<'T,'M1> -> Returns<'T,'M2>` | changes the message type, e.g. from domain messages to report lines |
| `Returns.mapWarnings` / `mapErrors` | `('M -> 'M) -> Returns<'T,'M> -> Returns<'T,'M>` | only one channel |
| `Returns.dedupeWarnings` | `Returns<'T,'M> -> Returns<'T,'M>` | removes repeats and keeps first-occurrence order |
| `Returns.summariseWarnings` | `('M -> 'K) -> Returns<'T,'M> -> Returns<'T,'M * int>` | groups by key and counts |
| `Returns.withContextBy` | `('C -> 'M -> 'M) -> 'C -> Returns<'T,'M> -> Returns<'T,'M>` | wraps every error with a context (breadcrumbs) |

### 5.9 Side effects

| Function | Notes |
| --- | --- |
| `Returns.successTee`, `failureTee`, `eitherTee` | run an action on one or both cases and return the input unchanged |
| `Returns.log logger record message` | writes a line through `logger` when `record` is true and returns the input unchanged |
| `Returns.tee` | runs a dead-end function on a plain value and returns the value |

### 5.10 Test helpers (`ROP.Testing`)

These work with any test framework. On a mismatch they raise a plain exception whose message lists **every** message.

| Function | Returns | Fails when |
| --- | --- | --- |
| `getOrFail` | the value | the input is a Failure |
| `getWithWarnings` | value * warnings | the input is a Failure |
| `expectFailure` | the errors | the input is a Success |
| `expectFailureMatching pred` | `unit` | there is no error matching `pred` |
| `expectWarningMatching pred` | the value | there is no warning matching `pred`, or the input is a Failure |
| `expectNoWarnings` | the value | the input has warnings, or is a Failure |

### 5.11 Validation DSL (`ROP.Validation`)

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

Each validator has the shape `string -> 'Property -> ValidationState`: the DSL supplies the property name. A built validator is a plain function, `'Record -> ValidationState`, where `ValidationState` is `Ok | Errors of ValidationItem list` and each item carries `message`, `property` and `errorCode`. A worked example is in [§6, example 6](#6-recordproperty-validation-dsl-validationfs). The DSL collects every failing property. See pitfall 9 (`Ok` hides `Result.Ok`) and pitfall 12 (`null` records) in [Pitfalls](#7-pitfalls).

### 5.12 Extensions for `Result`, `Choice` and `option`

- `Result.Extension.fs`: additional helpers for `Result<'T,'E>` — `defaultValue`, `either`, `apply`, `mapError`, `bimap`, `bindError`/`catch`, `toChoice`, `zip`/`zip3`/`unzip`/`unzip3`, accumulating `apply2With`/`map2With`/`zipWith` combinators, and the `Check` module of smart-constructor-style validators.
- `Choice.Extension.fs`: helpers for `Choice<'T,'E>` — `apply`, `map2`, `map3`, `bind`, `bindChoice2Of2`, `either`, accumulating `apply2With`/`apply3With`.
- `Option.Extension.fs`: helpers for `option<'T>` — `apply`, `zip`/`zip3`, `either`, `toResultWith`, `protect`.

These modules are independent utility layers and can be used outside the `Returns` workflow.

## 6. Examples

Every snippet below has been compiled and run against the library, and the results in the comments are the ones it produced. Each example opens the namespaces it needs. Example 11 reuses `checkedDrop` from example 8.

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
open ROP.Returns.Operators

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

Returns.ok -11 >>= checkIsEven >>= checkIsNegative >>= checkIsLargerThan10
// Failure [IsNegativeValue; IsOdd]   <-- warnings collected so far are kept even on failure

Returns.ok -20 >>= checkIsEven >>= checkIsNegative >>= checkIsLargerThan10
// Failure [IsEven]                   <-- -20 is even: the first check fails and the rest never runs

// Or compose the switch functions themselves (point-free, "Kleisli" composition):
let pipeline = checkIsEven >=> checkIsNegative >=> checkIsLargerThan10
-11 |> pipeline    // Failure [IsNegativeValue; IsOdd]
```

### 3. Parallel composition (`&&&`) — accumulate every failure

```fsharp
open ROP
open ROP.Returns.Operators

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
open ROP.Returns.Operators

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
let withFallback : Result<int,string> = Result.bindError (fun _ -> Ok 0) (Error "not found")
// Ok 0

// Accumulate errors on a plain Result<'T, string list> without switching to Returns:
let combined =
    Result.apply2With (@) (fun a b -> a + b) (Error ["e1"]) (Error ["e2"])
// Error ["e1"; "e2"]

// Option parity helper:
Option.either (fun x -> string x) (fun () -> "none") (Some 42)   // "42"
```

### 8. Post-conditions and recovery (`filter`, `filterWith`, `recover`)

`filter` turns a Success into a Failure when a check on its value fails. As with a failing `>>=` step, the warnings raised so far are kept, after the error. `filterWith` builds the error message from the value, and only when the check fails. `recover` goes the other way: it replaces a Failure with a fallback, and the fallback can say that it is one.

```fsharp
open ROP

let pressureDrop (flow: float) : Returns<float,string> =
    if flow <= 0.0 then Returns.fail "flow must be positive"
    else
        Returns.ok (0.8 * flow * flow)
        |> Returns.warnIfLazy (fun dp -> dp > 50.0) (fun () -> "high pressure drop")

let checkedDrop flow =
    pressureDrop flow
    |> Returns.filterWith (fun dp -> dp <= 100.0) (fun dp -> sprintf "pressure drop %.1f bar exceeds 100 bar" dp)

checkedDrop 5.0    // Success (20.0, [])
checkedDrop 9.0    // Success (64.8, ["high pressure drop"])
checkedDrop 12.0   // Failure ["pressure drop 115.2 bar exceeds 100 bar"; "high pressure drop"]

let readSensor (id: string) : Returns<float,string> =
    if id = "T2" then Returns.fail "sensor T2 offline" else Returns.ok 81.5

let temperature id =
    readSensor id
    |> Returns.recover (fun errs -> Returns.warn ("using design value 80.0: " + String.concat "; " errs) 80.0)

temperature "T1"   // Success (81.5, [])
temperature "T2"   // Success (80.0, ["using design value 80.0: sensor T2 offline"])
```

Unlike `defaultValue`, which leaves the railway with a bare value, `recover` keeps the substitution visible to everything downstream.

### 9. Where did it fail? Breadcrumbs with `withContextBy`

`'TMessage` is generic, so the library can't prefix a label to it by itself. You say once how a context wraps a message, and every failure that passes through a labelled step gets wrapped:

```fsharp
open ROP

type Msg =
    | OutOfRange of quantity: string * value: float
    | InContext of context: string * inner: Msg

// Defined once per project.
let withContext context r = Returns.withContextBy (fun ctx m -> InContext (ctx, m)) context r

let nusselt (node: int) (re: float) : Returns<float,Msg> =
    (if re < 2300.0 then Returns.fail (OutOfRange ("Re", re)) else Returns.ok (0.023 * re ** 0.8))
    |> withContext $"node {node}"

let march (reynolds: float list) =
    reynolds
    |> List.indexed
    |> Returns.traverseListFailFast (fun (i, re) -> nusselt i re)
    |> withContext "tube-side march"

march [ 12000.0; 8000.0; 1500.0; 900.0 ]
// Failure [InContext ("tube-side march", InContext ("node 2", OutOfRange ("Re", 1500.0)))]
```

The failure says what went wrong (`Re = 1500`) and where: node 2 of the tube-side march. Node 3 is never evaluated, because the march is fail-fast.

### 10. The same warning 100 times: `dedupeWarnings` / `summariseWarnings`

```fsharp
open ROP

let reynoldsAlongTube = [ for i in 0 .. 99 -> 1500.0 + 40.0 * float i ]

let nodeHeatTransfer (re: float) : Returns<float,string> =
    Returns.ok (0.023 * re ** 0.8)
    |> Returns.warnIfLazy (fun _ -> re < 3000.0) (fun () -> "Gnielinski: Re below 3000, extrapolated")

let profile = Returns.traverseList nodeHeatTransfer reynoldsAlongTube
// Success ([...100 values...], [38 identical warnings])

profile |> Returns.dedupeWarnings
// Success ([...], ["Gnielinski: Re below 3000, extrapolated"])

profile |> Returns.summariseWarnings id
// Success ([...], [("Gnielinski: Re below 3000, extrapolated", 38)])
```

`summariseWarnings` takes a key function. With a union message such as `Extrapolated of correlation * node`, grouping by the correlation alone collapses warnings that differ only in the node index.

### 11. Testing with `ROP.Testing`

These helpers work with any test framework: a mismatch raises a plain exception, and xUnit, NUnit and Expecto all report it as a failure. Below, Expecto:

```fsharp
open Expecto
open ROP.Testing

let pressureDropTests =
    testList "pressure drop" [
        test "nominal flow passes, flagged as high" {
            let dp = checkedDrop 9.0 |> expectWarningMatching ((=) "high pressure drop")
            Expect.floatClose Accuracy.medium dp 64.8 "dp"
        }
        test "low flow is clean" {
            checkedDrop 2.0 |> expectNoWarnings |> ignore
        }
        test "excessive flow is rejected, keeping the earlier warning" {
            let errors = checkedDrop 12.0 |> expectFailure
            Expect.equal errors.Length 2 "the filter error plus the earlier warning"
        }
        test "zero flow is rejected" {
            checkedDrop 0.0 |> expectFailureMatching (fun m -> m.Contains "positive")
        }
    ]
```

On a mismatch, the exception message lists every message, not just the first:

```text
Expected Success, but got Failure with 2 error(s):
  [1] flow must be positive
  [2] U must be positive
```

### 12. End to end: sizing a heat exchanger

This example uses every part of the library at once:
- the inputs are validated **in parallel**, so every problem is reported together;
- the calculation runs **sequentially**, raising warnings without stopping;
- the result gets a **post-condition**;
- the **edge** of the program turns everything into a report with `Pass | Warn | Fail`.

```fsharp
open ROP

type Stream = { Name: string; MassFlow: float; Cp: float; TIn: float; TOut: float }

type HxMsg =
    | NonPositive of field: string
    | TemperatureCross of deltaT: float
    | EnergyImbalance of percent: float
    | SmallApproach of deltaT: float
    | AreaTooLarge of area: float

let positive field (v: float) : Returns<float,HxMsg> =
    if v > 0.0 then Returns.ok v else Returns.fail (NonPositive field)

// Independent checks on one stream: all run, every failure reported.
let validateStream (s: Stream) : Returns<Stream,HxMsg> =
    s |> Returns.validateAll [
        fun s -> positive $"{s.Name}.MassFlow" s.MassFlow |> Returns.map ignore
        fun s -> positive $"{s.Name}.Cp" s.Cp |> Returns.map ignore ]

// Counter-current log-mean temperature difference.
let logMeanDeltaT (hot: Stream) (cold: Stream) : Returns<float,HxMsg> =
    let dt1 = hot.TIn - cold.TOut
    let dt2 = hot.TOut - cold.TIn
    if dt1 <= 0.0 || dt2 <= 0.0 then Returns.fail (TemperatureCross (min dt1 dt2))
    else
        Returns.ok (if abs (dt1 - dt2) < 1e-9 then dt1 else (dt1 - dt2) / log (dt1 / dt2))
        |> Returns.warnIfLazy (fun _ -> min dt1 dt2 < 5.0) (fun () -> SmallApproach (min dt1 dt2))

let design (hot: Stream) (cold: Stream) (u: float) : Returns<float,HxMsg> =
    returns {
        // Parallel: all three inputs are validated, every problem is reported at once.
        let! hot = validateStream hot
        and! cold = validateStream cold
        and! u = positive "U" u
        // Sequential: each step needs the previous one.
        let qHot = hot.MassFlow * hot.Cp * (hot.TIn - hot.TOut)
        let qCold = cold.MassFlow * cold.Cp * (cold.TOut - cold.TIn)
        let imbalance = 100.0 * abs (qHot - qCold) / qHot
        let! lmtd = logMeanDeltaT hot cold
        return! Returns.ok (qHot / (u * lmtd))
                |> Returns.warnIfLazy (fun _ -> imbalance > 2.0) (fun () -> EnergyImbalance (round imbalance))
    }
    // Post-condition on the result: the warnings raised so far are kept if it fails.
    |> Returns.filterWith (fun area -> area <= 500.0) (round >> AreaTooLarge)

// The edge of the program: the only place that pattern-matches.
let report r =
    match r with
    | Returns.Pass area       -> printfn "Area %.2f m2" area
    | Returns.Warn (area, ws) -> printfn "Area %.2f m2, with warnings: %A" area ws
    | Returns.Fail errs       -> printfn "Design rejected: %A" errs

let hot  = { Name = "hot";  MassFlow = 2.0; Cp = 4.18; TIn = 90.0; TOut = 60.0 }
let cold = { Name = "cold"; MassFlow = 3.0; Cp = 4.18; TIn = 20.0; TOut = 40.0 }

report (design hot cold 0.5)
// Area 11.19 m2

report (design hot { cold with TOut = 87.0 } 0.5)
// Area 35.12 m2, with warnings: [EnergyImbalance 235.0; SmallApproach 3.0]

report (design { hot with MassFlow = 0.0 } { cold with Cp = -1.0 } -0.5)
// Design rejected: [NonPositive "hot.MassFlow"; NonPositive "cold.Cp"; NonPositive "U"]

report (design hot { cold with TOut = 95.0 } 0.5)
// Design rejected: [TemperatureCross -5.0]

report (design hot cold 0.01)
// Design rejected: [AreaTooLarge 560.0]
```

Things to notice:
- **Invalid inputs:** the third case reports all three bad inputs at once, even though they come from two different streams and a scalar.
- **Warning order:** in the second case the warnings come out newest first, because `let!`/`>>=` put each later step's warnings in front.
- **Calculation errors:** a temperature cross is a failure of the calculation itself, found only after the inputs have been accepted, so it stops the sequential part.

A longer version, with layered and remapped error messages, is in [`ROP/Examples.Returns.HeatExchanger.fsx`](https://github.com/GianFossi/ROP/blob/master/ROP/Examples.Returns.HeatExchanger.fsx).

## 7. Pitfalls

Each pitfall below was found in real use or while testing this library.

| # | Pitfall | What happens | Do this instead |
| --- | --- | --- | --- |
| 1 | `open ROP` only | `>>=`, `&&&`, `<!>` and `<*>` are not in scope, and the compiler says "this function takes too many arguments" | also `open ROP.Returns.Operators` |
| 2 | `r <- r \|> Returns.warnIf …` in a loop | **quadratic**: each call copies every warning collected so far. 20,000 steps allocate 1.6 GB | `Returns.foldSteps` with `warnIf`/`warnIfLazy` **inside** the step: 4 MB |
| 3 | Thousands of `&&&` validators that all warn | quadratic in the number of warnings (4,000 validators allocate 257 MB) | `Returns.validateAll` (0.8 MB) |
| 4 | `warnIf p $"Re = {re}"` / `filter p $"…"` | the string is formatted on **every** call, even when `p` is false (the common case) | `warnIfLazy`, `warnIfWith` or `filterWith` |
| 5 | `traverseList` where fail-fast was meant, or the reverse | `traverseList` **runs every element** and accumulates; the `FailFast` variants stop | pick the variant deliberately (see §5.6) |
| 6 | Expecting one warning order everywhere | `>>=`/`let!` put the newest first; traversals, loops and `foldSteps` are chronological | see [Where warnings and errors go](#where-warnings-and-errors-go); sort at the edge |
| 7 | Expecting `zip` to accumulate | `zip ea eb` returns only `ea` | `map2 (fun a b -> a, b)` or `and!` |
| 8 | `toPlainResult`, `toOption`, `defaultValue`, `valueOrFailwith` | the warnings are silently lost | `toResult` keeps them, or deal with them first (`failOnWarnings`, `successTee`) |
| 9 | `open ROP.Validation` in a file that uses `Result` | `ValidationState.Ok` hides `Result.Ok`, which gives type errors such as "expected Result… but was ValidationState" | open `ROP.Validation` last and write `Result.Ok`/`Result.Error`, or keep validation in its own module |
| 10 | `open ROP.Returns.Operators` in numeric code | `&&&` hides the bitwise AND on integers | use `%` or `Microsoft.FSharp.Core.Operators.(&&&)`, or open the operators only where needed |
| 11 | Interpolated numbers in messages: `$"{x}"` | follows the current culture, so an Italian machine prints `115,2`, and floats print every digit (`115,20000000000002`) | `sprintf "%.1f" x`, which is invariant and formatted |
| 12 | Validating a `null` record with the DSL | `NullReferenceException` | check for `null` before calling the validator |
| 13 | Constructing `Failure []` by hand | a "failure without errors" is reported as `Fail []` | always fail with at least one message (`Returns.fail`/`failmany`) |
| 14 | `Returns` in a per-node / per-iteration hot kernel | 32 B or more per step | struct state + flags enum in the kernel, `Returns` at the edge (§8) |
| 15 | `for` loops in `returns { }` before 1.2.0 | did not compile | upgrade to 1.2.0 or later |

## 8. Performance and zero-allocation paths

### Zero-allocation paths

The full design note is [docs/ZERO-ALLOC.md](https://github.com/GianFossi/ROP/blob/master/docs/ZERO-ALLOC.md). In short:

**Decision:** there is no `[<Struct>]` variant of `Returns`. Code that must not allocate per evaluation (per node, per iteration, per integration point) does not use ROP. It uses plain struct values and a `[<Flags>]` diagnostics enum, and converts to `Returns` **once**, at the edge (per solve or request).

**Why not a struct variant.** A struct prototype allocates nothing on the clean path, but:

- every warning still allocates a list cell and usually the message too;
- making warnings allocation-free means a flags enum, which is what the kernels already have;
- the whole API (operators, CE, traversals, `Validation`) would have to be duplicated.

**What `Returns` costs**, in bytes allocated per call, measured with `Bench/AllocProbe`:

| Scenario | v1.0.2 net8.0 | v1.0.2 net10.0 | v1.1.0+ net8.0 | v1.1.0+ net10.0 |
| --- | ---: | ---: | ---: | ---: |
| `Returns.ok x` | 32 B | 32 B | 32 B | 32 B |
| `ok x >>= s >>= s >>= s`, no warnings | 560 B | 408 B | **128 B** | **128 B** |
| `returns { let! … ×3 }`, no warnings | 448 B | 349 B | **128 B** | **128 B** |
| `returns { let! … and! … and! … }` | 288 B | 288 B | **128 B** | **128 B** |
| `validateAll` ×5 | 440 B | 440 B | **192 B** | **192 B** |
| `fold`, 100 elements | 5,760 B | 5,760 B | **104 B** | **104 B** |
| `traverseList`, 100 elements | 12,912 B | 12,912 B | **7,280 B** | **7,280 B** |
| `Validation` DSL, valid record (7 checks) | 1,096 B | 1,096 B | **56 B** | **56 B** |
| `warnIf` (false) vs `warnIfLazy` (false), interpolated message | 336 B | 336 B | 336 B → **32 B** | 336 B → **32 B** |

Times improve by similar factors: for example, `fold` over 100 elements drops from 2.0 µs to 0.7 µs, and a `Validation` record from 671 ns to 112 ns. The design note has the full memory and time tables, and the list of what changed; every rewrite is checked against its previous implementation by the test suite.

Since v1.1.0, a clean pipeline costs one `Success` (32 B) per step on both runtimes. That is the minimum for a reference-type union.

**The boundary:**

| Layer | Runs | Uses |
| --- | --- | --- |
| Kernel | per node / iteration | struct state + flags enum, combined with `\|\|\|`; **no `Returns`** |
| Edge | once per solve / request | decode flags into `Returns.warnmany` / `Returns.fail`, then ordinary ROP upstream |
| Warm paths | per solve | `Returns` freely; prefer `warnIfLazy`, `traverseArray*`, and `dedupeWarnings`/`summariseWarnings` before reporting |

In ROP code that loops, do not re-apply `warnIf` to one accumulating value: each call copies every warning collected so far, which is quadratic. Thread the state with `Returns.foldSteps` instead (linear, chronological warnings); [docs/ZERO-ALLOC.md](https://github.com/GianFossi/ROP/blob/master/docs/ZERO-ALLOC.md) has the measurements.

Combining flags with `|||` also removes duplicate warnings for free: 100 out-of-range nodes set the same bit once. The design note has a worked example of the kernel/edge split.

To reproduce the figures:

```bash
dotnet run -c Release --project Bench/AllocProbe --framework net8.0
dotnet run -c Release --project Bench/AllocProbe --framework net10.0
```

### Load tests

`Test/Performance.fs` measures realistic workloads, from 50,000 to 1,000,000 elements, against time and memory budgets. Each run prints its figures, so the test suite doubles as a benchmark. See [§11](#load-tests-time-and-memory-budgets).

---

# Part II: Developing the library

## 9. Prerequisites

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

## 10. Repository structure

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
│   ├── Performance.fs         # load tests with time and memory budgets
│   └── Program.fs             # Expecto test suite (387 tests)
└── Setup/
    └── Setup.vdproj           # legacy Visual Studio Installer project (not part of the build)
```

### Technology stack

- **Language**: F#
- **Runtime**: .NET 8 LTS and .NET 10 (`TargetFrameworks: net8.0;net10.0`; `LangVersion` pinned per framework)
- **Library project**: `ROP/ROP.fsproj` (NuGet package id `Ganfoss.ROP`)
- **Tests**: Expecto (`Test/Test.fsproj`)
- **Solution**: `ROP.sln`

### Where to start reading the code

1. `ROP/Returns.fs` — core type + operators + CE builder
2. `ROP/Validation.fs` — validator DSL
3. `ROP/Examples.Returns.General.fsx` — quick mental model
4. `Test/Program.fs` — behavior coverage and edge cases

## 11. Build, run and test

### Commands

From the repository root. Build and tests need no network access after the first restore.

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

### Test organization

Tests are in `Test/Program.fs` and grouped by behavior:

- **`Returns - …`**: one list per combinator group (creation, conversions, `bind`, `apply`, `map`, `&&&`, `fold`, `foldSteps`, `filter`, `recover`, `warnIf*`, traversals, aggregation, context, and more).
- **`ReturnsBuilder - …`**: `let!`, `and!`, and `for` loops in the computation expression.
- **`Result.Extension`, `Choice.Extension`, `Option.Extension`**: the stand-alone extensions.
- **`Validation - …`**: the validators and the DSL.
- **`Testing - assertion helpers`**: `ROP.Testing`.
- **`Performance rewrites - equivalence with previous implementations`**: every optimized combinator is compared with a verbatim copy of its previous code, over every combination of inputs.
- **`Integration - …`**: end-to-end scenarios.
- **`Performance - intensive usage (time and memory budgets)`**: the load tests, in `Test/Performance.fs`.

The test entry point composes all test lists into a single `All Tests` suite.

### Load tests (time and memory budgets)

`Test/Performance.fs` runs intensive, realistic workloads and checks both the result and the cost:

| Scenario | Items | What it exercises |
| --- | ---: | --- |
| Marching solver, clean / with 50 defective nodes | 50,000 | `&&&` input checks, a `>>=` chain, `warnIfLazy`, `filterWith`, `withContextBy`, `traverseArray`, `summariseWarnings` |
| Validation DSL, 10% invalid records | 20,000 | `createValidatorFor`, 9 checks per record |
| CE pipeline | 100,000 | `let!` / `and!` / `and!` / `let!` / `filter` |
| CE `for` loop collecting warnings | 200,000 | `returns { for … do do! … }` |
| `traverseList`, `>>=` chain, `fold` | 1,000,000 | stack safety at scale |
| Linearity guards | n and 2n | bytes per item must not grow with size, which catches accidental O(n²) |

Each scenario has a **`PerfBudget`** with two limits:
- `MaxMilliseconds`: the wall-clock time of the measured run;
- `MaxBytesPerItem`: the bytes allocated per processed item, with separate values for Debug and Release builds.

A warm-up run comes first, and every run prints a line like:

```text
[perf] marching solver: 50k nodes, clean     50000 items     14.7 ms     294.1 ns/item     323.2 B/item  gen0=1
```

There is also a 1M-node `foldSteps` march. Two environment variables control the load tests:

| Variable | Effect |
| --- | --- |
| `ROP_PERF_TIME_SCALE` | Multiplies every time budget (default `1`). CI sets it to `3` because shared runners are slower. |
| `ROP_PERF_SKIP=1` | Skips the load tests. |

Memory budgets are never scaled. Allocations are deterministic, and identical on net8.0 and net10.0, so exceeding one means the code changed, not the machine.

## 12. Publishing

### Publishing to NuGet.org

#### Option A — Automated, via GitHub Actions (recommended)

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

#### Option B — Manual publish from your own PC

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

### Publishing / consuming locally (no NuGet.org involved)

Useful while developing the library alongside a consuming project, or on a machine without NuGet.org access.

#### Option 1 — Direct project reference (fastest, for active development)

From the consuming project:

```bash
dotnet add reference ../path/to/ROP/ROP/ROP.fsproj
```

Changes to the library are picked up on the next build — no packing/publishing step at all.

#### Option 2 — A local NuGet folder feed

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

#### Option 3 — Seed your local NuGet cache directly

If you'd rather not maintain a folder feed, you can drop the packed `.nupkg` straight into your user-wide NuGet cache so it resolves like any restored package:

```bash
dotnet nuget push ./nupkgs/Ganfoss.ROP.0.0.1-local.nupkg --source %USERPROFILE%\.nuget\packages
```

(On Windows the cache is `%USERPROFILE%\.nuget\packages`; on macOS/Linux it's `~/.nuget/packages`.) This requires no admin rights — it's entirely within your user profile.

### The README on NuGet

This file is packed into the NuGet package (`PackageReadmeFile` in `ROP/ROP.fsproj`), so nuget.org shows it on the package page. Keep it self-contained:
- **Links:** link to repository files with absolute `https://github.com/GianFossi/ROP/blob/master/...` URLs, because relative paths do not resolve on nuget.org.
- **Markup:** use no raw HTML.
- **Images:** only images from hosts nuget.org trusts, such as `img.shields.io` and `github.com` badges.

---

# Part III: Project

## 13. TODO and future developments

### Next minor releases (1.x, additive only)

- [ ] **Async support:** a `returnsTask { }` builder for `Task<Returns<…>>` (with `let!` over `Task<Returns>`, `Task` and `Returns`), plus `Returns.bindTask`/`mapTask`. Nothing exists today.
- [ ] **Bridge between the validation DSL and `Returns`:** `Validation.toReturns : ValidationState -> Returns<unit, ValidationItem>`, so DSL results compose with the rest of the library.
- [ ] **Null guard in the validation DSL:** report a `null` record as an error instead of throwing ([pitfall 12](#7-pitfalls)).
- [ ] **API documentation site:** generated from the XML docs with `fsdocs`, published on GitHub Pages.
- [ ] **SourceLink and a symbols package (`.snupkg`):** step into the library while debugging.
- [ ] **Property-based tests (FsCheck):** check the monad and applicative laws, and the ordering rules in [§3](#where-warnings-and-errors-go).
- [ ] **BenchmarkDotNet suite in CI:** keep a history of the timing and allocation figures that `Bench/AllocProbe` measures today.
- [ ] **Repository clean-up:** remove or move the obsolete files (`Validate (OBSOLETE).fs`, `Script5–7.fsx`, `Setup/Setup.vdproj`) and refresh the `.fsx` examples.

### Version 2.0 (breaking changes, planned together)

- [ ] **Replace `'TMessage list` with a dedicated message collection.** Today's lists force every append to the end (`warnIf`, `&&&`, `apply`) to copy the whole list, which causes the quadratic pitfalls, and they make traversals accumulate in reverse and reverse again. A dedicated `Messages<'M>` type would fix this. Two candidates:
  - a small **rope or chunked tree**, with O(1) append and concat, materialized in order only when read;
  - a **catenable deque**.

  Either would make every combinator linear by construction. It would also make a `[<Struct>] Returns` realistic: an empty message collection could be a null or a default value, so a clean success would allocate nothing (see [§8](#8-performance-and-zero-allocation-paths)).
- [ ] **One ordering rule.** All combinators would keep warnings in chronological order, including `>>=` and `let!`. Today these put the newest first, while traversals and loops are chronological ([§3](#where-warnings-and-errors-go)).
- [ ] **Failures that cannot be empty.** `Failure` would hold a non-empty collection, so `Failure []` could not be constructed.
- [ ] **Optional severity or separate message types.** For example, `Returns<'T,'W,'E>` with distinct warning and error types, or a severity carried by the message.
- [ ] **Naming clean-up:**
  - `isSucceeded` → `isSuccess`;
  - `warnmany`/`failmany` → `warnMany`/`failMany`;
  - `jointMessages` → `appendMessages`;
  - typos in parameter names (`failureMesssage`, `'SuccesState`).
- [ ] **Validation DSL returns `Returns`,** and its `Ok`/`Errors` cases are renamed so they no longer hide `Result.Ok`.
- [ ] **Review the `Choice` extensions**, which are rarely used, for removal or a separate package.

Contributions and discussion: <https://github.com/GianFossi/ROP/issues>.

## 14. Changelog

### 1.2.0

This release only adds; code written against 1.1.x compiles unchanged.

- `Returns.filter` and `Returns.filterWith` add post-condition checks, turning a Success into a Failure when a predicate fails. The warnings collected so far are kept after the error, as with a failing `>>=` step. `filterWith` builds the message from the value, and only when the check fails.
- `Returns.foldSteps` threads a state through a sequence one step at a time (a monadic fold). It stops at the first failure and collects warnings in chronological order. This is the **linear** way to write a marching or iterative calculation: re-applying `warnIf` to one accumulating value in a loop is quadratic (1.6 GB for 20,000 steps, against 4 MB with `foldSteps` + `warnIfLazy`).
- `Returns.recover` provides error recovery: the errors go to a compensation function whose result replaces the Failure. That result can be a fallback Success, ideally with a warning saying so, or a different Failure.
- `for` loops inside `returns { }` now compile. They never did before: the builder's `Source` member only accepted `Returns` values, and the compiler applies `Source` to the sequence of a `for` loop too.
- `Returns.ToString()` no longer throws `NullReferenceException` when a message is `null`.
- Load tests with time and memory budgets are new; see [§11](#load-tests-time-and-memory-budgets).
- There are 98 more tests (387 in total). They include coverage for the `Result` extensions (`map2`–`map4`, `mapError`, `flatten`, `merge`, `zip`, `partition`, `fold`, `foldList`, the tee functions, `compose`, `protect`), `Returns.log`, and complex-object integration scenarios.

### 1.1.0

This release only adds; code written against 1.0.x compiles unchanged.

- `warnIfLazy` builds the warning message only when the predicate holds.
- `ROP.Testing` provides test assertions that work with any test framework.
- `dedupeWarnings` and `summariseWarnings` collapse repeated warnings.
- `traverseListFailFast`, `traverseArray` and `traverseArrayFailFast` complement `traverseList`, which accumulates every failure.
- `withContextBy` builds breadcrumb trails on failures.
- `ofPlainResult` and `toPlainResult` convert to and from plain `Result`.
- Performance, with semantics unchanged:
  - `bind`, `>>=` and `returns { let! … }` allocate one `Success` per step, 3–4× less than before.
  - `fold`, `traverseList` and `sequenceList` allocate 2–55× less and run 2–3× faster.
  - Validating a valid record with the `Validation` DSL allocates about 20× less and runs about 6× faster.
  - See [§8](#8-performance-and-zero-allocation-paths).

### 1.0.2 and earlier

The first public releases: the `Returns` type and its combinators, the `returns { }` builder with `and!`, the validation DSL, and the `Result`/`Choice`/`Option` extensions. Multi-targeting of `net8.0` and `net10.0` arrived in 1.0.2. The full history is in the [GitHub releases](https://github.com/GianFossi/ROP/releases).

## 15. License

Ganfoss.ROP is released under the **[PolyForm Noncommercial License 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0)** (see [LICENSE](https://github.com/GianFossi/ROP/blob/master/LICENSE)):
- **Allowed:** use, modification and redistribution for any noncommercial purpose. That includes personal use, research, education, and use by noncommercial organizations such as charities, educational and public research institutions, and government bodies.
- **Not covered:** commercial use.

The same license file ships inside the NuGet package.
