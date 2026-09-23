# Zero-allocation paths and `Returns`

**Decision (v1.1.0):** there is no `[<Struct>]` variant of `Returns`. Code that must not allocate per evaluation does not use ROP. It uses plain struct values and a bit-flag diagnostics set, and converts to `Returns` **once**, at a documented boundary. This note explains why, and where that boundary sits.

## What `Returns` actually costs

Measured with [`Bench/AllocProbe`](../Bench/AllocProbe/Program.fs): bytes allocated per call and time per call, averaged over 5M calls (100k for the 100-element collections) after warm-up. To reproduce, run `dotnet run -c Release --project Bench/AllocProbe --framework net8.0` (or `net10.0`). Pass a scenario-name fragment as an argument to run a single scenario.

### Memory, bytes per call

| Scenario | v1.0.2 net8.0 | v1.0.2 net10.0 | v1.1.0 net8.0 | v1.1.0 net10.0 |
| --- | ---: | ---: | ---: | ---: |
| Plain `float` arithmetic (baseline) | 0 | 0 | 0 | 0 |
| `Returns.ok x` | 32 | 32 | 32 | 32 |
| `ok x >>= s >>= s >>= s`, no warnings | 560 | 408 | **128** | **128** |
| `returns { let! … ×3 }`, no warnings | 448 | 349 | **128** | **128** |
| `returns { let! … and! … and! … }`, no warnings | 288 | 288 | **128** | **128** |
| 3 steps, each adding one warning | 816 | 662 | **448** | **448** |
| `Returns.map` | 96 | 96 | **64** | **64** |
| `Returns.map2` | 184 | 184 | **96** | **96** |
| `f <!> a <*> b <*> c` | 280 | 280 | **248** | **248** |
| `&&&` ×5, no warnings | 288 | 288 | **160** | **160** |
| `validateAll` ×5, no warnings | 440 | 440 | **192** | **192** |
| `warnIf` (false) with an interpolated message | 336 | 336 | 336 | 336 |
| `warnIfLazy` (false), same message | n/a | n/a | **32** | **32** |
| `mapWarnings` on a Success with no warnings | 64 | 64 | **32** | **32** |
| `traverseList`, 100 elements | 12,912 | 12,912 | **7,280** | **7,280** |
| `sequenceList`, 100 elements | 9,688 | 9,688 | **4,056** | **4,056** |
| `traverseArray`, 100 elements | n/a | n/a | 4,080 | 4,080 |
| `fold`, 100 elements | 5,760 | 5,760 | **104** | **104** |
| `Validation` DSL, 4 properties / 7 checks, valid record | 1,096 | 1,096 | **56** | **56** |
| *Prototype* struct variant, 3 binds, no warnings | 0 | 0 | 0 | 0 |
| *Prototype* struct variant, one warning | 64 | 64 | 64 | 64 |

### Time, ns per call (net8.0; net10.0 is within noise of these)

| Scenario | Before the rewrite | v1.1.0 |
| --- | ---: | ---: |
| `returns { let! … ×3 }` | 64 | **15** |
| `returns { let! … and! … and! … }` | 79 | **26** |
| `Returns.map` / `map2` | 14 / 32 | **10 / 16** |
| `&&&` ×5 | 90 | **78** |
| `validateAll` ×5 | 217 | **81** |
| `warnIfLazy` (false) | 14 | **7** |
| `mapWarnings` / `failOnWarnings`, clean Success | 28 / 18 | **12 / 10** |
| `traverseList`, 100 elements | 3,751 | **1,908** |
| `sequenceList`, 100 elements | 2,797 | **1,043** |
| `fold`, 100 elements | 2,044 | **692** |
| `Validation` DSL, valid record | 671 | **112** |

### What changed in v1.1.0

Every rewrite below keeps the semantics exactly: same values, same warnings and errors, in the same order, including edge cases such as `Failure []`. The test suite checks this by comparing each rewritten combinator against a verbatim copy of its previous implementation, on every combination of clean Success, Success with warnings, Failure, and `Failure []`.

- **`bind` / `>>=` / `jointMessages`**: no longer allocate closures (via `either`) on every call, and no longer re-wrap a result when there are no warnings to merge. A clean pipeline costs exactly one `Success` (32 B) per step, the minimum for a reference-type union, on both runtimes. On net10.0, the fractional v1.0.2 figures came from the JIT removing some of those closures, but only sometimes.
- **`returns { }`**: the hot builder members are `inline` with `[<InlineIfLambda>]`, so the continuation of each `let!` is not allocated as a closure. `Bind2Return` and `Bind3Return` handle `let! … and! … return` directly, without the nested tuples of `MergeSources`.
- **`map`, `map2`, `map3`, `map4`**: matched directly instead of lifting the function with `ok` and chaining `apply`.
- **`&&&`**: returns the first branch's result as-is when it would otherwise be re-wrapped unchanged.
- **`validateAll`, `traverseList`, `sequenceList`, `traverseListFailFast`, `fold`**: a single pass over mutable locals. There is no accumulator object per element, and values are consed into the result list exactly once.
- **`warnIfLazy`, `warnIfWith`**: `inline`, so call-site lambdas allocate nothing.
- **`failOnWarnings`**: `List.isEmpty` instead of generic structural equality against `[]`. `mapWarnings`, `dedupeWarnings` and `summariseWarnings` return early when there is nothing to do.
- **`Validation`**:
  - A record is validated in one pass with no intermediate lists, and is boxed once.
  - `isEqualTo`, `isNotEqualTo`, `isGreaterThan(OrEqualTo)` and `isLessThan(OrEqualTo)` are `inline`, so comparisons are specialized to the property type instead of boxing both operands.
  - `hasLengthOf`, `hasMinLengthOf`, `hasMaxLengthOf`, `isNotEmpty` and `isEmpty` read `string.Length` instead of enumerating the string character by character.

What remains is inherent. Each step's own `Success` costs 32 B. The partial-application closures of `f <!> a <*> b <*> c` are built into curried applicative style; use `map3` or `and!` when that matters. Warnings are lists. None of this can be removed without a different type, and the next section explains why that isn't worth it.

## Is a struct variant viable?

Technically yes. A struct union such as

```fsharp
[<Struct>]
type ValueReturns<'T,'M> =
    | VSuccess of value: 'T * warnings: 'M list
    | VFailure of errors: 'M list
```

allocates nothing on the clean path, because `[]` is a shared singleton (see the prototype in the probe). It still doesn't pay for itself:

1. **The warning channel still allocates.** Every warning costs a list cell, and usually the message object as well (64 B in the probe). "Zero allocation" survives only while nothing goes wrong. Removing that cost requires a different warning representation, and each option has a problem:
   - *No warnings*: this is just `ValueResult`/`Result`, so there is nothing ROP-specific left to add.
   - *A single warning*: the second warning is silently lost, the kind of trap this library exists to prevent.
   - *A bit-flag set* (`'M : enum`): this works, but it is exactly the struct-state + flags design the consuming kernels already have. A library type would only give it a new name.
2. **The whole API would be duplicated.** `bind`, `map`, `apply`, `and!`, the CE builder, the traversals, `Validation`, and the operators would all need a second copy. The operators are let-bound functions in `Returns.Operators`, so they can't serve both types without SRTP dispatch, and that would hurt type inference for every existing caller.
3. **Struct unions store every case's fields side by side.** Each value carries the tag, `'T`, and two list references. For a large `'T`, the copies made at each step can cost more than the allocation they avoid.
4. **You still need a boundary.** A consumer of the struct form still converts to `Returns` somewhere to report to the outside world. So the boundary below is needed either way, and the struct form only moves it.

## The boundary

| Layer | Examples | Uses |
| --- | --- | --- |
| **Kernel**: runs per node, per iteration, per integration point | correlation evaluation, a marching step, a residual assembly | plain values, `[<Struct>]` state, a `[<Flags>]` diagnostics enum. **No `Returns`.** |
| **Edge**: runs once per solve or request | "solve this heat exchanger", "load these 48 records" | converts the kernel's flags to `Returns` once, then everything upstream is ordinary ROP |

Inside the kernel, flags combine with `|||` and cost nothing. That also removes the duplicate warnings for free, because 100 nodes that are out of range set the same bit once:

```fsharp
[<Flags>]
type Diag =
    | None         = 0
    | ReBelowRange = 1
    | PrAboveRange = 2
    | Diverged     = 4

[<Struct>]
type NodeResult = { Nu: float; Diag: Diag }

// Kernel: no Returns, no allocation.
let inline nusselt (re: float) (pr: float) : NodeResult =
    let diag = (if re < 3000.0 then Diag.ReBelowRange else Diag.None)
           ||| (if pr > 2000.0 then Diag.PrAboveRange else Diag.None)
    { Nu = 0.023 * re ** 0.8 * pr ** 0.4; Diag = diag }

// Edge: converts once per solve. Only here are messages (and strings) built.
let decode (d: Diag) : Msg list =
    [ if d.HasFlag Diag.ReBelowRange then CorrelationExtrapolated ("Gnielinski", "Re below 3000")
      if d.HasFlag Diag.PrAboveRange then CorrelationExtrapolated ("Gnielinski", "Pr above 2000") ]

let solve (nodes: Node[]) : Returns<Profile, Msg> =
    let mutable diag = Diag.None
    let nu = Array.zeroCreate nodes.Length
    for i in 0 .. nodes.Length - 1 do
        let r = nusselt nodes.[i].Re nodes.[i].Pr
        nu.[i] <- r.Nu
        diag <- diag ||| r.Diag
    if diag.HasFlag Diag.Diverged then Returns.fail Diverged
    else Returns.warnmany (decode diag) (Profile nu)
```

If per-node detail matters (for example, which nodes were out of range), keep the flags per node in a scratch `Diag[]` and decode that at the edge instead.

### Combinators on each side

- **Kernel:** none from this library. That is deliberate: a `Returns` inside the kernel is the signal that the boundary is in the wrong place.
- **Edge conversion:** `Returns.ok`, `Returns.warnmany`, `Returns.fail`/`failmany`. For kernels that already return `Result`, use `Returns.ofPlainResult`.
- **Warm paths** (run per solve, not per node) can use `Returns` freely. Three things keep them cheap:
  - `warnIfLazy`, so messages are formatted only when a warning is actually raised.
  - `traverseArray`/`traverseArrayFailFast`, which avoid the intermediate lists that `traverseList` builds.
  - `dedupeWarnings`/`summariseWarnings`, applied once before reporting.

## When to revisit

Revisit this if a profile of a real consumer shows `Returns` allocation dominating a path where a flags enum can't express the diagnostics, for example when every warning needs a distinct runtime payload. If that happens, measure again with `Bench/AllocProbe` first. The fair comparison is the struct form against the v1.1.0 figures above, not against v1.0.2.
