# Zero-allocation paths and `Returns`

**Decision (v1.1.0):** there is no `[<Struct>]` variant of `Returns`. Code that must not allocate per evaluation does not use ROP. It uses plain struct values and a bit-flag diagnostics set, and converts to `Returns` **once**, at a documented boundary. This note explains why, and where that boundary sits.

## What `Returns` actually costs

Measured with [`Bench/AllocProbe`](../Bench/AllocProbe/Program.fs), in bytes allocated per call, averaged over 5M calls after warm-up. To reproduce, run `dotnet run -c Release --project Bench/AllocProbe --framework net8.0` (or `net10.0`):

| Scenario | v1.0.2 net8.0 | v1.0.2 net10.0 | v1.1.0 net8.0 | v1.1.0 net10.0 |
| --- | ---: | ---: | ---: | ---: |
| Plain `float` arithmetic (baseline) | 0 B | 0 B | 0 B | 0 B |
| `Returns.ok x` | 32 B | 32 B | 32 B | 32 B |
| `ok x >>= s >>= s >>= s`, no warnings | 560 B | 408 B | **128 B** | **128 B** |
| `returns { let! … ×3 }`, no warnings | 448 B | 349 B | **160 B** | **160 B** |
| 3 steps, each adding one warning | 816 B | 662 B | **448 B** | **448 B** |
| `warnIf` (false) with an interpolated message | 312 B | 312 B | 312 B | 312 B |
| `warnIfLazy` (false), same message | n/a | n/a | **32 B** | **32 B** |
| *Prototype* struct variant, 3 binds, no warnings | 0 B | 0 B | 0 B | 0 B |
| *Prototype* struct variant, one warning | 64 B | 64 B | 64 B | 64 B |

The v1.1.0 figures include a fix to `bind`/`jointMessages`. Before it, both allocated closures on every call, and `jointMessages` re-wrapped results that had no warnings to merge. Semantics are unchanged, and the whole existing test suite still passes. With the fix, a clean pipeline costs exactly one `Success` object (32 B) per step on both runtimes. That is the minimum for a reference-type union.

The fractional v1.0.2 figures on net10.0 come from the .NET 10 JIT, which removes some of those closures itself, but only sometimes. After the fix, the result no longer depends on what the JIT happens to do.

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
