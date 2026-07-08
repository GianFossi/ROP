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
/// Additional operations on Option (from FSharpPlus: https://github.com/fsprojects/FSharpPlus/blob/master/src/FSharpPlus/Extensions/Option.fs).
/// Usable independently of the <c>Returns</c> type/module.
/// </summary>
[<RequireQualifiedAccess>]
module Option =

    /// <summary>Applies an option value to an option function.</summary>
    /// <param name="f">The option function.</param>
    /// <param name="x">The option value.</param>
    /// <returns>An option of the function applied to the value, or <c>None</c> if either the function or the value is <c>None</c>.</returns>
    let apply f (x: option<'T>) : option<'U> =
        match f, x with
        // Applicative semantics: the function only gets invoked when BOTH sides are Some; any None on
        // either side (function or value) makes the whole application None.
        | Some f, Some x -> Some (f x)
        | _              -> None

    /// <summary>If value is Some, returns both of them tupled. Otherwise it returns None tupled.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The resulting tuple.</returns>
    let unzip (v: option<'T * 'U>) =
        match v with
        // A Some pair splits into two Somes (one per component); a None yields a pair of Nones rather than None itself.
        | Some (x, y) -> Some x, Some y
        | _           -> None  , None

    /// <summary>If both value are Some, returns both of them tupled. Otherwise it returns None.</summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The resulting option.</returns>
    let zip x y : option<'T * 'U> =
        match x, y with
        // The inverse of unzip: both inputs must be Some for the tupled result to be Some.
        | Some x, Some y -> Some (x, y)
        | _              -> None

    /// <summary>If all 3 values are Some, returns them tupled. Otherwise it returns None.</summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <param name="z">The third value.</param>
    /// <returns>The resulting option.</returns>
    let zip3 x y z : option<'T * 'U * 'V> =
        match x, y, z with
        // Same all-or-nothing rule as zip, extended to three values.
        | Some x, Some y, Some z -> Some (x, y, z)
        | _                      -> None

    /// <summary>Converts an option to a Result.</summary>
    /// <param name="source">The option value.</param>
    /// <returns>The resulting Result value.</returns>
    let toResult (source: option<'T>) =
        match source with
        // No error value is available here, so a bare Error () is used to signal "was None" - use
        // toResultWith below when a meaningful error payload is needed.
        | Some x -> Ok x
        | None -> Error ()

    /// <summary>Converts an option to a Result.</summary>
    /// <param name="errorValue">The error value to be used in case of None.</param>
    /// <param name="source">The option value.</param>
    /// <returns>The resulting Result value.</returns>
    let toResultWith (errorValue: 'Error) (source: 'T option) =
        match source with
        | Some x -> Ok x
        | None -> Error errorValue

    /// <summary>Converts a Result to an option.</summary>
    /// <remarks>The error value (if any) is lost.</remarks>
    /// <param name="source">The Result value.</param>
    /// <returns>The resulting option value.</returns>
    let ofResult (source: Result<'T,'Error>) =
        match source with
        // The Error payload is intentionally discarded - this conversion is inherently lossy in that direction.
        | Ok x -> Some x
        | Error _ -> None

    /// <summary>Creates a safe version of the supplied function, which returns an option&lt;'U&gt; instead of throwing exceptions.</summary>
    /// <param name="f">The function to protect (that may raise an exception).</param>
    /// <param name="x">The argument to apply <paramref name="f"/> to.</param>
    /// <returns><c>Some</c> of the function's result, or <c>None</c> if it threw (the exception itself is discarded).</returns>
    let protect (f: 'T->'U) x =
        try
            Some (f x)
        // Every exception is swallowed here (unlike Result.tryCatch/Returns.tryCatch, which preserve the
        // exception as the Error payload) because `option` has no slot to carry error details in.
        with _ -> None

    /// <summary>Converts pair of bool and value to Option.</summary>
    /// <remarks>Useful for handling C# try pattern with `out` parameter. E.g. `Int.TryParse` or `Dictionary.TryGetValue`.</remarks>
    /// <param name="pair">Pair of bool and value.</param>
    /// <returns><c>Some</c> if bool is `true`, <c>None</c> otherwise.</returns>
    let ofPair (pair: (bool * 'T)) =
        match pair with
        | (true,  x) -> Some x
        | (false, _) -> None

    /// <summary>Extracts a value from either side of an Option, for parity with <c>Returns.either</c>/<c>Result.either</c>/<c>Choice.either</c>.</summary>
    /// <param name="fSome">Function to be applied to source, if it contains a Some value.</param>
    /// <param name="fNone">Function to be invoked, if the source is None.</param>
    /// <param name="source">The source value, containing a Some or a None.</param>
    /// <returns>The result of applying either function.</returns>
    let inline either fSome fNone source =
        match source with
        | Some v -> fSome v
        | None -> fNone ()

    /// <summary>Ignores the value inside the option, if any.</summary>
    /// <param name="source">The option value.</param>
    /// <returns><c>Some ()</c> if the option is <c>Some</c>, <c>None</c> otherwise.</returns>
    let ignore (source: option<'T>) =
        match source with
        | Some _ -> Some ()
        | None   -> None
