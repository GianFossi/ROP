// ****************************************************************************************************** //
// ****************************************************************************************************** //
// Author: G.L. Anfossi (ALO/UTEC).
//
// REVISION HYSTORY:
//
// 18/07/2020: Start.
// 28/11/2020: General Revision.
//
// ****************************************************************************************************** //
// ****************************************************************************************************** //

/// <summary>
/// Contains error propagation functions and a computation expression builder for Railway-Oriented-Programming (ROP).
/// </summary>
namespace ROP

/// <summary>
/// Additional operations on Choice (from FSharpPlus: https://github.com/fsprojects/FSharpPlus/blob/master/src/FSharpPlus/Extensions/Choice.fs).
/// By convention here, <c>Choice1Of2</c> plays the role of "Success" and <c>Choice2Of2</c> the role of "Failure".
/// Usable independently of the <c>Returns</c> type/module.
/// </summary>
[<RequireQualifiedAccess>]
module Choice =

    /// <summary>Creates a Choice1Of2 ("success") with the supplied value.</summary>
    /// <param name="x">The value to wrap.</param>
    /// <returns><c>Choice1Of2 x</c>.</returns>
    let result x = Choice1Of2 x

    /// <summary>Creates a Choice2Of2 ("failure") with the supplied value.</summary>
    /// <param name="x">The value to wrap.</param>
    /// <returns><c>Choice2Of2 x</c>.</returns>
    let throw x = Choice2Of2 x

    /// <summary>Applies the wrapped value to the wrapped function when both are Choice1Of2 and returns a wrapped result or the first Choice2Of2.
    /// This is as if Choice1Of2 respresents a Success value and Choice2Of2 a Failure.</summary>
    /// <param name="f">The function wrapped in a Choice1Of2 or a Choice2Of2.</param>
    /// <param name="x">The value wrapped in a Choice1Of2 or a Choice2Of2.</param>
    /// <returns>A Choice1Of2 of the function applied to the value, or the first <c>Choice2Of2</c> if either the function or the value is <c>Choice2Of2</c>.</returns>
    let apply f (x: Choice<'T,'Error>) : Choice<'U,'Error> =
        match f, x with
        // Applicative semantics: both sides are supplied already-computed; only invoke the function when
        // BOTH are Choice1Of2, otherwise propagate whichever side is a Choice2Of2 (function side checked first).
        | Choice1Of2 a, Choice1Of2 b -> Choice1Of2 (a b)
        | Choice2Of2 e, _ | _, Choice2Of2 e -> Choice2Of2 e

    /// <summary>Maps the value on the Choice1Of2 if any.</summary>
    /// <param name="mapping">A function to apply to the Choice1Of2 value.</param>
    /// <param name="source">The source input value.</param>
    /// <returns>A Choice1Of2 of the input value after applying the mapping function, or the original Choice2Of2 value if the input is Choice2Of2.</returns>
    let map (mapping: 'T->'U) (source: Choice<'T,'T2>) =
        match source with
        | Choice1Of2 v -> Choice1Of2 (mapping v)
        | Choice2Of2 e -> Choice2Of2 e

    /// <summary>Creates a Choice value from a pair of Choice values, using a function to combine the Choice1Of2 values.</summary>
    /// <param name="f">The function used to function to combine the Choice1Of2 values.</param>
    /// <param name="x">The first Choice value.</param>
    /// <param name="y">The second Choice value.</param>
    /// <returns>The combined value, or the first Choice2Of2.</returns>
    let map2 f (x: Choice<'T,'Error>) (y: Choice<'U,'Error>) : Choice<'V,'Error> =
        match x, y with
        // Both sides are always evaluated (already-computed values passed in); only combine via `f` when
        // both are Choice1Of2, otherwise propagate whichever side is a Choice2Of2 (x checked first).
        | Choice1Of2 a, Choice1Of2 b -> Choice1Of2 (f a b)
        | Choice2Of2 e, _ | _, Choice2Of2 e -> Choice2Of2 e

    /// <summary>Creates a Choice value from three Choice values, using a function to combine the Choice1Of2 values.</summary>
    /// <param name="f">The function used to combine the three Choice1Of2 values.</param>
    /// <param name="x">The first Choice value.</param>
    /// <param name="y">The second Choice value.</param>
    /// <param name="z">The third Choice value.</param>
    /// <returns>The combined value, or the first Choice2Of2 encountered (x, then y, then z).</returns>
    let map3 f (x: Choice<'T,'Error>) (y: Choice<'U,'Error>) (z: Choice<'V,'Error>) : Choice<'W,'Error> =
        match x, y, z with
        | Choice1Of2 a, Choice1Of2 b, Choice1Of2 c -> Choice1Of2 (f a b c)
        | Choice2Of2 e, _, _ | _, Choice2Of2 e, _ | _, _, Choice2Of2 e -> Choice2Of2 e

    /// <summary>
    /// Like <see cref="apply"/>, but when BOTH sides are a Choice2Of2, combines them with <paramref name="combiner"/>
    /// instead of only keeping the first one. Useful to accumulate failures (e.g. with a list-typed 'Error and
    /// <c>combiner = (@)</c>) without switching to the <c>Returns</c> type.
    /// </summary>
    /// <param name="combiner">Combines two Choice2Of2 values into one, when both sides fail.</param>
    /// <param name="f">The function to apply to both Choice1Of2 values when both sides succeed.</param>
    /// <param name="x">The first input Choice.</param>
    /// <param name="y">The second input Choice.</param>
    /// <returns>A Choice1Of2 of the combined value, the single Choice2Of2 when only one side failed, or the combined Choice2Of2 when both failed.</returns>
    let apply2With combiner f (x: Choice<'T, 'Error>) (y: Choice<'U, 'Error>) : Choice<'V, 'Error> =
        match x, y with
        | Choice1Of2 a, Choice1Of2 b -> Choice1Of2 (f a b)
        // Exactly one side failed: that single failure propagates unchanged (nothing to combine yet).
        | Choice2Of2 e, Choice1Of2 _ | Choice1Of2 _, Choice2Of2 e -> Choice2Of2 e
        // Both sides failed: this is what distinguishes apply2With from plain apply/map2.
        | Choice2Of2 e1, Choice2Of2 e2 -> Choice2Of2 (combiner e1 e2)

    /// <summary>Three-argument version of <see cref="apply2With"/>, combining every Choice2Of2 encountered via <paramref name="combiner"/>.</summary>
    /// <param name="combiner">Combines two Choice2Of2 values into one; applied pairwise when two or more sides fail.</param>
    /// <param name="f">The function to apply to all three Choice1Of2 values when every side succeeds.</param>
    /// <param name="x">The first input Choice.</param>
    /// <param name="y">The second input Choice.</param>
    /// <param name="z">The third input Choice.</param>
    /// <returns>A Choice1Of2 of the combined value, the single Choice2Of2 when only one side failed, or the combined Choice2Of2 when two or more failed.</returns>
    let apply3With combiner f (x: Choice<'T, 'Error>) (y: Choice<'U, 'Error>) (z: Choice<'V, 'Error>) : Choice<'W, 'Error> =
        match x, y, z with
        | Choice1Of2 a, Choice1Of2 b, Choice1Of2 c -> Choice1Of2 (f a b c)
        // Exactly one side failed: propagate that single failure.
        | Choice2Of2 e, Choice1Of2 _, Choice1Of2 _ | Choice1Of2 _, Choice2Of2 e, Choice1Of2 _ | Choice1Of2 _, Choice1Of2 _, Choice2Of2 e -> Choice2Of2 e
        // Exactly two sides failed: combine just those two failures.
        | Choice1Of2 _, Choice2Of2 e1, Choice2Of2 e2 | Choice2Of2 e1, Choice1Of2 _, Choice2Of2 e2 | Choice2Of2 e1, Choice2Of2 e2, Choice1Of2 _ -> Choice2Of2 (combiner e1 e2)
        // All three sides failed: fold all three failures together via the combiner.
        | Choice2Of2 e1, Choice2Of2 e2, Choice2Of2 e3 -> Choice2Of2 (combiner (combiner e1 e2) e3)

    /// <summary>Flattens two nested Choice.</summary>
    /// <param name="source">The nested Choice.</param>
    /// <returns>A single Choice1Of2 of the value when it was nested with Choice1Of2s, or the Choice2Of2.</returns>
    /// <remarks><c>flatten</c> is equivalent to <c>bind id</c>.</remarks>
    let flatten source : Choice<'T1,'T2> =
        match source with
        // Only a Choice1Of2-wrapping-a-Choice1Of2 collapses to a plain Choice1Of2; a Choice2Of2 at either
        // level (outer, or nested inside the outer Choice1Of2) surfaces as the flattened Choice2Of2.
        | Choice1Of2 (Choice1Of2 v) -> Choice1Of2 v
        | Choice1Of2 (Choice2Of2 e) | Choice2Of2 e -> Choice2Of2 e

    /// <summary>If the input value is a Choice2Of2 leaves it unchanged, otherwise maps the value on the Choice1Of2 and flattens the resulting nested Choice.</summary>
    /// <param name="binder">A function that takes the value of type T and transforms it into a Choice containing (potentially) a value of type U.</param>
    /// <param name="source">The source input value.</param>
    /// <returns>A result of the output type of the binder.</returns>
    let bind (binder: 'T->Choice<'U,'T2>) (source: Choice<'T,'T2>) =
        match source with
        // On Choice1Of2, hand the value to the binder and use its result directly (this is what performs the
        // implicit "flatten" - the binder already returns a Choice, so there's no extra nesting to collapse).
        | Choice1Of2 v -> binder v
        | Choice2Of2 e -> Choice2Of2 e

    /// <summary>Obsolete alias kept for backward compatibility; use <see cref="bindChoice2Of2"/> instead.</summary>
    /// <param name="f">The function applied to a Choice2Of2 value, producing a new Choice.</param>
    [<System.Obsolete("Use Choice.bindChoice2Of2")>]
    let inline catch (f: 't -> _) = function Choice1Of2 v -> Choice1Of2 v | Choice2Of2 e -> f e : Choice<'v,'e>

    /// <summary>If the input value is a Choice1Of2 leaves it unchanged, otherwise maps the value on the Choice2Of2 and flattens the resulting nested Choice.</summary>
    /// <param name="binder">A function that takes the value of type T and transforms it into a Choice containing (potentially) a value of type U.</param>
    /// <param name="source">The source input value.</param>
    /// <returns>A result of the output type of the binder.</returns>
    let bindChoice2Of2 (binder: 'T2->Choice<'T,'U2>) (source: Choice<'T,'T2>) =
        match source with
        // Mirror image of `bind`: Choice1Of2 passes through untouched, and the "error-recovery" binder only
        // runs against a Choice2Of2 value, giving it the chance to turn a failure back into a success.
        | Choice1Of2 v -> Choice1Of2 v
        | Choice2Of2 e -> binder e

    /// <summary>Extracts a value from either side of a Choice.</summary>
    /// <param name="fChoice1Of2">Function to be applied to source, if it contains a Choice1Of2 value.</param>
    /// <param name="fChoice2Of2">Function to be applied to source, if it contains a Choice2Of2 value.</param>
    /// <param name="source">The source value, containing a Choice1Of2 or a Choice2Of2.</param>
    /// <returns>The result of applying either functions.</returns>
    let inline either fChoice1Of2 fChoice2Of2 source =
        match source with
        | Choice1Of2 v -> fChoice1Of2 v
        | Choice2Of2 e -> fChoice2Of2 e

    /// <summary>Creates a safe version of the supplied function, which returns a Choice&lt;'U,exn&gt; instead of throwing exceptions.</summary>
    /// <param name="f">The function to protect (that may raise an exception).</param>
    /// <param name="x">The argument to apply <paramref name="f"/> to.</param>
    /// <returns>A Choice1Of2 of the function's result, or a Choice2Of2 carrying the caught exception.</returns>
    let protect (f: 'T->'U) x =
        try
            Choice1Of2 (f x)
        // Note: unlike Result.tryCatch/Returns.tryCatch, every exception type is caught here (including
        // OutOfMemoryException) - this function is a direct, unmodified port of the FSharpPlus original.
        with e -> Choice2Of2 e
