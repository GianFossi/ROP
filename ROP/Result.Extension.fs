// ****************************************************************************************************** //
// ****************************************************************************************************** //
// Author: G.L. Anfossi (ALO/UTEC).
//
// REVISION HYSTORY:
//
// 18/07/2020: Start.
// 21/11/2021: General Revision.
//
// ****************************************************************************************************** //
// ****************************************************************************************************** //

/// <summary>
/// Contains error propagation functions and a computation expression builder for Railway-Oriented-Programming (ROP).
/// </summary>
namespace ROP

open System

/// <summary>
/// Additional operations on Result&lt;'T,'Error&gt; (from FSharpPlus: https://github.com/fsprojects/FSharpPlus/blob/master/src/FSharpPlus/Extensions/Result.fs).
/// These are usable independently of the <c>Returns</c> type/module.
/// </summary>
module Result =

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Result</c> container and extracts the Ok value or use the supplied default value when it's an Error.
    /// </summary>
    /// <param name="value">The default value to be used in case of Erroro status.</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>The Ok value, or <paramref name="value"/> when the input is an Error.</returns>
    let defaultValue (value:'T) (source: Result<'T,'Error>) : 'T = match source with Ok v -> v | _ -> value

    /// <summary>
    /// Takes a <c>Result</c> container and extracts the Ok value or use the supplied function value to get a default value when it's an Error.
    /// </summary>
    /// <param name="compensation">The default value to be used in case of Erroro status.</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>The Ok value, or the result of applying <paramref name="compensation"/> to the error when the input is an Error.</returns>
    let defaultWith (compensation: 'Error->'T) (source: Result<'T,'Error>) : 'T = match source with Ok v -> v | Error e -> compensation e

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Result</c> container and extract its Value in case of Success.
    /// Otherwise, raise/throw an exception with the list of Failure Messages associated to the Failure status.
    /// </summary>
    /// <param name="source">The input <c>Returns</c> (result) type.</param>
    /// <returns>The Ok value.</returns>
    /// <exception cref="System.Exception">Thrown (via <c>failwith</c>) when the input is an Error, with the error's <c>ToString()</c> as the exception message.</exception>
    let valueOrFailwith (source: Result<'T,'Error>) : 'T =
        match source with
        | Ok s -> s
        | Error err -> err |> (sprintf "%O") |> failwith

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Creates a safe version of the supplied function, which returns a Result instead of throwing exceptions.
    /// </summary>
    /// <param name="f">The supplied function (that may fail raising an exceptions).</param>
    /// <param name="value">The argument value of the supplied function.</param>
    /// <returns>An Ok of the function's result, or an Error carrying the caught exception.</returns>
    let tryCatch (f: 'T->'U) (value:'T) : Result<'U,exn>=
        try
            Ok (f value)
        with
        // OutOfMemoryException is deliberately NOT caught: swallowing it would let the process limp along in
        // an unreliable state instead of terminating, which is generally worse than letting it propagate.
        | ex when not (ex :? OutOfMemoryException) -> Error ex

    /// <summary>
    /// Creates a safe version of the supplied function, which returns a Result instead of throwing exceptions.
    /// </summary>
    /// <param name="f">The supplied function (that may fail raising an exceptions).</param>
    /// <param name="value">The argument value of the supplied function.</param>
    /// <returns>An Ok of the function's result, or an Error carrying the caught exception.</returns>
    let protect (f: 'T->'U) (value:'T) : Result<'U,exn>=
        // Simple alias kept for API-naming parity with Choice.protect / Option.protect.
        tryCatch f value

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Result</c> container container and transfor it into an Active Pattern result (|Pass|Fail|).
    /// </summary>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns><c>Pass value</c> for Ok; <c>Fail msgs</c> for Error.</returns>
    let (|Pass|Fail|) (source: Result<'T,'Error>) =
      match source with
      | Ok value -> Pass  value
      | Error msgs -> Fail msgs

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Result</c> container and check if it is Ok.
    /// </summary>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>Returns True if the input <c>Result</c> container is Ok, otherwise False if it is a Error.</returns>
    let isOk (source: Result<'T,'Error>) : bool =
        match source with
        | Ok _ -> true
        | _ -> false

    /// <summary>
    /// Takes a <c>Result</c> container and check if it is an Error.
    /// </summary>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>Returns True if the input <c>Result</c> container is a Error, otherwise False if it is Ok.</returns>
    let isError (source: Result<'T,'Error>) : bool =
        match source with
        | Error _ -> true
        | _ -> false

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Converts a <c>Result</c> container into a Option container.
    /// </summary>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns><c>Some value</c> for Ok; <c>None</c> for Error (the error value is discarded).</returns>
    let toOption (source: Result<'T,'Error>) : 'T option =
        match source with
        | Ok x    -> Some x
        | Error _ -> None

    /// <summary>
    /// Creates a <c>Result</c> container from a Option.
    /// </summary>
    /// <param name="errorMesssage">The error to use when the option is <c>None</c>.</param>
    /// <param name="option">The input option type.</param>
    /// <returns>An Ok of the option's value when <c>Some</c>; otherwise an Error carrying <paramref name="errorMesssage"/>.</returns>
    let ofOption (errorMesssage:'Error ) (option: Option<'T>) =
        match option with
        | Some x-> Ok x
        | None ->  Error errorMesssage

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Converts a <c>Result</c> container into a Choice container.
    /// </summary>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns><c>Choice1Of2 value</c> for Ok; <c>Choice2Of2 error</c> for Error.</returns>
    let toChoice (source: Result<'T,'Error>) =
        match source with
        | Ok x-> Choice1Of2 x
        | Error x -> Choice2Of2 x

    /// <summary>
    /// Creates a <c>Result</c> container from a Choice.
    /// </summary>
    /// <param name="source">The input choice type: Choice1Of2 for Ok, Choice2Of2 for Error.</param>
    /// <returns>An Ok wrapping the Choice1Of2 value, or an Error wrapping the Choice2Of2 value.</returns>
    let ofChoice (source: Choice<'T,'Error>) =
        match source with
        | Choice1Of2 x-> Ok x
        | Choice2Of2 x -> Error x

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **    EITHER     ***
    // ********************

    /// <summary>
    /// Takes an input <c>Result</c> container, and:
    ///
    /// if it is a Ok, maps its Ok Value with the given "fOk" function;
    ///
    /// if it is a Error, maps it Error Messages with the given "fError" function.
    /// </summary>
    /// <param name="fOk">Function to be applied to source, if it contains an Ok value.</param>
    /// <param name="fError">Function to be applied to source, if it contains an Error value.</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>The result of applying either functions.</returns>
    let inline either (fOk) (fError) (source: Result<'T,'Error>) =
        match source with
        | Ok v -> fOk v
        | Error e -> fError e

    // -------------------------------------------------------------------------------------- //

    // *********************
    // ** JOINT MESSAGE/S **
    // *********************

    // Not Applicable.

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **     BIND      ***
    // ********************

    // Already defined inside the Fsharp.Core

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a given function ( 'T -> 'U ) [already wrapped inside a Ok or Error <c>Result</c> container] and an input <c>Result</c> container, then:
    ///
    /// if both the <c>Result</c> containers are a Ok, maps the given Ok Value, with the given "okFunction" function
    ///
    /// otherwise the existing error messages of one of the two function and input <c>Result</c> containers container is propagated.
    /// </summary>
    /// <param name="wrappedOkFunction">The function wrapped in an Ok or an Error.</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>An Ok of the function applied to the value, or the first <c>Error</c> if either the function or the value is <c>Error</c>.</returns>
    let inline apply (wrappedOkFunction : Result<'T -> 'U,'Error>) (source: Result<'T,'Error>) : Result<'U,'Error> =
        match wrappedOkFunction, source with
        // Applicative semantics: unlike bind, this does not chain one Result into producing another - it just
        // combines two already-computed Results, propagating whichever Error comes first (function side checked first).
        | Ok f, Ok x -> Ok (f x)
        | Error e, _ -> Error e
        | _, Error e -> Error e

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **  LIFT or MAP  ***
    // ********************

    // -------------------------------------------------------------------------------------- //

    // Already defined inside the Fsharp.Core

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Maps the given function over all existing warning and failure messages of a <c>Result</c> (result) container (if any).
    /// It works as a sort of Trasformation/Conversion Function of the warning and failure message type.
    /// </summary>
    /// <param name="conversionFunction">The conversion function (common for both the Warning and Error Message).</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>The Ok value untouched, or an Error with its value transformed by <paramref name="conversionFunction"/>.</returns>
    let mapError (conversionFunction: 'Error1 -> 'Error2) (source: Result<'T,'Error1>) =
        match source with
        | Ok x -> Ok x
        | Error error ->
            // Only the Error payload is transformed; the Ok case is passed straight through unchanged.
            let error' = conversionFunction error
            Error error'

    /// <summary>
    /// Maps both sides of a <c>Result</c> in a single call: the Ok value with <paramref name="okMapper"/>,
    /// or the Error value with <paramref name="errorMapper"/>.
    /// </summary>
    /// <param name="errorMapper">Function applied to the Error value.</param>
    /// <param name="okMapper">Function applied to the Ok value.</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>The same case (Ok/Error), with its payload transformed by the matching mapper.</returns>
    let bimap (errorMapper: 'Error1 -> 'Error2) (okMapper: 'T1 -> 'T2) (source: Result<'T1,'Error1>) : Result<'T2,'Error2> =
        match source with
        | Ok a -> Ok (okMapper a)
        | Error e -> Error (errorMapper e)

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// If the input is Ok, leaves it unchanged; otherwise maps the Error value through <paramref name="binder"/>,
    /// which may itself turn the failure back into an Ok (e.g. a fallback/retry/recovery value). This is the
    /// Error-side counterpart of <c>Result.bind</c> (which only ever chains on the Ok side).
    /// </summary>
    /// <param name="binder">A function that takes the error and transforms it into a new Result (possibly recovering into an Ok).</param>
    /// <param name="source">The source input value.</param>
    /// <returns>The original Ok, or the result of applying <paramref name="binder"/> to the error.</returns>
    let inline bindError (binder: 'Error1 -> Result<'T,'Error2>) (source: Result<'T,'Error1>) : Result<'T,'Error2> =
        match source with
        | Ok v -> Ok v
        | Error e -> binder e

    /// <summary>Like <see cref="bindError"/> but with flipped arguments, convenient for piping: <c>source |> Result.catch recover</c>.</summary>
    /// <param name="source">The source input value.</param>
    /// <param name="f">A function that takes the error and transforms it into a new Result (possibly recovering into an Ok).</param>
    /// <returns>The original Ok, or the result of applying <paramref name="f"/> to the error.</returns>
    let inline catch (source: Result<'T,'Error1>) (f: 'Error1 -> Result<'T,'Error2>) : Result<'T,'Error2> =
        bindError f source

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Creates a Result value from a pair of Result values, using a function to combine all of them.
    /// That is: <c>apply (apply (Ok f) x) y)</c>
    /// </summary>
    /// <param name="f">The function used to combine the content of the Successful values of the given input.</param>
    /// <param name="x">The first Result value.</param>
    /// <param name="y">The second Result value.</param>
    /// <returns>The combined value, or the first Error.</returns>
    let map2 f (x: Result<'T1,'Error>) (y: Result<'T2,'Error>) : Result<'T3,'Error> =
        // Standard applicative-style lifting: wrap the curried function in an Ok, then apply it to each
        // argument in turn via `apply`.
        Ok f
        |> apply <| x
        |> apply <| y
        //match x, y with
        //| Ok a, Ok b -> Ok (f a b)
        //| Error e, _ | _, Error e -> Error e

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Creates a Result value from 3 Result values, using a function to combine all of them.
    /// That is: <c>apply (apply (apply (Ok f) x) y) z)</c>
    /// </summary>
    /// <param name="f">The function used to combine the content of the Successful values of the given input.</param>
    /// <param name="x">The first Result value.</param>
    /// <param name="y">The second Result value.</param>
    /// <param name="z">The Third Result value.</param>
    /// <returns>The combined value, or the first Error.</returns>
    let map3 f (x: Result<'T,'Error>) (y: Result<'U,'Error>) (z: Result<'U,'Error>) : Result<'V,'Error> =
        // Same applicative-lift pattern as map2, extended with a third `apply`.
        Ok f
        |> apply <| x
        |> apply <| y
        |> apply <| z
        //match x, y, z  with
        //| Ok a, Ok b, Ok c -> Ok ( f a b c )
        //| Error e, _, _ | _, Error e, _ | _, _, Error e -> Error e

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Creates a Result value from 4 Result values, using a function to combine all of them.
    /// That is: <c>apply (apply (apply (apply (map okFunction source1) source2) source3) source4)</c>
    /// </summary>
    /// <param name="okFunction">The function used to combine the content of the Successful values of the given inputs.</param>
    /// <param name="source1">The first Result value.</param>
    /// <param name="source2">The second Result value.</param>
    /// <param name="source3">The third Result value.</param>
    /// <param name="source4">The fourth Result value.</param>
    /// <returns>The combined value, or the first Error.</returns>
    let inline map4 (okFunction : 'T -> 'U -> 'V -> 'W -> 'Z)
                    (source1: Result<'T,'Error>)
                    (source2: Result<'U,'Error>)
                    (source3: Result<'V,'Error>)
                    (source4: Result<'W,'Error>) : Result<'Z,'Error> =
        // Same applicative-lift pattern as map2/map3, starting from Result.map on the first source instead
        // of wrapping the function in Ok explicitly (equivalent, just one fewer step).
        source1
        |> Result.map okFunction
        |> apply <| source2
        |> apply <| source3
        |> apply <| source4

    // -------------------------------------------------------------------------------------- //

    // ***************************
    // ** ACCUMULATING (..With) **
    // ***************************

    /// <summary>
    /// Like <see cref="apply"/>, but when BOTH sides are an Error, combines them with <paramref name="combiner"/>
    /// instead of only keeping the first one. Useful to accumulate errors on a plain <c>Result&lt;'T,'Error&gt;</c>
    /// (e.g. with <c>'Error = 'Msg list</c> and <c>combiner = (@)</c>) without switching to the <c>Returns</c> type.
    /// </summary>
    /// <param name="combiner">Combines two Error values into one, when both sides fail.</param>
    /// <param name="mapper">The function to apply to both Ok values when both sides succeed.</param>
    /// <param name="source1">The first input <c>Result</c>.</param>
    /// <param name="source2">The second input <c>Result</c>.</param>
    /// <returns>An Ok of the combined value, the single Error when only one side failed, or the combined Error when both failed.</returns>
    let apply2With combiner (mapper: 'T1 -> 'T2 -> 'U) (source1: Result<'T1,'Error>) (source2: Result<'T2,'Error>) : Result<'U,'Error> =
        match source1, source2 with
        | Ok a, Ok b -> Ok (mapper a b)
        // Exactly one side failed: that single error propagates unchanged (nothing to combine yet).
        | Error e, Ok _ | Ok _, Error e -> Error e
        // Both sides failed: this is what distinguishes apply2With from plain apply/map2.
        | Error e1, Error e2 -> Error (combiner e1 e2)

    /// <summary>Three-argument version of <see cref="apply2With"/>, combining every Error encountered (in left-to-right order) via <paramref name="combiner"/>.</summary>
    /// <param name="combiner">Combines two Error values into one; applied pairwise, left-to-right, when two or more sides fail.</param>
    /// <param name="mapper">The function to apply to all three Ok values when every side succeeds.</param>
    /// <param name="source1">The first input <c>Result</c>.</param>
    /// <param name="source2">The second input <c>Result</c>.</param>
    /// <param name="source3">The third input <c>Result</c>.</param>
    /// <returns>An Ok of the combined value, the single Error when only one side failed, or the combined Errors when two or more failed.</returns>
    let apply3With combiner (mapper: 'T1 -> 'T2 -> 'T3 -> 'U) (source1: Result<'T1,'Error>) (source2: Result<'T2,'Error>) (source3: Result<'T3,'Error>) : Result<'U,'Error> =
        match source1, source2, source3 with
        | Ok a, Ok b, Ok c -> Ok (mapper a b c)
        // Exactly one side failed: propagate that single error.
        | Error e, Ok _, Ok _ | Ok _, Error e, Ok _ | Ok _, Ok _, Error e -> Error e
        // Exactly two sides failed: combine just those two errors.
        | Ok _, Error e1, Error e2 | Error e1, Ok _, Error e2 | Error e1, Error e2, Ok _ -> Error (combiner e1 e2)
        // All three sides failed: fold all three errors together via the combiner.
        | Error e1, Error e2, Error e3 -> Error (combiner (combiner e1 e2) e3)

    /// <summary>Alias of <see cref="apply2With"/> under the "map" naming used by <see cref="map2"/>; same accumulating behavior.</summary>
    /// <param name="combiner">Combines two Error values into one, when both sides fail.</param>
    /// <param name="mapper">The function to apply to both Ok values when both sides succeed.</param>
    /// <param name="source1">The first input <c>Result</c>.</param>
    /// <param name="source2">The second input <c>Result</c>.</param>
    /// <returns>An Ok of the combined value, the single Error when only one side failed, or the combined Error when both failed.</returns>
    let map2With combiner mapper (source1: Result<'T1,'Error>) (source2: Result<'T2,'Error>) : Result<'U,'Error> =
        apply2With combiner mapper source1 source2

    /// <summary>Alias of <see cref="apply3With"/> under the "map" naming used by <see cref="map3"/>; same accumulating behavior.</summary>
    /// <param name="combiner">Combines two Error values into one; applied pairwise when two or more sides fail.</param>
    /// <param name="mapper">The function to apply to all three Ok values when every side succeeds.</param>
    /// <param name="source1">The first input <c>Result</c>.</param>
    /// <param name="source2">The second input <c>Result</c>.</param>
    /// <param name="source3">The third input <c>Result</c>.</param>
    /// <returns>An Ok of the combined value, the single Error when only one side failed, or the combined Errors when two or more failed.</returns>
    let map3With combiner mapper (source1: Result<'T1,'Error>) (source2: Result<'T2,'Error>) (source3: Result<'T3,'Error>) : Result<'U,'Error> =
        apply3With combiner mapper source1 source2 source3

    /// <summary>Like <see cref="zip"/> (defined further below), but combines both Errors via <paramref name="combiner"/> instead of only keeping the first one.</summary>
    /// <param name="combiner">Combines two Error values into one, when both sides fail.</param>
    /// <param name="source1">The first input <c>Result</c>.</param>
    /// <param name="source2">The second input <c>Result</c>.</param>
    /// <returns>An Ok of the tupled values, the single Error when only one side failed, or the combined Error when both failed.</returns>
    let zipWith combiner (source1: Result<'T1,'Error>) (source2: Result<'T2,'Error>) : Result<'T1 * 'T2,'Error> =
        // Tupling is just "combine the two Ok values by pairing them" - map2With already implements exactly that shape.
        map2With combiner (fun a b -> (a, b)) source1 source2

    /// <summary>Three-argument version of <see cref="zipWith"/>.</summary>
    /// <param name="combiner">Combines two Error values into one; applied pairwise when two or more sides fail.</param>
    /// <param name="source1">The first input <c>Result</c>.</param>
    /// <param name="source2">The second input <c>Result</c>.</param>
    /// <param name="source3">The third input <c>Result</c>.</param>
    /// <returns>An Ok of the tupled values, the single Error when only one side failed, or the combined Errors when two or more failed.</returns>
    let zip3With combiner (source1: Result<'T1,'Error>) (source2: Result<'T2,'Error>) (source3: Result<'T3,'Error>) : Result<'T1 * 'T2 * 'T3,'Error> =
        map3With combiner (fun a b c -> (a, b, c)) source1 source2 source3

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **    FLATTEN    ***
    // ********************

    /// <summary>
    /// Flattens two nested Results.
    /// </summary>
    /// <param name="source">The nested Results.</param>
    /// <returns>A single Ok of the value when it was nested with OKs, or the Error.</returns>
    /// <remarks><c>flatten</c> is equivalent to <c>bind id</c>.</remarks>
    let flatten (source : Result<Result<'T,'Error>,'Error>) =
        // `bind id` unwraps one layer of nesting: if the outer Result is Ok, its payload is itself
        // already a Result, and `id` hands it straight back to `bind` to be joined in.
        source |> Result.bind id

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **     MERGE     ***
    // ********************
    /// <summary>
    /// Takes two input <c>Result</c> container, then:
    ///
    /// If both the <c>Result</c> containers are a Ok, apply the given merging function on the Ok Value of the two inputs.
    ///
    /// If both the <c>Result</c> containers are a Error, apply the given merging function on the Error Value of the two inputs.
    ///
    /// Otherwise the existing error messages of one of the two function and input <c>Returns</c> (result) container are propagated.
    /// </summary>
    /// <param name="addSuccess">The given function that define the mergin operation on success values.</param>
    /// <param name="addFailure">The given function that define the mergin operation on erro messages.</param>
    /// <param name="source1">The first input <c>Result</c> type.</param>
    /// <param name="source2">The second input <c>Result</c> type.</param>
    /// <returns>The merged Ok, the merged Error, or whichever single Error applies.</returns>
    let inline merge (addSuccess) (addFailure) (source1: Result<'T1,'Error>) (source2: Result<'T2,'Error>) : Result<'T3,'Error> =
        match (source1, source2) with
        | Ok x1, Ok x2 -> Ok ( addSuccess x1 x2 )
        | Error e1, Ok _  -> Error e1
        | Ok _ , Error e2 -> Error e2
        | Error e1, Error e2 -> Error ( addFailure e1 e2 )

    // -------------------------------------------------------------------------------------- //

    //// ********************
    //// **   VALIDATE   ***
    //// ********************

    ///// Merge two <c>Result</c> container, in accordance with the following criteria:
    ///// If both the two inputs are in a Ok status, propagate the first Ok status only.
    ///// If both the two inputs are in a Error status, returns the forst Error status only.
    ///// Otherwise propagate the Ok status of any of the two inputs.
    //let inline validate (source1: Result<'T,'Error list>) (source2: Result<'T,'Error list>) : Result<'T,'Error list>=
    //    // define the behaviour
    //    let addSuccess x1 x2 = x1
    //    let addFailure e1 e2 = e1 @ e2
    //    match (source1, source2) with
    //    | Ok x1, Ok x2 -> Ok (addSuccess x1 x2 )
    //    | Error _, Ok x2  -> Ok x2
    //    | Ok x1 , Error _ -> Ok x1
    //    | Error e1, Error e2 -> Error ( addFailure e1 e2 )

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **     FOLD      ***
    // ********************

    /// <summary>
    /// Folds a sequence of <c>Result</c> values into a single running <c>Result</c>, combining Ok values with
    /// <paramref name="folder"/> and short-circuiting (keeping only the FIRST error encountered, subsequent
    /// errors are discarded) on the first Error.
    /// </summary>
    /// <param name="folder">Combines the running Ok state with each element's Ok value.</param>
    /// <param name="state">The initial (seed) <c>Result</c> value the fold starts from.</param>
    /// <param name="sources">The sequence of <c>Result</c> values to fold over.</param>
    /// <returns>The final accumulated Ok, or the first Error encountered.</returns>
    let inline fold (folder: 'State->'T->'State) (state: Result<'State,'Error>) (sources: Result<'T,'Error> seq) =
        use e = sources.GetEnumerator()
        let mutable state = state
        while e.MoveNext() do
            let addSuccess stateOk currentOk = folder stateOk currentOk
            // Once failed, keep the FIRST error and ignore any subsequent one - this is what distinguishes
            // plain `fold` from the error-accumulating `foldList` below.
            let addFailure stateError _currentError = stateError
            state <- merge addSuccess addFailure state e.Current
        state

    /// <summary>
    /// Variant of fold for Result with list-typed errors that accumulates all errors across failures,
    /// rather than keeping only the first error encountered.
    /// </summary>
    /// <param name="folder">Combines the running Ok state with each element's Ok value.</param>
    /// <param name="state">The initial (seed) <c>Result</c> value the fold starts from.</param>
    /// <param name="sources">The sequence of <c>Result</c> values (with list-typed errors) to fold over.</param>
    /// <returns>The final accumulated Ok, or an Error with every error list concatenated, in order.</returns>
    let inline foldList (folder: 'State->'T->'State) (state: Result<'State,'Error list>) (sources: Result<'T,'Error list> seq) =
        // Accumulate errors in reverse to avoid repeated O(n) appends via "@", then reverse once at the end.
        let revAppend xs ys = List.fold (fun acc x -> x :: acc) ys xs
        use e = sources.GetEnumerator()
        // Track progress as a Choice: Choice1Of2 while still accumulating a running Ok; Choice2Of2 once any
        // element has failed (errors kept reversed until the very end, to avoid re-copying the growing list).
        let mutable acc =
            match state with
            | Ok s       -> Choice1Of2 s
            | Error errs -> Choice2Of2 (List.rev errs)
        while e.MoveNext() do
            acc <-
                match acc, e.Current with
                // Still succeeding, and the next element also succeeds: fold the value in.
                | Choice1Of2 s,       Ok v      -> Choice1Of2 (folder s v)
                // Already failed: subsequent successes contribute nothing further; keep the accumulated errors as-is.
                | Choice2Of2 errsRev, Ok _       -> Choice2Of2 errsRev
                // Still succeeding, but this element fails: switch tracks to failure mode, seeded with its errors.
                | Choice1Of2 _,       Error errs -> Choice2Of2 (List.rev errs)
                // Already failed, and this element fails too: append its errors to the accumulated errors.
                | Choice2Of2 errsRev, Error errs -> Choice2Of2 (revAppend errs errsRev)
        // Un-reverse the accumulated errors exactly once, at the very end, to restore original order.
        match acc with
        | Choice1Of2 s       -> Ok s
        | Choice2Of2 errsRev -> Error (List.rev errsRev)

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **      TEE      ***
    // ********************

    /// <summary>
    /// Takes an input <c>Result</c> container, and:
    ///
    /// if it is a Ok, it executes the given Ok function ('T -> unit) on the Ok Value.
    ///
    /// if it is a Error, it executes the given Error function ('Error -> unit) on the error messages.
    ///
    /// NOTE: the input <c>Returns</c> (result) containe is propagated unchanged.
    /// </summary>
    /// <param name="fOk">Side-effecting function invoked with the value on Ok.</param>
    /// <param name="fError">Side-effecting function invoked with the error on Error.</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>The original <paramref name="source"/>, unchanged.</returns>
    let inline eitherTee fOk fError (source: Result<'T,'Error>) =
        // Runs `f` purely for its side effect, then discards its result and hands back the original `x` -
        // this is what makes `eitherTee` (and successTee/failureTee below) pass-through/transparent.
        let tee f x = f x; x;
        tee (either fOk fError) source

    /// <summary>
    /// Takes an input <c>Result</c> container and executes the given ok function on the (Success) Value and on its warning messages only.
    ///
    /// NOTE: the input <c>Result</c> container is propagated unchanged.
    /// </summary>
    /// <param name="fOk">Side-effecting function invoked with the value on Ok; ignored on Error.</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>The original <paramref name="source"/>, unchanged.</returns>
    let successTee fOk (source: Result<'T,'Error>) =
        eitherTee fOk ignore source

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container and executes the given Failure function on the Error Messages only.
    ///
    /// NOTE: the input <c>Returns</c> (result) container is propagated unchanged.
    /// </summary>
    /// <param name="fError">Side-effecting function invoked with the error on Error; ignored on Ok.</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>The original <paramref name="source"/>, unchanged.</returns>
    let failureTee fError (source: Result<'T,'Error>) =
        eitherTee ignore fError source

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **     LOG       ***
    // ********************

    /// <summary>
    /// Logs the current status of a <c>Result</c> container (its Ok value, or its Error), then propagates it
    /// unchanged. Logging can be toggled off via <paramref name="record"/> without changing the call site.
    /// </summary>
    /// <param name="logger">The sink that receives the formatted log line (e.g. <c>Console.WriteLine</c>, a logging framework call, etc.).</param>
    /// <param name="record">Whether the log action should actually run (TRUE) or be skipped entirely (FALSE).</param>
    /// <param name="message">A description message prefixed to the log line.</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    /// <returns>The original <paramref name="source"/>, unchanged (logging is a side effect only).</returns>
    let log (logger: string -> unit) (record:bool) (message:string) (source: Result<'T,'Error>) =
        // Builds the two possible log lines up-front; only one of them actually gets invoked, based on the case.
        let fOk s = logger (sprintf ">>> %s: Result is Ok: %A" message s)
        let fError err = logger (sprintf ">>> %s: Result is a Error: %A" message err)
        if record then
            // eitherTee both performs the logging side effect and passes the original value straight through.
            eitherTee fOk fError source
        else
            // Logging disabled for this call: skip straight to returning the value unchanged.
            source

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **    SPECIAL     **
    // ********************

    /// Compose two given (SWITCH) functions in series
    /// (i.e.: bind switchFunction1 >> bind switchFunction2).
    ///
    /// The given (SWITCH) functions must return a <c>Result</c> container.
    ///
    /// Note: useful for composing <c>Returns</c> (result) container sequentially.
    /// Note: this composition does not accumulate errors.
    /// Note: the infix operator version is: ">=>".
    /// <param name="switchFunction1">The first switch function to run.</param>
    /// <param name="switchFunction2">The second switch function to run, only if the first one succeeded.</param>
    /// <param name="value">The input value fed into <paramref name="switchFunction1"/>.</param>
    /// <returns>The result of running both switch functions in sequence, short-circuiting on the first Error.</returns>
    let inline compose
        ( switchFunction1: 'T -> Result<'U,'Message>)
        ( switchFunction2: 'U -> Result<'V,'Message>)
        (value : 'T) =

        match ( switchFunction1 value ) with
        // First step succeeded: run the second step against its Ok value.
        | Ok s -> ( switchFunction2 s )
        // First step failed: short-circuit, the second step never runs.
        | Error e -> Error e
        (*

            Alternative code:
                value
                |> bind switchFunction1 >> bind switchFunction2

        *)

    // -------------------------------------------------------------------------------------- //

    /// Applies the given "dead-end function" to a "value" and return the input "value" ignoring the results of the given function.
    /// (">=> toSwitchFunction" is exactly the same as ">> map").
    /// <param name="successFunction">The side-effecting function to run against <paramref name="value"/>; its result is discarded.</param>
    /// <param name="value">The value to pass through unchanged.</param>
    /// <returns><paramref name="value"/>, unchanged.</returns>
    let tee successFunction (value:'T) =
        successFunction value |> ignore
        value

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Creates two lists by classifying the values depending on whether they were wrapped with Ok or Error.
    /// </summary>
    /// <param name="source">The list of <c>Result</c> values to partition.</param>
    /// <returns>
    /// A tuple with both resulting lists, Oks are in the first list.
    /// </returns>
    let partition (source: list<Result<'T,'Error>>) =
        // Tail-recursive single pass, building both accumulator lists in reverse order as it goes.
        let rec loop ((acc1, acc2) as acc) = function
            | [] -> acc
            | x::xs ->
                match x with
                | Ok x -> loop (x::acc1, acc2) xs
                | Error x -> loop (acc1, x::acc2) xs
        // The input is reversed up-front so that, combined with the reverse-prepend accumulation above,
        // both output lists come out in the original input order without a second List.rev pass at the end.
        loop ([], []) (List.rev source)

    // -------------------------------------------------------------------------------------- //

    /// Takes two results and returns a tuple of the pair
    /// <param name="x1">The first <c>Result</c> value.</param>
    /// <param name="x2">The second <c>Result</c> value.</param>
    /// <returns>An Ok of the tupled values if both succeeded; otherwise the first Error encountered (x1 takes priority over x2).</returns>
    let zip x1 x2 =
        match x1, x2 with
        | Ok x1res, Ok x2res -> Ok(x1res, x2res)
        | Error e, _ -> Error e
        | _, Error e -> Error e

    /// <summary>Takes three results and returns a tuple of the triple.</summary>
    /// <param name="x1">The first <c>Result</c> value.</param>
    /// <param name="x2">The second <c>Result</c> value.</param>
    /// <param name="x3">The third <c>Result</c> value.</param>
    /// <returns>An Ok of the tupled values if all three succeeded; otherwise the first Error encountered, in x1/x2/x3 priority order.</returns>
    let zip3 x1 x2 x3 =
        match x1, x2, x3 with
        | Ok x1res, Ok x2res, Ok x3res -> Ok (x1res, x2res, x3res)
        | Error e, _, _ -> Error e
        | _, Error e, _ -> Error e
        | _, _, Error e -> Error e

    /// <summary>The inverse of <see cref="zip"/>: splits a Result of a pair into a pair of Results.</summary>
    /// <param name="source">A Result wrapping a tuple.</param>
    /// <returns>A tuple of two Results: both Ok (one per component) if the source was Ok, or the same Error duplicated into both positions if the source was an Error.</returns>
    let unzip (source: Result<'T1 * 'T2,'Error>) : Result<'T1,'Error> * Result<'T2,'Error> =
        match source with
        // There is only one Error to distribute, so it is duplicated into both output slots.
        | Ok (x, y) -> Ok x, Ok y
        | Error e -> Error e, Error e

    /// <summary>Three-argument version of <see cref="unzip"/>.</summary>
    /// <param name="source">A Result wrapping a 3-tuple.</param>
    /// <returns>A 3-tuple of Results: all Ok (one per component) if the source was Ok, or the same Error duplicated into all three positions if the source was an Error.</returns>
    let unzip3 (source: Result<'T1 * 'T2 * 'T3,'Error>) : Result<'T1,'Error> * Result<'T2,'Error> * Result<'T3,'Error> =
        match source with
        | Ok (x, y, z) -> Ok x, Ok y, Ok z
        | Error e -> Error e, Error e, Error e

    /// <summary>Discards the Ok value, keeping only whether the Result succeeded or its error.</summary>
    /// <param name="source">The Result whose value should be discarded.</param>
    /// <returns><c>Ok ()</c> if <paramref name="source"/> is Ok; otherwise the original Error.</returns>
    let ignore (source: Result<'T,'Error>) : Result<unit,'Error> =
        match source with
        | Ok _ -> Ok ()
        | Error e -> Error e

    // -------------------------------------------------------------------------------------- //

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Infix/operator forms of the core <c>Result</c> combinators, auto-opened alongside the <c>Result</c> module.
    /// </summary>
    [<AutoOpen>]
    module Operators =

        // -------------------------------------------------------------------------------------- //

        /// Applies the given (SWITCH) function
        /// to the Success Value of the given input <c>Returns</c> (result) container,
        /// if the input container is in a Succeeded status,
        /// otherwise
        /// the existing errors of the input container are propagated.
        ///
        /// The given (SWITCH) function must return a <c>Returns</c> (result) container.
        ///
        /// Note: useful for composing <c>Returns</c> (result) container sequentially.
        /// Note: this bind does not accumulate errors.
        /// Note: this is the infix operator version of "Results.bind".
        /// <param name="source">The input <c>Result</c> container.</param>
        /// <param name="successFunction">The switch function to bind with.</param>
        /// <returns>The result of <c>Result.bind successFunction source</c>.</returns>
        let inline (>>=) (source: Result<'T,'Error>) successFunction =
            Result.bind successFunction source
            (*
            static member
                ( >>= ) : returns:Returns<'a,'b> *
                        successFunction:('a -> Returns<'c,'b>) -> Returns<'c,'b>
            *)

        // -------------------------------------------------------------------------------------- //

        /// This is the infix operator version of "Results.compose".
        /// Compose two given (SWITCH) functions in series (RIGHT COMPOSITION)
        /// (i.e.: bind switchFunction1 >> bind switchFunction2).
        ///
        /// The given (SWITCH) functions must return a <c>Returns</c> (result) container.
        ///
        /// Note: useful for composing <c>Returns</c> (result) container sequentially.
        /// Note: this composition does not accumulate errors.
        /// <param name="switchFunction1">The first switch function to run.</param>
        /// <param name="switchFunction2">The second switch function to run, only if the first one succeeded.</param>
        /// <returns>A single function equivalent to running <paramref name="switchFunction1"/> then <paramref name="switchFunction2"/> in sequence.</returns>
        let inline (>=>) switchFunction1 switchFunction2 =
            compose switchFunction1 switchFunction2
        (*
        static member
            ( >=> ) : switchFunction1:('a -> Returns<'b,'c>) *
                    switchFunction2:('b -> Returns<'d,'c>) ->
                        ('a -> Returns<'d,'c>)
        *)

        // -------------------------------------------------------------------------------------- //

        /// This is the infix operator version of "Results.compose".
        /// Compose two given (SWITCH) functions in series (LEFT COMPOSITION)
        /// (i.e.: bind successFunction1 >> bind switchFunction2).
        ///
        /// The given (SWITCH) functions must return a <c>Returns</c> (result) container.
        ///
        /// Note: useful for composing <c>Returns</c> (result) container sequentially.
        /// Note: this composition does not accumulate errors.
        /// <param name="successFunction2">The second (right-hand) switch function, applied last.</param>
        /// <param name="successFunction1">The first (left-hand) switch function, applied first.</param>
        /// <returns>A single function equivalent to running <paramref name="successFunction1"/> then <paramref name="successFunction2"/> in sequence.</returns>
        let inline (<=<) successFunction2 successFunction1 =
            compose successFunction1 successFunction2
        (*
        static member
            ( <=< ) : switchFunction2:('a -> Returns<'b,'c>) *
                        switchFunction1:('b -> Returns<'d,'c>) ->
                        ('a -> Returns<'d,'c>)
        *)

        // -------------------------------------------------------------------------------------- //

        /// This is the infix operator version of "Results.lift".
        /// Applies the given function, lifted/wrapped inside a a Success results (return) container,
        /// to the Success Value of the given input <c>Returns</c> (result) container,
        /// if the input container is in a Succeeded status,
        /// otherwise the existing error of the input container is propagated.
        /// <param name="successFunction">The plain function to lift and map with.</param>
        /// <returns>A function <c>Result&lt;'a,'c&gt; -&gt; Result&lt;'b,'c&gt;</c> equivalent to <c>Result.map successFunction</c>.</returns>
        let inline (<!>) successFunction =
            Result.map successFunction
            (*
            static member
                ( <!> ) : successFunction:('a -> 'b) * returns:Returns<'a,'c> ->
                          -> Returns<'b,'c>
            *)

        // -------------------------------------------------------------------------------------- //

        /// This is the infix operator version of "Results.apply".
        /// Applies the given function, already wrapped inside a Success or Failure results (return) container,
        /// to the Success Value of the given input <c>Returns</c> (result) container,
        /// if both the containers are in a Succeeded status,
        /// otherwise the existing error of either the two (input and function) containers is propagated.
        /// <param name="wrappedSuccessFunction">The function already wrapped in a <c>Result</c> container.</param>
        /// <returns>A function <c>Result&lt;'a,'c&gt; -&gt; Result&lt;'b,'c&gt;</c> equivalent to <c>Result.apply wrappedSuccessFunction</c>.</returns>
        let inline (<*>) wrappedSuccessFunction  =
            apply wrappedSuccessFunction
            (*
            static member
                ( <*> ) : wrappedSuccessFunction:Returns<('a -> 'b),'c> * returns:Returns<'a,'c>
                          -> Returns<'b,'c>
            *)

        // -------------------------------------------------------------------------------------- //

        /// Compose two given (SWITCH) functions in parallel with an AND logic
        /// (bind switchFunction1 + bind switchFunction2).
        ///
        /// The behavior of the composition shall be specified by two other additional functions:
        /// By the "addSuccess" function in case of Success of "successFunction1 returns" and "successFunction2 returns".
        /// By the "addFailure" function in case of Failure of "successFunction1 returns" and "successFunction2 returns".
        ///
        /// The given (SHWITCH) functions must return a <c>Returns</c> (result) container.
        ///
        /// Note: useful for composing <c>Returns</c> (result) container in parallel.
        /// Note: this composition may accumulate errors, if this behavior is implemented in the "addFailure" function.
        /// Note: the infix operator version is: "&&&".
        /// Note: this is the infix operator version of "Results.plus".
        /// Currently commented out: Result has no "plus" combinator defined in this module (unlike Returns.plus),
        /// so the parallel "&&&" operator is not available here.
        //let inline (&&&) switchFunction1 switchFunction2 =
        //    let addSuccess s1 _ = s1
        //    let addFailure errs1 errs2 = errs1 @ errs2  // concatenate error messages.
        //    plus addSuccess addFailure switchFunction1 switchFunction2
        //    (*
        //    static member
        //        ( &&& ) : switchFunction1:('a -> Returns<'b,'c>) * switchFunction2:('a -> Returns<'d,'c>)
        //                  -> ('a -> Returns<'b,'c>)
        //    *)

// -------------------------------------------------------------------------------------- //
// -------------------------------------------------------------------------------------- //

/// <summary>
/// Computation-expression builder type that enables the <c>result { ... }</c> syntax, giving Railway-Oriented
/// error handling (<c>let!</c>/<c>do!</c> that short-circuit on Error) over the standard <c>Result&lt;'T,'Error&gt;</c> type.
/// </summary>
[<Sealed>]
type ResultlBuilder() =

    /// <summary>Produces the "no result" value for an empty CE block: <c>Ok ()</c>.</summary>
    /// <returns>An Ok of <c>()</c>.</returns>
    member __.Zero() = Result.Ok()

    /// <summary>Identity hook the F# compiler uses to normalize already-<c>Result</c>-typed expressions used in <c>let!</c>.</summary>
    /// <param name="result">The <c>Result</c> value to pass through unchanged.</param>
    /// <returns><paramref name="result"/>, unchanged.</returns>
    member inline _.Source(result: Result<_, _>) : Result<_, _> = result

    /// <summary>Implements <c>let! x = m in ...</c>: sequential (short-circuiting) binding.</summary>
    /// <param name="m">The <c>Result</c> value being bound.</param>
    /// <param name="f">The continuation to run with the unwrapped Ok value.</param>
    /// <returns>The result of <c>Result.bind f m</c>.</returns>
    member inline __.Bind(m, f) = Result.bind f m

    /// <summary>Implements <c>let! x = m in return f x</c> as a single step (used by the compiler as an optimization of Bind+Return).</summary>
    /// <param name="x">The <c>Result</c> value being bound.</param>
    /// <param name="f">The plain (non-Result-returning) function to map the unwrapped Ok value with.</param>
    /// <returns>The result of <c>Result.map f x</c>.</returns>
    member __.BindReturn(x: Result<'T, 'U>, f) = Result.map f x

    /// <summary>Implements <c>return x</c>: wraps a plain value in an Ok.</summary>
    /// <param name="x">The value to wrap.</param>
    /// <returns><c>Result.Ok x</c>.</returns>
    member __.Return(x) = Result.Ok x

    /// <summary>Implements <c>return! m</c>: returns an already-<c>Result</c>-typed value as-is.</summary>
    /// <param name="x">The <c>Result</c> value to return directly.</param>
    /// <returns><paramref name="x"/>, unchanged.</returns>
    member inline __.ReturnFrom(x) = x

    /// <summary>Implements sequencing of two statements in the CE body (e.g. <c>do! a; b</c>): runs <paramref name="a"/>, and if it succeeds, continues with <paramref name="b"/>.</summary>
    /// <param name="a">The first (unit-typed) computation to run.</param>
    /// <param name="b">The continuation to run if <paramref name="a"/> succeeded.</param>
    /// <returns>The result of <c>Result.bind b a</c>.</returns>
    member __.Combine (a, b) = Result.bind b a

    /// <summary>Delays evaluation of a CE block's body until it is actually run, as required by the CE machinery.</summary>
    /// <param name="f">The thunk wrapping the delayed computation.</param>
    /// <returns><paramref name="f"/>, unchanged.</returns>
    member __.Delay f = f

    /// <summary>Forces evaluation of a delayed computation produced by <see cref="Delay"/>.</summary>
    /// <param name="f">The thunk to invoke.</param>
    /// <returns>The result of invoking <paramref name="f"/>.</returns>
    member inline __.Run f = f ()

    /// <summary>Implements <c>try ... with ...</c> inside the CE.</summary>
    /// <param name="body">The protected computation.</param>
    /// <param name="handler">The exception handler, invoked if <paramref name="body"/> throws.</param>
    /// <returns>The result of <paramref name="body"/>, or of <paramref name="handler"/> if an exception was thrown and caught.</returns>
    member __.TryWith (body, handler) =
        try
            body()
        with
        | e -> handler e

    /// <summary>Implements <c>try ... finally ...</c> inside the CE.</summary>
    /// <param name="body">The protected computation.</param>
    /// <param name="compensation">The cleanup action, always run whether or not <paramref name="body"/> threw.</param>
    /// <returns>The result of <paramref name="body"/>.</returns>
    member __.TryFinally (body, compensation) =
        try
            body()
        finally
            compensation()

    /// <summary>Implements <c>use d = ... in body d</c>: ensures <paramref name="d"/> is disposed after <paramref name="body"/> completes, mirroring the standard <c>IDisposable</c> "use" pattern.</summary>
    /// <param name="d">The disposable resource (possibly null, which is tolerated).</param>
    /// <param name="body">The computation that uses the resource.</param>
    /// <returns>The result of running <paramref name="body"/> with <paramref name="d"/>, disposing it afterwards.</returns>
    member x.Using(d:#System.IDisposable, body) =
        let returns = fun () -> body d
        x.TryFinally (returns, fun () ->
            match d with
            | null -> ()
            | d -> d.Dispose())

    /// <summary>Implements <c>while guard do body</c> inside the CE, short-circuiting (keeping the first Error) as soon as any iteration fails.</summary>
    /// <param name="guard">The loop condition, re-checked before each iteration.</param>
    /// <param name="body">The loop body; must produce a <c>Result&lt;unit,'Error&gt;</c> each iteration.</param>
    /// <returns><c>Ok ()</c> if the loop ran to completion; otherwise the first Error encountered.</returns>
    member _.While (guard, body) =
        // Iterative (not recursive): a recursive formulation via Result.bind would grow the call
        // stack by one frame per loop iteration and overflow on long-running `while` loops.
        let mutable result = Ok ()
        // Once `cont` flips to false (an Error occurred), the loop condition below stops iterating.
        let mutable cont = true
        while cont && guard () do
            match body () with
            | Ok ()   -> ()
            | Error e -> result <- Error e; cont <- false
        result

    /// <summary>Implements <c>for x in s do body</c> inside the CE by translating it into a <see cref="While"/> loop driven by the sequence's enumerator (itself disposed via <see cref="Using"/>).</summary>
    /// <param name="s">The sequence to iterate over.</param>
    /// <param name="body">The loop body, invoked once per element.</param>
    /// <returns>The result of the equivalent <see cref="While"/> loop over the sequence's enumerator.</returns>
    member x.For(s:seq<_>, body) =
        x.Using(s.GetEnumerator(), fun enum ->
            x.While(enum.MoveNext,
                x.Delay(fun () -> body enum.Current)))

/// <summary>
/// Module hosting the single, shared <see cref="ResultlBuilder"/> instance used to enter the <c>result { ... }</c> computation expression.
/// </summary>
[<AutoOpen>]
module ResultlBuilder =
    /// Wraps computations in an error handling computation expression.
    let result = ResultlBuilder()

// ****************************************************************************************************** //
// ****************************************************************************************************** //

/// <summary>
/// Small collection of "smart-constructor"-style checks: each function validates a condition and returns a
/// <c>Result</c>, making it easy to compose validation steps with <c>Result.bind</c> / the <c>result { }</c> CE.
/// </summary>
module Check =

    /// <summary>Converts a nullable value into a Result, using the given error if null.</summary>
    /// <param name="error">The error to return when <paramref name="value"/> is null.</param>
    /// <param name="value">The value to check.</param>
    /// <returns>An Ok of the (now known non-null) value, or <paramref name="error"/>.</returns>
    let isNotNull error value =
        match value with
        | null -> Error error
        | nonnull -> Ok nonnull

    /// <summary>Returns the specified error if the value is false.</summary>
    /// <param name="error">The error to return when <paramref name="value"/> is false.</param>
    /// <param name="value">The boolean condition to check.</param>
    /// <returns><c>Ok ()</c> if <paramref name="value"/> is true; otherwise <paramref name="error"/>.</returns>
    let isTrue error value =
        if value then Ok() else Error error

    /// <summary>Returns the specified error if the value is true.</summary>
    /// <param name="error">The error to return when <paramref name="value"/> is true.</param>
    /// <param name="value">The boolean condition to check.</param>
    /// <returns><c>Ok ()</c> if <paramref name="value"/> is false; otherwise <paramref name="error"/>.</returns>
    let isFalse error value =
        if not value then Ok() else Error error

    /// <summary>Converts an Option to a Result, using the given error if None.</summary>
    /// <param name="error">The error to return when <paramref name="option"/> is <c>None</c>.</param>
    /// <param name="option">The option to check.</param>
    /// <returns>An Ok of the unwrapped value when <c>Some</c>; otherwise <paramref name="error"/>.</returns>
    let isSome error option =
        match option with
        | Some value -> Ok value
        | None -> Error error

    /// <summary>Converts an Option to a Result, using the given error if Some.</summary>
    /// <param name="error">The error to return when <paramref name="option"/> is <c>Some</c>.</param>
    /// <param name="option">The option to check.</param>
    /// <returns><c>Ok ()</c> when <paramref name="option"/> is <c>None</c>; otherwise <paramref name="error"/> (the <c>Some</c> value is discarded).</returns>
    let isNone error option =
        match option with
        | Some _ -> Error error
        | None -> Ok()

    /// <summary>Returns Ok if the sequence is empty, or the specified error if not.</summary>
    /// <param name="error">The error to return when <paramref name="values"/> is non-empty.</param>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> if the sequence is empty; otherwise <paramref name="error"/>.</returns>
    let isEmpty error values =
        // Seq.isEmpty short-circuits on the first element instead of fully enumerating, which matters for
        // large or lazily-computed sequences.
        if Seq.isEmpty values then
            Ok()
        else
            Error error

    /// <summary>Returns the specified error if the sequence is empty, or Ok if not.</summary>
    /// <param name="error">The error to return when <paramref name="values"/> is empty.</param>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> if the sequence is non-empty; otherwise <paramref name="error"/>.</returns>
    let isNotEmpty error values =
        if Seq.isEmpty values then
            Error error
        else
            Ok()

    /// <summary>
    /// Returns the first item of the sequence if it exists, or the specified
    /// error if the sequence is empty.
    /// </summary>
    /// <param name="error">The error to return when <paramref name="values"/> is empty.</param>
    /// <param name="values">The sequence to read the head of.</param>
    /// <returns>An Ok of the first element, or <paramref name="error"/> if the sequence is empty.</returns>
    let hasHead error values =
        match Seq.tryHead values with
        | Some value -> Ok value
        | None -> Error error

    /// <summary>
    /// Returns Ok if the two values are equal, or the specified error if not.
    /// Same as requireEqualTo, but with a signature that fits normal function
    /// application better than piping.
    /// </summary>
    /// <param name="other">The value to compare against.</param>
    /// <param name="error">The error to return when the values differ.</param>
    /// <param name="this">The value being checked.</param>
    /// <returns><c>Ok ()</c> if <paramref name="this"/> equals <paramref name="other"/>; otherwise <paramref name="error"/>.</returns>
    let areEqualTo other error this =
        if this = other then Ok() else Error error

    /// <summary>String-specific checks.</summary>
    module string =

        /// <summary>Converts a nullable value into a Result, using the given error if null.</summary>
        /// <param name="minLength">The inclusive minimum allowed length.</param>
        /// <param name="maxLength">The inclusive maximum allowed length.</param>
        /// <param name="error">The error to return when the length is out of range.</param>
        /// <param name="value">The string to check.</param>
        /// <returns>An Ok of the string if its length is within range; otherwise <paramref name="error"/>.</returns>
        let hasLengthWithin (minLength, maxLength) error (value:string) =
            match value.Length with
            | l when l < minLength || l > maxLength -> Error error
            | _ -> Ok value

    /// <summary>Numeric (<c>double</c>) range/comparison checks.</summary>
    module double =

        /// <summary>Check if the given value fall inside a rage defined by a minimum and a maximum value.</summary>
        /// <param name="min">The inclusive lower bound.</param>
        /// <param name="max">The inclusive upper bound.</param>
        /// <param name="error">The error to return when the value is out of range.</param>
        /// <param name="value">The value to check.</param>
        /// <returns>An Ok of the value if within [<paramref name="min"/>, <paramref name="max"/>]; otherwise <paramref name="error"/>.</returns>
        let isWithin (min, max) error (value: double) =
            match value with
            | l when l < min || l > max -> Error error
            | _ -> Ok value

        /// <summary>Check if the given value is less than a reference value.</summary>
        /// <param name="reference">The exclusive upper bound.</param>
        /// <param name="error">The error to return when the value is not less than <paramref name="reference"/>.</param>
        /// <param name="value">The value to check.</param>
        /// <returns>An Ok of the value if it is strictly less than <paramref name="reference"/>; otherwise <paramref name="error"/>.</returns>
        let isLessThan (reference) error (value: double) =
            match value with
            | l when l < reference -> Ok value
            | _ -> Error error

        /// <summary>Check if the given value is less than or equal to a reference value.</summary>
        /// <param name="reference">The inclusive upper bound.</param>
        /// <param name="error">The error to return when the value exceeds <paramref name="reference"/>.</param>
        /// <param name="value">The value to check.</param>
        /// <returns>An Ok of the value if it is less than or equal to <paramref name="reference"/>; otherwise <paramref name="error"/>.</returns>
        let isLessThanOrEqualTo (reference) error (value: double) =
            match value with
            | l when l <= reference -> Ok value
            | _ -> Error error

        /// <summary>Check if the given value is greater than a reference value.</summary>
        /// <param name="reference">The exclusive lower bound.</param>
        /// <param name="error">The error to return when the value is not greater than <paramref name="reference"/>.</param>
        /// <param name="value">The value to check.</param>
        /// <returns>An Ok of the value if it is strictly greater than <paramref name="reference"/>; otherwise <paramref name="error"/>.</returns>
        let isGreaterThan (reference) error (value: double) =
            match value with
            | l when l > reference -> Ok value
            | _ -> Error error

        /// <summary>Check if the given value is greater than or equal to a reference value.</summary>
        /// <param name="reference">The inclusive lower bound.</param>
        /// <param name="error">The error to return when the value is below <paramref name="reference"/>.</param>
        /// <param name="value">The value to check.</param>
        /// <returns>An Ok of the value if it is greater than or equal to <paramref name="reference"/>; otherwise <paramref name="error"/>.</returns>
        let isGreaterThanOrEqualTo (reference) error (value: double) =
            match value with
            | l when l >= reference -> Ok value
            | _ -> Error error
