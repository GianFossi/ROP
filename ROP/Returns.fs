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

// ****************************************************************************************************** //
// ****************************************************************************************************** //

// Open System name space.
open System

// ****************************************************************************************************** //
// ****************************************************************************************************** //

/// <summary>
/// <para>A <c>Returns</c> is a container (similar to 'Result' container) with a status that may be:</para>
/// <para> * a 'Success' containing a VALID VALUE of type 'TSuccess along with a list of WARNING MESSAGEs of type 'TMessage.</para>
/// <para> * a 'Failure' containing a list of ERROR MESSAGEs of type 'TMessage.</para>
/// <para>The <c>Returns</c> (result) container returns only the first error.</para>
/// </summary>
/// <typeparam name="TSuccess">The type of the value carried by the Success case.</typeparam>
/// <typeparam name="TMessage">The type shared by both warning messages (Success) and error messages (Failure).</typeparam>
type Returns<'TSuccess, 'TMessage> =

    /// <summary>
    /// Represents the <c>Returns</c> (result) container in a successful status/computation.
    /// Carries the computed value together with any non-fatal warning messages accumulated along the way.
    /// </summary>
    | Success of 'TSuccess * 'TMessage list

    /// <summary>
    /// Represents the <c>Returns</c> (result) container in a failed status/computation.
    /// Carries every accumulated error message (there is always at least one).
    /// </summary>
    | Failure of 'TMessage list

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Convert this <c>Returns</c> (result) container into a human-readable string, primarily intended for
    /// debugging/logging output.
    /// </summary>
    /// <returns>
    /// "Success: &lt;value&gt; - &lt;warnings&gt;" for a Success, or "Failure: &lt;errors&gt;" for a Failure.
    /// </returns>
    override this.ToString() =
        // Renders a message list as a single "; "-separated string, calling each message's own ToString().
        let printMsgs msgs =
            msgs |> List.map (fun x -> x.ToString()) |> String.concat "; "
        match this with
        // %A performs structural (reflection-based) formatting of the value, which is more informative than
        // %O for arbitrary record/tuple/list success values (at the cost of some reflection overhead) -
        // acceptable here since ToString() is a diagnostics/logging path, not a hot path.
        | Success(value, msgs) -> sprintf "Success: %A - %s" value (printMsgs msgs)
        | Failure(msgs)        -> sprintf "Failure: %s" (printMsgs msgs)

// ****************************************************************************************************** //
// ****************************************************************************************************** //

/// <summary>
/// Basic operations on <c>Returns</c> (result).
/// </summary>
[<RequireQualifiedAccess>]
module Returns =

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Wraps a value in a Success of a <c>Returns</c> (result) type (without any warning messages).
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>A Success carrying <paramref name="x"/> and an empty warning list.</returns>
    let ok<'TSuccess,'TMessage> (x:'TSuccess) : Returns<'TSuccess,'TMessage> =
        Success(x, [])

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Wraps a value in a Success of a <c>Returns</c> (result) type and adds a single warning message.
    /// </summary>
    /// <param name="msg">The Warning Message associated to the Success value.</param>
    /// <param name="x">The Success value.</param>
    /// <returns>A Success carrying <paramref name="x"/> and a single-element warning list containing <paramref name="msg"/>.</returns>
    let warn<'TSuccess,'TMessage> (msg:'TMessage) (x:'TSuccess) : Returns<'TSuccess,'TMessage> =
        Success(x,[msg])

    /// <summary>
    /// Wraps a value in a Success of a <c>Returns</c> (result) type and adds a single warning message.
    /// </summary>
    /// <param name="msgs">The list of Warning Message associated to the Success value.</param>
    /// <param name="x">The Success value.</param>
    /// <returns>A Success carrying <paramref name="x"/> and the given sequence of warnings materialized as a list.</returns>
    let warnmany<'TSuccess,'TMessage> (msgs:'TMessage seq) (x:'TSuccess) : Returns<'TSuccess,'TMessage> =
        // Seq.toList forces evaluation once here, so the warnings are captured immediately rather than
        // re-enumerating a lazy sequence on every future read.
        Success(x,msgs |> Seq.toList )

    /// <summary>
    /// Appends a single warning to the current Success value if the predicate holds; passes through unchanged otherwise.
    /// Failures are always propagated unchanged.
    /// </summary>
    /// <param name="predicate">Condition evaluated against the current Success value.</param>
    /// <param name="message">Warning message to append when the predicate is true.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The input with <paramref name="message"/> appended to its warnings when the predicate matches; otherwise the input unchanged.</returns>
    let warnIf (predicate: 'TSuccess -> bool) (message: 'TMessage) (returns: Returns<'TSuccess,'TMessage>) : Returns<'TSuccess,'TMessage> =
        match returns with
        // Only a Success whose value satisfies the predicate gets the new warning appended; every other
        // case (Success failing the predicate, or any Failure) falls through to the wildcard, unchanged.
        | Success (value, msgs) when predicate value -> Success (value, msgs @ [message])
        | _ -> returns

    /// <summary>
    /// Lazy variant of <see cref="warnIf"/>: appends a single warning to the current Success value if the predicate
    /// holds, building the warning message only in that case. Failures are always propagated unchanged.
    /// </summary>
    /// <remarks>
    /// Avoids the eager-argument trap of <c>warnIf</c>: F# evaluates every argument before the call, so
    /// <c>warnIf p (Msg $"Re = {re}")</c> formats the string even when <c>p</c> is false (the common case) and then
    /// throws it away. Here the message is a thunk, invoked at most once and only when the predicate holds.
    /// The function is <c>inline</c> so that a lambda passed at the call site is inlined too, and no closure is
    /// allocated for it either. Keep using <c>warnIf</c> when the message is already a constructed value.
    /// </remarks>
    /// <param name="predicate">Condition evaluated against the current Success value.</param>
    /// <param name="message">Thunk building the warning message; invoked only when the predicate is true.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The input with the built message appended to its warnings when the predicate matches; otherwise the input unchanged.</returns>
    let inline warnIfLazy (predicate: 'TSuccess -> bool) (message: unit -> 'TMessage) (returns: Returns<'TSuccess,'TMessage>) : Returns<'TSuccess,'TMessage> =
        match returns with
        // Same shape as warnIf; the only difference is that the message is built inside the matching branch.
        | Success (value, msgs) when predicate value -> Success (value, msgs @ [message ()])
        | _ -> returns

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Wraps a single message in a Failure of a <c>Returns</c> (result) type.
    /// </summary>
    /// <param name="msg">The Failure Message associated to the Failure status.</param>
    /// <returns>A Failure carrying a single-element error list containing <paramref name="msg"/>.</returns>
    let fail<'TSuccess,'TMessage> (msg:'TMessage) : Returns<'TSuccess,'TMessage> =
        Failure([ msg ])

    /// <summary>
    /// Wraps a list of message in a Failure of a <c>Returns</c> (result) type.
    /// </summary>
    /// <param name="msgs">The list of Failure Messages associated to the Failure status.</param>
    /// <returns>A Failure carrying the given sequence of errors materialized as a list.</returns>
    let failmany<'TSuccess,'TMessage> (msgs:'TMessage seq) : Returns<'TSuccess,'TMessage> =
        Failure( msgs |> Seq.toList )

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Change the Success status of a <c>Returns</c> (result) type into a Failure status,
    /// if it contain any warning messages, transferring all the Warning Messages into the Failure container.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>
    /// A Failure carrying the former warnings when the input was a Success with at least one warning;
    /// otherwise the input unchanged (clean Success or already-a-Failure).
    /// </returns>
    let failOnWarnings (returns : Returns<'TSuccess,'TMessage>) : Returns<'TSuccess,'TMessage> =
      match returns with
      // Only escalate when there is at least one warning to escalate; a clean Success (no warnings) is left as-is.
      | Success (_, msgs) when msgs <> [] -> Failure msgs
      | _ -> returns

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Returns</c> (result) container and extract its Value in case of Success.
    /// Otherwise, returns the given (DEFAULT) value in case of Failure.
    /// </summary>
    /// <param name="value">The default value.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The Success value, or <paramref name="value"/> when the input is a Failure.</returns>
    let defaultValue (value: 'TSuccess) (returns: Returns<'TSuccess,'TMessage>) : 'TSuccess =
        match returns with
        | Success (x,_) -> x
        | _ -> value

    /// Takes a <c>Returns</c> (result) container and extract its Value in case of Success.
    /// Otherwise, determine which (default) Value has to be to returned, in base to the generated list of Errors, using a given function as selector.
    /// <param name="compensation">The function that work as a selector to return the prover default value.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The Success value, or the result of applying <paramref name="compensation"/> to the accumulated errors when the input is a Failure.</returns>
    let defaultWith (compensation: 'TMessage list->'TSuccess) (returns: Returns<'TSuccess,'TMessage>) : 'TSuccess  =
        match returns with
        | Success (x,_) -> x
        | Failure errors -> compensation errors

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Returns</c> (result) container and extract its Value in case of Success.
    /// Otherwise, raise/throw an exception with the list of Failure Messages associated to the Failure status.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The Success value.</returns>
    /// <exception cref="System.Exception">Thrown (via <c>failwith</c>) when the input is a Failure, with all error messages joined into the exception message.</exception>
    let valueOrFailwith (returns: Returns<'TSuccess,'TMessage>) : 'TSuccess =
        // Renders each error message via %O (calls its ToString()) and joins them for a single exception message.
        let concatenateMessages msgs =
            msgs
            |> Seq.map (sprintf "%O")
            |> String.concat ("; ")
        match returns with
        | Success(s, _) -> s
        | Failure(errs) -> errs |> concatenateMessages |> failwith

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Creates a safe version of the supplied function,
    /// applying the given function to the specified input Value and
    /// catch its output as a <c>Returns</c> (result) container,
    /// instead of throwing any internally raised exception/s.
    /// </summary>
    /// <param name="givenFunction">The function to be applied to the input value (that may raise an exception!).</param>
    /// <param name="value">The input value.</param>
    /// <returns>A Success of the function's result, or a Failure carrying the caught exception.</returns>
    let tryCatch (givenFunction: 'TSuccess1 -> 'TSuccess2) (value:'TSuccess1) : Returns<'TSuccess2,exn> =
        try
            Success (givenFunction value ,[] )
        with
        // OutOfMemoryException is deliberately NOT caught: swallowing it would let the process limp along in
        // an unreliable state instead of terminating, which is generally worse than letting it propagate.
        | ex when not (ex :? OutOfMemoryException) -> Failure [ex]

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Returns</c> (result) container and transfor it into an Active Pattern result (|Pass|Warn|Fail|).
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>
    /// <c>Pass value</c> for a warning-free Success, <c>Warn (value, msgs)</c> for a Success carrying warnings,
    /// or <c>Fail msgs</c> for a Failure.
    /// </returns>
    let (|Pass|Warn|Fail|) (returns : Returns<'TSuccess,'TMessage>) =
      match returns with
      // A Success with an empty warning list is distinguished from one that carries warnings, so callers
      // can pattern-match on "clean success" vs "success with caveats" vs "failure" in one expression.
      | Success  (value, []  ) -> Pass  value
      | Success  (value, msgs) -> Warn (value,msgs)
      | Failure  msgs -> Fail msgs

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Returns</c> (result) container and check if it is a Success.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>Returns True if the input <c>Returns</c> (result) container is a Success, otherwise False if it is a Failure.</returns>
    let isSucceeded (returns : Returns<'TSuccess,'TMessage>) =
        match returns with
        | Success _ -> true
        | _ -> false

    /// <summary>
    /// Takes a <c>Returns</c> (result) container and check if it is a Failure.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result)) type.</param>
    /// <returns>Returns True if the input <c>Returns</c> (result) container is a Failure, otherwise False if it is a Success.</returns>
    let isFailure (returns : Returns<'TSuccess,'TMessage>) =
        match returns with
        | Failure _ -> true
        | _ -> false

    /// <summary>
    /// Takes a <c>Returns</c> (result) container and check if it has some Warning Messages.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>Returns True if the input <c>Returns</c> (result) container has some messages, otherwise False.</returns>
    let hasWarnings (returns : Returns<'TSuccess,'TMessage>) =
        match returns with
        // List.isEmpty is O(1) (checks the union case) unlike List.Length, which would walk the whole list
        // just to compare it against zero.
        | Success (_, msgs) -> not (List.isEmpty msgs)
        | _ -> false

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Returns</c> (result) container and transform it into an Option container.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns><c>Some (value, warnings)</c> for a Success; <c>None</c> for a Failure (the error messages are discarded).</returns>
    let toOption (returns : Returns<'TSuccess,'TMessage>)  =
        match returns with
        | Success (value,warns) -> Some (value,warns)
        | Failure (_) -> Option.None

    /// <summary>
    /// Takes a Option and transform it into an <c>Returns</c> (result) container.
    /// If Some then its Value is wrapped into a Success otherwise returns a Failure with the specified message.
    /// </summary>
    /// <param name="failureMesssage">The message to use for the Failure produced when the option is None.</param>
    /// <param name="option">The input option type.</param>
    /// <returns>A Success wrapping the option's value when <c>Some</c>; otherwise a Failure carrying <paramref name="failureMesssage"/>.</returns>
    let ofOption (failureMesssage:'TMessage) (option:option<'TSuccess>) =
        match option with
        | Some x -> ok x
        | None -> fail failureMesssage

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Converts a <c>Returns</c> (result) container into a Choice container.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns><c>Choice1Of2 (value, warnings)</c> for a Success; <c>Choice2Of2 errors</c> for a Failure.</returns>
    let toChoice (returns : Returns<'TSuccess,'TMessage>) =
        match returns with
        | Success (value,warns) -> Choice1Of2 (value,warns)
        | Failure (errors) -> Choice2Of2 errors

    /// <summary>
    /// Converts a Choice into a r<c>Returns</c> (result) container.
    /// </summary>
    /// <param name="choice">The input choice type: Choice1Of2 (value, warnings) for success, Choice2Of2 errors for failure.</param>
    /// <returns>A Success wrapping the Choice1Of2 payload, or a Failure wrapping the Choice2Of2 errors.</returns>
    let ofChoice (choice : Choice<'TSuccess * 'TMessage list, 'TMessage list>) =
        match choice with
        | Choice1Of2 (value, warns) -> Success (value, warns)
        | Choice2Of2 errors -> failmany errors

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Converts a <c>Returns</c> (result) container into a Choice.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns><c>Result.Ok (value, warnings)</c> for a Success; <c>Result.Error errors</c> for a Failure.</returns>
    let toResult (returns : Returns<'TSuccess,'TMessage>) =
        match returns with
        | Success (value,warns) -> Result.Ok (value, warns)
        | Failure (errors) -> Result.Error errors

    /// <summary>
    /// Converts a Result into a <c>Returns</c> (result) container.
    /// </summary>
    /// <param name="result">A Result whose Ok case carries a value together with its warnings.</param>
    /// <returns>A Success carrying the Ok payload, or a Failure carrying the Error payload.</returns>
    let ofResult (result: Result<'TSuccess * 'TMessage list, 'TMessage list>) =
        match result with
        | Result.Ok (value, warns) -> Success (value, warns)
        | Result.Error errors -> failmany errors

    /// <summary>
    /// Converts a plain (warning-less, single-error) <c>Result&lt;'TSuccess,'TMessage&gt;</c> into a <c>Returns</c> (result) container.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="ofResult"/>, the Ok case carries just the value (no warning list), which is the shape
    /// every non-ROP API produces. If the Error payload is already a list (<c>Result&lt;'T,'M list&gt;</c>), this
    /// would yield a <c>Returns&lt;'T,'M list&gt;</c> with a single list-valued error; use <see cref="ofResult"/>
    /// (after <c>Result.map (fun v -> v, [])</c>) for that shape instead.
    /// </remarks>
    /// <param name="result">The input plain Result.</param>
    /// <returns>A warning-free Success of the Ok value, or a single-error Failure of the Error value.</returns>
    let ofPlainResult (result: Result<'TSuccess,'TMessage>) : Returns<'TSuccess,'TMessage> =
        match result with
        | Result.Ok value -> Success (value, [])
        | Result.Error error -> Failure [ error ]

    /// <summary>
    /// Converts a <c>Returns</c> (result) container into a plain <c>Result&lt;'TSuccess,'TMessage list&gt;</c>.
    /// WARNINGS ARE DISCARDED: a Success with warnings becomes an <c>Ok</c> of the bare value.
    /// </summary>
    /// <remarks>
    /// This is a lossy boundary conversion, intended for handing a value to code that does not understand the
    /// warning channel. If the warnings matter, either use <see cref="toResult"/> (which keeps them in the Ok
    /// payload), or escalate them first with <see cref="failOnWarnings"/>, or log them with <see cref="successTee"/>.
    /// </remarks>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns><c>Ok value</c> for a Success (warnings dropped); <c>Error errors</c> for a Failure.</returns>
    let toPlainResult (returns: Returns<'TSuccess,'TMessage>) : Result<'TSuccess,'TMessage list> =
        match returns with
        | Success (value, _) -> Result.Ok value
        | Failure errors -> Result.Error errors

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **    EITHER     ***
    // ********************

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container, and:
    ///
    /// if it is a Success, maps its Success Value, along with its Warning Messages (if any), with the given "fSuccess" function;
    ///
    /// if it is a Failure, maps it Error Messages with the given "fFailure" function.
    /// </summary>
    /// <param name="fSuccess">Function to be applied to source, if it contains a Success value.</param>
    /// <param name="fFailure">Function to be applied to source, if it contains a Failure value.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The result of applying either functions.</returns>
    let inline either (fSuccess) (fFailure) (returns : Returns<'TSuccess,'TMessage>)  =
        match returns with
        // Both branches receive everything the case carries (value+warnings, or errors), so callers
        // never need to re-destructure the Returns value themselves.
        | Success(s, msgs) -> fSuccess (s, msgs)
        | Failure(errs) -> fFailure (errs)

    // -------------------------------------------------------------------------------------- //

    // ********************
    // ** JOINT MESSAGES **
    // ********************

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container, and:
    ///
    /// if it is a Success, appends the given (new) messages to the existing warning messages (if any)
    ///
    /// if it is a Failure, appends the given (new) messages to the existing error messages.
    /// </summary>
    /// <param name="messages">The given (new) messages to be appended the existing waring or error messages.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The result of merging the given (new) messages to the existing warning or error messages, in base of the current status of the input <c>Returns</c> (result) container.</returns>
    let inline jointMessages (messages : 'TMessage list) (returns : Returns<'TSuccess,'TMessage>) =
        // Note the append order: the returns' OWN messages come first, then the supplied "messages" - this
        // is what gives `bind` its documented (and test-covered) "newest step's messages first" ordering
        // when composed in a chain, since each step's own result becomes the "messages" side of the next call.
        // Matched directly rather than through `either` with local lambdas, which allocated closures on every call.
        match messages, returns with
        // Nothing to append: the result would be structurally identical to the input, so skip re-wrapping it.
        | [], _                -> returns
        | _, Success (x, msgs) -> Success(x, msgs @ messages)
        | _, Failure errs      -> Failure(errs @ messages)

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container, and:
    ///
    /// if it is a Success, appends the given (new) message to the existing warning messages (if any)
    ///
    /// if it is a Failure, appends the given (new) message to the existing error messages.
    /// </summary>
    /// <param name="message">The given (new) message to be appended the existing waring or error messages.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The result of merging the given (new) message to the existing warning or error messages, in base of the current status of the input <c>Returns</c> (result) container.</returns>
    let inline jointMessage (message : 'TMessage) (returns : Returns<'TSuccess,'TMessage>) =
        jointMessages [message] returns

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **     BIND      ***
    // ********************

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container, and:
    ///
    /// if it is a Success, applies the given SWITCH function to the Success Value of the input. Any new warning messages are concatenated to the existing warning messages.
    ///
    /// if it is a Failure, the existing error messages are propagated.
    /// </summary>
    /// <param name="switchFunction">The given SWITH function to be applied to the Success Value of the input (must return a <c>Returns</c> (result) container).</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// Note: useful for composing <c>Returns</c> (result) container sequentially.
    /// Note: this bind does not accumulate errors.
    /// Note: the infix operator version is: ">>=".
    /// <returns>The Failure unchanged if the input failed; otherwise the result of <paramref name="switchFunction"/> with the input's warnings folded in.</returns>
    let inline bind (switchFunction : 'TSuccess1 -> Returns<'TSuccess2,'TMessage> ) (returns : Returns<'TSuccess1,'TMessage>) =
        // On Success, run the next step and fold this step's warnings into its result (see jointMessages
        // for the resulting message ordering); on Failure, short-circuit and propagate the errors as-is.
        // Matched directly rather than through `either` with local lambdas, which allocated closures on every call
        // (bind sits on the hottest path of any pipeline); jointMessages returns the step's result as-is when there
        // are no warnings to fold in.
        match returns with
        | Success (s, msgs) -> switchFunction s |> jointMessages msgs
        | Failure msgs      -> Failure msgs

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **     APPLY     ***
    // ********************

    /// <summary>
    /// Takes a given function ( 'TSuccess1 -> 'TSuccess2 ) [already wrapped inside a Success or Failure <c>Returns</c> (result) container] and an input <c>Returns</c> (result) container, then:
    ///
    /// if both the <c>Returns</c> (result) containers are a Success, maps the given Success Value, with the given "fSuccess" function. Any new warning messages are concatenated with the existing warning messages;
    ///
    /// if both the <c>Returns</c> (result) containers are a Failure, their error messages are concatenated;
    ///
    /// otherwise the existing error messages of one of the two function and input <c>Returns</c> (result) container is propagated.
    /// </summary>
    /// <param name="wrappedSuccessFunction">The given function, wrapped inside a <c>Returns</c> (result) container, to be applied to the input Success Value.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The result of applying either functions.</returns>
    let inline apply (wrappedSuccessFunction : Returns<'TSuccess1->'TSuccess2,'TMessage>) (returns : Returns<'TSuccess1,'TMessage>) =
        match wrappedSuccessFunction, returns with
        // Applicative semantics: unlike bind, BOTH sides are always evaluated up-front (neither one
        // short-circuits the other), so two Failures concatenate their errors rather than only the first
        // one surfacing.
        | Success(f, msgs1), Success(x, msgs2) -> Success(f x, msgs1 @ msgs2)
        | Failure errs, Success(_, _) -> Failure(errs)
        | Success(_, _), Failure errs -> Failure(errs)
        | Failure errs1, Failure errs2 -> Failure(errs1 @ errs2)

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **  LIFT or MAP  ***
    // ********************

    /// <summary>
    /// Takes a given function ( 'TSuccess1 -> 'TSuccess2 ) and an input <c>Returns</c> (result) container, then:
    ///
    /// if the input <c>Returns</c> (result) container is a Success, maps its Success Value with the given "fSuccess" function. Any of its warning messages are propagated;
    ///
    /// if the input <c>Returns</c> (result) containers is a Failure, its error messages are propagated.
    /// </summary>
    /// <param name="successFunction">The given function to be applied to the input Success Value.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>A Success of the transformed value (warnings preserved), or the original Failure.</returns>
    let inline map (successFunction : 'TSuccess1 -> 'TSuccess2) (returns : Returns<'TSuccess1,'TMessage>) =
        // map is implemented in terms of apply: lift the plain function into a warning-free Success, then
        // let apply's Success/Success case do the actual invocation.
        let wrappedSuccessFunction = ok successFunction
        apply wrappedSuccessFunction returns

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Maps the given function over all existing warning and failure messages of a <c>Returns</c> (result) container (if any).
    /// It works as a sort of Trasformation/Conversion Function of the warning and failure message type.
    /// </summary>
    /// <param name="conversionFunction">The conversion function (common for both the Warning and Error Message).</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The same case (Success/Failure) with every message transformed by <paramref name="conversionFunction"/>.</returns>
    let mapMessages (conversionFunction: 'TMessage1 -> 'TMessage2) (returns : Returns<'TSuccess,'TMessage1>) =
        match returns with
        | Success (x,msgs) ->
            // Transform every warning message, keep the success value untouched.
            let msgs' = List.map conversionFunction msgs
            Success (x, msgs')
        | Failure errors ->
            // Transform every error message.
            let errors' = List.map conversionFunction errors
            Failure errors'

    /// <summary>
    /// Transforms only the warning messages on a Success using the given function.
    /// The Success value and any Failure error messages are propagated unchanged.
    /// </summary>
    /// <param name="f">The function used to transform each warning message.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>A Success with transformed warnings, or the original Failure untouched.</returns>
    let mapWarnings (f: 'TMessage -> 'TMessage) (returns: Returns<'TSuccess,'TMessage>) : Returns<'TSuccess,'TMessage> =
        match returns with
        | Success (value, msgs) -> Success (value, msgs |> List.map f)
        | Failure _             -> returns

    /// <summary>
    /// Transforms only the error messages on a Failure using the given function.
    /// The Failure errors are remapped; Success values and warnings are propagated unchanged.
    /// </summary>
    /// <param name="f">The function used to transform each error message.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>A Failure with transformed errors, or the original Success untouched.</returns>
    let mapErrors (f: 'TMessage -> 'TMessage) (returns: Returns<'TSuccess,'TMessage>) : Returns<'TSuccess,'TMessage> =
        match returns with
        | Success _        -> returns
        | Failure errors   -> Failure (errors |> List.map f)

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **  AGGREGATION  ***
    // ********************

    /// <summary>
    /// Removes duplicate warnings from a Success, keeping the first occurrence of each and preserving the order of the
    /// warnings that remain. Failures are propagated unchanged.
    /// </summary>
    /// <remarks>
    /// Loops (marching solvers, per-element traversals) tend to raise the same warning once per iteration, which
    /// buries everything else in the report; the usual workaround, dropping warnings upstream, defeats the warning
    /// channel entirely. Use <see cref="summariseWarnings"/> instead when the number of occurrences matters.
    /// </remarks>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>A Success with duplicate warnings removed (first-occurrence order kept), or the original Failure untouched.</returns>
    let dedupeWarnings (returns: Returns<'TSuccess,'TMessage>) : Returns<'TSuccess,'TMessage> =
        match returns with
        // List.distinct keeps the first occurrence of each element and discards later ones, in list order.
        | Success (value, msgs) -> Success (value, List.distinct msgs)
        | Failure _             -> returns

    /// <summary>
    /// Collapses the warnings of a Success by a key, returning each distinct warning (the first one seen for its key)
    /// together with the number of warnings that shared that key, in first-occurrence order.
    /// Error messages of a Failure are NOT collapsed: each is carried through with a count of 1.
    /// </summary>
    /// <remarks>
    /// Keeps the information "this happened 100 times" that <see cref="dedupeWarnings"/> throws away. The key lets
    /// warnings that differ only in a payload (e.g. the node index they were raised at) be grouped together; pass
    /// <c>id</c> to group only identical warnings.
    /// </remarks>
    /// <param name="key">Projects each warning to the key it is grouped by.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>A Success whose warnings are (first warning, count) pairs, one per distinct key; or a Failure whose errors are each paired with 1.</returns>
    let summariseWarnings (key: 'TMessage -> 'Key) (returns: Returns<'TSuccess,'TMessage>) : Returns<'TSuccess,'TMessage * int> =
        match returns with
        | Success (value, msgs) ->
            // List.groupBy yields groups in first-occurrence order of their key, each group keeping list order,
            // so the head of each group is the first warning seen for that key.
            let summary = msgs |> List.groupBy key |> List.map (fun (_, group) -> List.head group, List.length group)
            Success (value, summary)
        | Failure errors -> Failure (errors |> List.map (fun e -> e, 1))

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **    CONTEXT    ***
    // ********************

    /// <summary>
    /// Annotates every error message of a Failure with a context label, using a caller-supplied annotation function,
    /// so that nested calls build a breadcrumb trail of where the failure travelled. Successes (and their warnings)
    /// are propagated unchanged.
    /// </summary>
    /// <remarks>
    /// <para>Because <c>'TMessage</c> is generic, the library cannot know how to attach a label to a message, so the
    /// caller provides that once (e.g. <c>fun ctx m -> InContext (ctx, m)</c> for a union, or
    /// <c>fun ctx m -> $"{ctx}: {m}"</c> for strings). Partially apply it to get a project-wide <c>withContext</c>:
    /// <c>let withContext label = Returns.withContextBy MyMessage.inContext label</c>.</para>
    /// <para>Applied at several levels, the innermost label is applied first, so the outermost label ends up
    /// outermost in the message. Note that a Failure produced by <c>bind</c>/<c>&gt;&gt;=</c> also carries the
    /// warnings collected before it, and those are annotated too. To annotate warnings as well, pipe through
    /// <c>Returns.mapWarnings (annotate label)</c>.</para>
    /// </remarks>
    /// <param name="annotate">Attaches the context to a single message.</param>
    /// <param name="context">The context label (typically a string naming the current step).</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The Failure with every error annotated, or the original Success untouched.</returns>
    let withContextBy (annotate: 'Context -> 'TMessage -> 'TMessage) (context: 'Context) (returns: Returns<'TSuccess,'TMessage>) : Returns<'TSuccess,'TMessage> =
        match returns with
        | Success _      -> returns
        | Failure errors -> Failure (errors |> List.map (annotate context))

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a given function ('TSuccess1 -> 'TSuccess2 -> 'TSuccess3) and an input <c>Returns</c> (result) container, then:
    ///
    /// if the two input <c>Returns</c> (result) container are a Success, maps their Success Values with the given "fSuccess" function. Any of its warning messages are propagated;
    ///
    /// if the input <c>Returns</c> (result) containers is a Failure, its error messages are propagated.
    /// </summary>
    /// <param name="successFunction">The given function to be applied to the input Success Value.</param>
    /// <param name="returns1">The first input <c>Returns</c> (result) type.</param>
    /// <param name="returns2">The second input <c>Returns</c> (result) type.</param>
    /// <returns>The combined Success (warnings from both merged), or the (accumulated, per <see cref="apply"/>) Failure.</returns>
    let inline map2 (successFunction : 'TSuccess1 -> 'TSuccess2 -> 'TSuccess3)
                    (returns1 : Returns<'TSuccess1,'TMessage>)
                    (returns2 : Returns<'TSuccess2,'TMessage>) =
        //successFunction <!> return1 <*> return2
        // Standard applicative-style lifting: wrap the curried function in a Success, then apply it to each
        // argument in turn via `apply` (which is what actually merges warnings/accumulates errors).
        ok successFunction
        |> apply <| returns1
        |> apply <| returns2
        //match returns1, returns2 with
        //| Success (a,msga), Ok (bmsgsb) -> Ok (f returns1 returns1)
        //| Error e, _ | _, Error e -> Error e

    // -------------------------------------------------------------------------------------- //

    /// Applies the given function, lifted/wrapped inside a  results (return) container in the Success status,
    /// to the Success Value of the given input <c>Returns</c> (result) container,
    /// if the input container is in a Succeeded status,
    /// otherwise the existing errors of the input container are propagated.
    ///
    /// The given function doesn't return a <c>Returns</c> (result) container.
    /// ( Synonym of "lift" <see cref="lift"/> ).
    ///
    /// The function is applied to the first returns argument, then to the second returns argument, then to the third returns argument.
    /// <param name="successFunction">The 3-argument function to lift and apply.</param>
    /// <param name="returns1">The first input <c>Returns</c> (result) type.</param>
    /// <param name="returns2">The second input <c>Returns</c> (result) type.</param>
    /// <param name="returns3">The third input <c>Returns</c> (result) type.</param>
    /// <returns>The combined Success (warnings from all three merged), or the accumulated Failure.</returns>
    let inline map3 successFunction
                    (returns1 : Returns<'TSuccess1,'TMessage>)
                    (returns2 : Returns<'TSuccess2,'TMessage>)
                    (returns3 : Returns<'TSuccess3,'TMessage>) =
        // successFunction <!> return1 <*> return2 <*> return3
        // Same applicative-lift pattern as map2, extended with a third `apply`.
        ok successFunction
        |> apply <| returns1
        |> apply <| returns2
        |> apply <| returns3

    // -------------------------------------------------------------------------------------- //

    /// Applies the given function, lifted/wrapped inside a  results (return) container in the Success status,
    /// to the Success Value of the given input <c>Returns</c> (result) container,
    /// if the input container is in a Succeeded status,
    /// otherwise the existing errors of the input container are propagated.
    ///
    /// The given function doesn't return a <c>Returns</c> (result) container.
    /// ( Synonym of "lift" <see cref="lift"/> ).
    ///
    /// The function is applied to the first returns argument, then to the second returns argument, then to the third returns argument, then to the fourth returns argument.
    /// <param name="successFunction">The 4-argument function to lift and apply.</param>
    /// <param name="returns1">The first input <c>Returns</c> (result) type.</param>
    /// <param name="returns2">The second input <c>Returns</c> (result) type.</param>
    /// <param name="returns3">The third input <c>Returns</c> (result) type.</param>
    /// <param name="returns4">The fourth input <c>Returns</c> (result) type.</param>
    /// <returns>The combined Success (warnings from all four merged), or the accumulated Failure.</returns>
    let inline map4 successFunction (returns1 : Returns<'TSuccess1,'TMessage>)
                                    (returns2 : Returns<'TSuccess2,'TMessage>)
                                    (returns3 : Returns<'TSuccess3,'TMessage>)
                                    (returns4 : Returns<'TSuccess4,'TMessage>) =
        // successFunction <!> return1 <*> return2 <*> return3
        // Same applicative-lift pattern as map2/map3, extended with a fourth `apply`.
        ok successFunction
        |> apply <| returns1
        |> apply <| returns2
        |> apply <| returns3
        |> apply <| returns4

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **    FLATTEN    ***
    // ********************

    /// <summary>
    /// Flattens two nested <c>Returns</c> (result) container.
    /// </summary>
    /// <param name="returns">The nested <c>Returns</c> (result) container.</param>
    /// <returns>A single Success of the value when it was nested with Success, or the Failure messages.</returns>
    /// <remarks><c>flatten</c> is equivalent to <c>bind id</c>.</remarks>
    let flatten (returns: Returns<Returns<'TSuccess,'TMessage>,'TMessage>) =
        // `bind id` unwraps one layer of nesting: if the outer Returns is a Success, its payload is
        // itself already a Returns, and `id` hands it straight back to `bind` to be joined in.
        returns |> bind id

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **     MERGE     ***
    // ********************

    /// <summary>
    /// Takes two input <c>Returns</c> (result) container, then:
    ///
    /// If both the <c>Returns</c> (result) containers are a Success, apply the given merging function on the Success Value of the two inputs. Their warning messages are concatenated together;
    ///
    /// If both the <c>Returns</c> (result) containers are a Failure, their error messages are concatenated together;
    ///
    /// Otherwise the existing error messages of one of the two function and input <c>Returns</c> (result) container are propagated.
    /// </summary>
    /// <param name="addSuccess">The given function that define the mergin operation on success values.</param>
    /// <param name="addFailure">The given function that define the mergin operation on erro messages.</param>
    /// <param name="returns1">The first input <c>Returns</c> (result) type.</param>
    /// <param name="returns2">The second input <c>Returns</c> (result) type.</param>
    /// <returns>The merged Success, the merged Failure, or whichever single Failure applies.</returns>
    let inline merge (addSuccess) (addFailure) (returns1 : Returns<'TSuccess1,'TMessage>) (returns2 : Returns<'TSuccess2,'TMessage>) : Returns<'TSuccess3,'TMessage> =
        match (returns1, returns2) with
        | Success (s1,msgs1), Success (s2,msgs2) -> Success ( addSuccess s1 s2, msgs1 @ msgs2 )
        | Failure errs, Success(_, _) -> Failure(errs)
        | Success(_, _), Failure errs -> Failure(errs)
        | Failure errs1, Failure errs2 -> Failure( addFailure errs1 errs2 )

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **   VALIDATE   ***
    // ********************

    ///// <summary>
    ///// Takes two input <c>Returns</c> (result) container of the SAME TYPE, and:
    /////
    ///// if both the <c>Returns</c> (result) containers are a Success, apply the given merging function on the Success Value of the two inputs. Their warning messages are concatenated together;
    /////
    ///// if both the <c>Returns</c> (result) containers are a Failure, their error messages are concatenated together;
    /////
    ///// otherwise the existing error messages of one of the two function and input <c>Returns</c> (result) container are propagated.
    ///// </summary>

    ///// Merge two <c>Returns</c> (result) container, in accordance with the following criteria:
    ///// If both the two inputs are in a success status, returns the first Success (merging any of their warning messages, if any).
    ///// If both the two inputs are in a failure status, returns a failure value, merging their error messages.
    ///// Otherwise propagate the Success status of any of the two inputs.
    //let inline validate (returns1 : Returns<'TSuccess,'TMessage>) (returns2 : Returns<'TSuccess,'TMessage>) =
    //    match (returns1, returns2) with
    //    | Success (s1,msgs1), Success (_,msgs2) -> Success ( s1, msgs1@msgs2 )
    //    | Failure _, Success (s2,msgs2)  -> Success (s2,msgs2)
    //    | Success (s1,msgs1) , Failure _ -> Success (s1,msgs1)
    //    | Failure f1, Failure f2 -> Failure (f1 @ f2)

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **     FOLD      ***
    // ********************

    /// <summary>
    /// Collects a sequence of <c>Returns</c> (result) container and accumulates their values .
    /// If the sequence contains successes only, any warning messages will be folded together and propagated as an success.
    /// If the sequence contains one or more errors, all the error messages will be folded together and propagated as an error.
    /// </summary>
    /// <param name="folder">Combines the running success state with each element's success value.</param>
    /// <param name="state">The initial (seed) <c>Returns</c> value the fold starts from.</param>
    /// <param name="returns">The sequence of <c>Returns</c> values to fold over.</param>
    /// <returns>The final accumulated Success (all warnings collected, in order), or Failure (all errors collected, in order) once any element fails.</returns>
    let inline fold (folder: 'SuccesState->'TSuccess->'SuccesState) (state: Returns<'SuccesState,'Message>) (returns : Returns<'TSuccess,'Message> seq ) =
        // Accumulate warnings/errors in reverse to avoid repeated O(n) appends (state's message list would
        // otherwise be re-copied on every element via "@"), then reverse once at the end. Mirrors traverseList.
        let revAppend xs ys = List.fold (fun acc x -> x :: acc) ys xs
        use e = returns.GetEnumerator()
        // Track progress as a Choice: Choice1Of2 while still accumulating a running Success (state, reversed
        // messages-so-far); Choice2Of2 once any element has failed (reversed errors-so-far). Working with the
        // reversed lists throughout avoids re-copying the growing accumulator on every iteration.
        let mutable acc =
            match state with
            | Success (s, msgs) -> Choice1Of2 (s, List.rev msgs)
            | Failure msgs       -> Choice2Of2 (List.rev msgs)
        while e.MoveNext() do
            acc <-
                match acc, e.Current with
                // Still succeeding, and the next element also succeeds: fold the value in, append its warnings.
                | Choice1Of2 (s, msgsRev), Success (v, msgs) -> Choice1Of2 (folder s v, revAppend msgs msgsRev)
                // Already failed: subsequent successes contribute nothing further; keep the accumulated errors as-is.
                | Choice2Of2 errsRev,      Success _         -> Choice2Of2 errsRev
                // Still succeeding, but this element fails: switch tracks to failure mode, seeded with its errors.
                | Choice1Of2 _,            Failure errs      -> Choice2Of2 (List.rev errs)
                // Already failed, and this element fails too: append its errors to the accumulated errors.
                | Choice2Of2 errsRev,      Failure errs      -> Choice2Of2 (revAppend errs errsRev)
        // Un-reverse the accumulated messages/errors exactly once, at the very end, to restore original order.
        match acc with
        | Choice1Of2 (s, msgsRev) -> Success (s, List.rev msgsRev)
        | Choice2Of2 errsRev      -> Failure (List.rev errsRev)

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **      TEE      ***
    // ********************

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container, and:
    ///
    /// if it is a Success, it executes the given Success function on the (Success) Value and on its warning messages.
    ///
    /// if it is a Failure, it executes the given Failure function on the error messages.
    ///
    /// NOTE: the input <c>Returns</c> (result) containe is propagated unchanged.
    /// </summary>
    /// <param name="fSuccess">Side-effecting function invoked with (value, warnings) on Success.</param>
    /// <param name="fFailure">Side-effecting function invoked with the errors on Failure.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The original <paramref name="returns"/>, unchanged.</returns>
    let inline eitherTee fSuccess fFailure (returns : Returns<'TSuccess,'TMessage>) =
        // Runs `f` purely for its side effect, then discards its result and hands back the original `x` -
        // this is what makes `eitherTee` (and successTee/failureTee below) pass-through/transparent.
        let tee f x = f x; x;
        tee (either fSuccess fFailure) returns

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container and executes the given Success function on the (Success) Value and on its warning messages only.
    ///
    /// NOTE: the input <c>Returns</c> (result) containe is propagated unchanged.
    /// </summary>
    /// <param name="successFunction">Side-effecting function invoked with (value, warnings) on Success; ignored on Failure.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The original <paramref name="returns"/>, unchanged.</returns>
    let successTee successFunction (returns : Returns<'TSuccess,'TMessage>) =
        eitherTee successFunction ignore returns

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container and executes the given Failure function on the Error Messages only.
    ///
    /// NOTE: the input <c>Returns</c> (result) container is propagated unchanged.
    /// </summary>
    /// <param name="failureFunction">Side-effecting function invoked with the errors on Failure; ignored on Success.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The original <paramref name="returns"/>, unchanged.</returns>
    let failureTee failureFunction (returns : Returns<'TSuccess,'TMessage>) =
        eitherTee ignore failureFunction returns

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **     LOG       ***
    // ********************

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container, and:
    ///
    /// if it is a Success, perform a log action on the Success Value, along with its Warning Messages (if any), with the given "logSuccess" function;
    ///
    /// if it is a Failure, perform a log action on the Error Messages with the given "logFailure" function.
    /// </summary>
    /// <param name="logger">The sink that receives the formatted log line (e.g. <c>Console.WriteLine</c>, a logging framework call, etc.).</param>
    /// <param name="record">Boolean value to indicate ioif teh Log action need to be performed (TRUE) or skipped (FALSE).</param>
    /// <param name="message">A description message.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    /// <returns>The original <paramref name="returns"/>, unchanged (logging is a side effect only).</returns>
    let log (logger: string -> unit) (record:bool) (message:string) (returns : Returns<'TSuccess,'TMessage>) =
        // Builds the two possible log lines up-front; only one of them actually gets invoked, based on the case.
        let successFunction (s, msgs) = logger (sprintf ">>> %s: Returns is a Success: %A (%A)" message s msgs)
        let failureFunction errs = logger (sprintf ">>> %s Returns is a Failure: %A" message errs)
        if record then
            // eitherTee both performs the logging side effect and passes the original value straight through.
            eitherTee successFunction failureFunction returns
        else
            // Logging disabled for this call: skip straight to returning the value unchanged.
            returns

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **    SPECIAL     **
    // ********************

    /// Compose two given (SWITCH) functions in series
    /// (i.e.: bind switchFunction1 >> bind switchFunction2).
    ///
    /// The given (SWITCH) functions must return a <c>Returns</c> (result) container.
    ///
    /// Note: useful for composing <c>Returns</c> (result) container sequentially.
    /// Note: this composition does not accumulate errors.
    /// Note: the infix operator version is: ">=>".
    /// <param name="switchFunction1">The first switch function to run.</param>
    /// <param name="switchFunction2">The second switch function to run, only if the first one succeeded.</param>
    /// <param name="value">The input value fed into <paramref name="switchFunction1"/>.</param>
    /// <returns>The result of running both switch functions in sequence, short-circuiting on the first failure.</returns>
    let inline compose
        ( switchFunction1: 'TSuccess1 -> Returns<'TSuccess2,'TMessage>)
        ( switchFunction2: 'TSuccess2 -> Returns<'TSuccess3,'TMessage>)
        (value : 'TSuccess1) =

        match ( switchFunction1 value ) with
        // First step succeeded: run the second step and fold the first step's warnings into its result.
        | Success (s,msgs) -> ( switchFunction2 s ) |> ( jointMessages msgs )
        // First step failed: short-circuit, the second step never runs.
        | Failure f -> Failure f
        (*

            Alternative code:
                value
                |> bind switchFunction1 >> bind switchFunction2

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
    /// <param name="addSuccess">Define the behavior of the plus (composition), in case of success of both the input sources.</param>
    /// <param name="addFailure">Define the behavior of the plus (composition), in case of failure of both the input sources.</param>
    /// <param name="switchFunction1">The first switch function to run against <paramref name="value"/>.</param>
    /// <param name="switchFunction2">The second switch function to run against <paramref name="value"/>.</param>
    /// <param name="value">The shared input value fed into both switch functions.</param>
    /// <returns>The merged Success, the merged Failure, or whichever single Failure applies.</returns>
    let inline plus addSuccess addFailure switchFunction1 switchFunction2 value =
        // Both switch functions are always invoked against the same value (neither short-circuits the other),
        // which is what makes this a "parallel" (as opposed to sequential/bind-style) composition.
        match (switchFunction1 value, switchFunction2 value) with
        | Success (s1,msgs1),Success (s2,msgs2) -> Success ( addSuccess s1 s2, msgs1@msgs2 )
        | Failure f1,Success _  -> Failure f1
        | Success _ ,Failure f2 -> Failure f2
        | Failure f1,Failure f2 -> Failure (addFailure f1 f2)

    // -------------------------------------------------------------------------------------- //

    /// Applies the given "dead-end function" to a "value" and return the input "value" ignoring the results of the given function.
    /// (">=> toSwitchFunction" is exactly the same as ">> map").
    /// <param name="successFunction">The side-effecting function to run against <paramref name="value"/>; its result is discarded.</param>
    /// <param name="value">The value to pass through unchanged.</param>
    /// <returns><paramref name="value"/>, unchanged.</returns>
    let tee (successFunction: 'TSuccess1 -> 'TSuccess2) (value: 'TSuccess1) =
        successFunction value |> ignore
        value

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Creates two lists by classifying the values depending on whether they were wrapped with Ok or Error.
    /// </summary>
    /// <param name="returns">The list of <c>Returns</c> values to partition.</param>
    /// <returns>
    /// A tuple with both resulting lists, Oks are in the first list.
    /// </returns>
    let partition (returns: list<Returns<'TSuccess, 'TMessage>>) =
        // Tail-recursive single pass, building both accumulator lists in reverse order as it goes.
        let rec loop ((acc1, acc2) as acc) = function
            | [] -> acc
            | x::xs ->
                match x with
                | Success (v,msgs) -> loop ((v,msgs)::acc1, acc2) xs
                | Failure e -> loop (acc1, e::acc2) xs
        // The input is reversed up-front so that, combined with the reverse-prepend accumulation above,
        // both output lists come out in the original input order without a second List.rev pass at the end.
        loop ([], []) (List.rev returns)

    // -------------------------------------------------------------------------------------- //

    /// Takes two results and returns a tuple of the pair
    /// <param name="x1">The first <c>Returns</c> value.</param>
    /// <param name="x2">The second <c>Returns</c> value.</param>
    /// <returns>A Success of the tupled values (warnings merged) if both succeeded; otherwise the first Failure encountered (x1 takes priority over x2).</returns>
    let zip x1 x2 =
        match x1, x2 with
        | Success (x1res,msgs1) , Success (x2res,msgs2) -> Success( (x1res, x2res), msgs1 @ msgs2 )
        | Failure f, _ -> Failure f
        | _, Failure f -> Failure f

    // -------------------------------------------------------------------------------------- //

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **   TRAVERSE    ***
    // ********************

    /// <summary>
    /// Maps each element of a list through a switch function and collects results into a single Returns.
    /// ACCUMULATES ALL FAILURES (applicative semantics, does NOT stop at the first failure): the switch function runs
    /// on every element, and if any element fails, the errors of every failing element are merged, in order.
    /// </summary>
    /// <remarks>
    /// This is the right choice for validation (every defect reported in one pass). When later elements are
    /// expensive or meaningless once one has failed, use <see cref="traverseListFailFast"/> instead.
    /// </remarks>
    /// <param name="switchFunction">The function applied to each input element, producing a <c>Returns</c> per element.</param>
    /// <param name="inputs">The list of values to map and combine.</param>
    /// <returns>A Success of the list of mapped values (all warnings merged, in order) if every element succeeded; otherwise a Failure with every accumulated error, in order.</returns>
    let traverseList (switchFunction: 'TSuccess1 -> Returns<'TSuccess2,'TMessage>) (inputs: 'TSuccess1 list) : Returns<'TSuccess2 list,'TMessage> =
        // Accumulate warnings/errors in reverse to avoid repeated O(n) appends, then reverse once at the end.
        let revAppend xs ys = List.fold (fun acc x -> x :: acc) ys xs
        let folder state current =
            match state, switchFunction current with
            // Still succeeding, and this element also succeeds: prepend its value, append its warnings (in reverse-accumulator form).
            | Success (acc, msgsRev), Success (v, msgs) -> Success (v :: acc, revAppend msgs msgsRev)
            // Already failed: subsequent successes contribute nothing further to the accumulated errors.
            | Failure errsRev,        Success _         -> Failure errsRev
            // Still succeeding, but this element fails: switch to failure mode, seeded with its (reversed) errors.
            | Success _,              Failure errs      -> Failure (List.rev errs)
            // Already failed, and this element fails too: append its errors to the accumulated errors.
            | Failure errsRev,        Failure errs      -> Failure (revAppend errs errsRev)

        match List.fold folder (Success ([], [])) inputs with
        // Un-reverse both the collected values and the collected messages/errors exactly once, at the end.
        | Success (acc, msgsRev) -> Success (List.rev acc, List.rev msgsRev)
        | Failure errsRev        -> Failure (List.rev errsRev)

    /// <summary>
    /// Converts a list of Returns into a Returns of a list, accumulating all errors if any element is a Failure.
    /// Equivalent to traverseList id.
    /// </summary>
    /// <param name="returns">The list of <c>Returns</c> values to sequence.</param>
    /// <returns>A Success of the list of values (warnings merged) if every element succeeded; otherwise a Failure with every accumulated error.</returns>
    let sequenceList (returns: Returns<'TSuccess,'TMessage> list) : Returns<'TSuccess list,'TMessage> =
        // No per-element transformation needed - traversing with `id` is exactly "sequence".
        traverseList id returns

    /// <summary>
    /// Maps each element of a list through a switch function and collects results into a single Returns,
    /// STOPPING AT THE FIRST FAILURE (sequential semantics, like chaining with <c>&gt;&gt;=</c>): the switch function
    /// is not invoked on any element after the first failing one.
    /// </summary>
    /// <remarks>
    /// The sequential counterpart of <see cref="traverseList"/>, for computations where continuing past a failure is
    /// wasted work. Do not use it for validation: callers would see one defect at a time. As with a <c>for</c> loop
    /// inside <c>returns { }</c>, the warnings collected before the failure are kept: the Failure carries them (in
    /// element order) followed by the errors of the failing element.
    /// </remarks>
    /// <param name="switchFunction">The function applied to each input element, producing a <c>Returns</c> per element.</param>
    /// <param name="inputs">The list of values to map and combine.</param>
    /// <returns>A Success of the list of mapped values (all warnings merged, in order) if every element succeeded; otherwise a Failure with the earlier warnings followed by the first failing element's errors.</returns>
    let traverseListFailFast (switchFunction: 'TSuccess1 -> Returns<'TSuccess2,'TMessage>) (inputs: 'TSuccess1 list) : Returns<'TSuccess2 list,'TMessage> =
        // Accumulate values and warnings in reverse (cheap prepend), then reverse once at the end.
        let revAppend xs ys = List.fold (fun acc x -> x :: acc) ys xs
        let rec loop valuesRev msgsRev remaining =
            match remaining with
            | [] -> Success (List.rev valuesRev, List.rev msgsRev)
            | x :: rest ->
                match switchFunction x with
                | Success (v, msgs) -> loop (v :: valuesRev) (revAppend msgs msgsRev) rest
                // Short-circuit: the rest of the list is never visited.
                | Failure errs      -> Failure (List.rev msgsRev @ errs)
        loop [] [] inputs

    /// <summary>
    /// Array counterpart of <see cref="traverseList"/>: maps each element through a switch function and collects the
    /// results into a single Returns of an array. ACCUMULATES ALL FAILURES (does NOT stop at the first failure).
    /// </summary>
    /// <remarks>
    /// Writes the mapped values straight into a pre-sized result array, avoiding the intermediate (reversed) value
    /// list that <c>traverseList</c> builds and then reverses; worthwhile on hot paths and large inputs. Each call to
    /// the switch function still allocates its own <c>Returns</c>.
    /// </remarks>
    /// <param name="switchFunction">The function applied to each input element, producing a <c>Returns</c> per element.</param>
    /// <param name="inputs">The array of values to map and combine.</param>
    /// <returns>A Success of the array of mapped values (all warnings merged, in order) if every element succeeded; otherwise a Failure with every accumulated error, in order.</returns>
    let traverseArray (switchFunction: 'TSuccess1 -> Returns<'TSuccess2,'TMessage>) (inputs: 'TSuccess1 array) : Returns<'TSuccess2 array,'TMessage> =
        let revAppend xs ys = List.fold (fun acc x -> x :: acc) ys xs
        let values : 'TSuccess2 array = Array.zeroCreate inputs.Length
        let mutable msgsRev = []
        let mutable errsRev = []
        for i in 0 .. inputs.Length - 1 do
            match switchFunction inputs.[i] with
            | Success (v, msgs) ->
                values.[i] <- v
                msgsRev <- revAppend msgs msgsRev
            // Keep going after a failure, so that every failing element contributes its errors.
            | Failure errs -> errsRev <- revAppend errs errsRev
        match errsRev with
        | [] -> Success (values, List.rev msgsRev)
        | _  -> Failure (List.rev errsRev)

    /// <summary>
    /// Array counterpart of <see cref="traverseListFailFast"/>: maps each element through a switch function and
    /// collects the results into a single Returns of an array, STOPPING AT THE FIRST FAILURE.
    /// </summary>
    /// <remarks>
    /// Same semantics as <c>traverseListFailFast</c> (earlier warnings are kept, followed by the failing element's
    /// errors); same allocation advantage as <see cref="traverseArray"/>. Do not use it for validation.
    /// </remarks>
    /// <param name="switchFunction">The function applied to each input element, producing a <c>Returns</c> per element.</param>
    /// <param name="inputs">The array of values to map and combine.</param>
    /// <returns>A Success of the array of mapped values (all warnings merged, in order) if every element succeeded; otherwise a Failure with the earlier warnings followed by the first failing element's errors.</returns>
    let traverseArrayFailFast (switchFunction: 'TSuccess1 -> Returns<'TSuccess2,'TMessage>) (inputs: 'TSuccess1 array) : Returns<'TSuccess2 array,'TMessage> =
        let revAppend xs ys = List.fold (fun acc x -> x :: acc) ys xs
        let values : 'TSuccess2 array = Array.zeroCreate inputs.Length
        let mutable msgsRev = []
        let mutable failure = None
        let mutable i = 0
        while failure.IsNone && i < inputs.Length do
            match switchFunction inputs.[i] with
            | Success (v, msgs) ->
                values.[i] <- v
                msgsRev <- revAppend msgs msgsRev
            | Failure errs -> failure <- Some (Failure (List.rev msgsRev @ errs))
            i <- i + 1
        match failure with
        | Some f -> f
        | None   -> Success (values, List.rev msgsRev)

    // -------------------------------------------------------------------------------------- //

    // ********************
    // ** VALIDATE ALL  ***
    // ********************

    /// <summary>
    /// Runs every validator in the list against the same input value and collects ALL errors and warnings.
    /// Each validator must return a <c>Returns&lt;unit,'TMessage&gt;</c> — unit on success, messages on warning/failure.
    /// If every validator succeeds, returns a Success of the original value with all warnings merged.
    /// If any validator fails, returns a Failure with every accumulated error (no short-circuiting).
    /// </summary>
    /// <param name="validators">The independent validators to run against <paramref name="value"/>.</param>
    /// <param name="value">The value being validated; also the value returned on overall success.</param>
    /// <returns>A Success of <paramref name="value"/> with every warning merged, or a Failure with every error merged.</returns>
    let validateAll (validators: ('TSuccess -> Returns<unit,'TMessage>) list) (value: 'TSuccess) : Returns<'TSuccess,'TMessage> =
        // Every validator runs unconditionally against the same value (no short-circuiting), so all of
        // them get a chance to report a problem in a single pass.
        let results = validators |> List.map (fun v -> v value)
        // Pull out just the error messages from any failing validator...
        let errors  = results |> List.collect (fun r -> match r with | Failure msgs       -> msgs | _ -> [])
        // ...and, separately, just the warning messages from any (still-successful) validator.
        let warns   = results |> List.collect (fun r -> match r with | Success (_, msgs)  -> msgs | _ -> [])
        match errors with
        // No errors at all: overall success, carrying every warning collected along the way.
        | [] -> Success (value, warns)
        // At least one validator failed: overall failure, carrying every error collected (warnings are dropped).
        | _  -> Failure errors

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Infix/operator forms of the core <c>Returns</c> combinators, auto-opened alongside the <c>Returns</c> module.
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
        /// <param name="returns">The input <c>Returns</c> (result) container.</param>
        /// <param name="successFunction">The switch function to bind with.</param>
        /// <returns>The result of <c>Returns.bind successFunction returns</c>.</returns>
        let inline (>>=) returns successFunction =
            bind successFunction returns
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
        /// <returns>A function <c>Returns&lt;'a,'c&gt; -&gt; Returns&lt;'b,'c&gt;</c> equivalent to <c>Returns.map successFunction</c>.</returns>
        let inline (<!>) successFunction =
            map successFunction
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
        /// <param name="wrappedSuccessFunction">The function already wrapped in a <c>Returns</c> container.</param>
        /// <returns>A function <c>Returns&lt;'a,'c&gt; -&gt; Returns&lt;'b,'c&gt;</c> equivalent to <c>Returns.apply wrappedSuccessFunction</c>.</returns>
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
        /// <param name="switchFunction1">The first switch function to run against the shared input.</param>
        /// <param name="switchFunction2">The second switch function to run against the shared input.</param>
        /// <returns>A single function running both switch functions in parallel, keeping the first Success value and concatenating errors on double failure.</returns>
        let inline (&&&) switchFunction1 switchFunction2 =
            // On double-success, arbitrarily keep the first branch's value (the caller only wanted validation,
            // not a value to combine); on double-failure, concatenate both branches' error messages.
            let addSuccess s1 _ = s1
            let addFailure errs1 errs2 = errs1 @ errs2  // concatenate error messages.
            plus addSuccess addFailure switchFunction1 switchFunction2
            (*
            static member
                ( &&& ) : switchFunction1:('a -> Returns<'b,'c>) * switchFunction2:('a -> Returns<'d,'c>)
                          -> ('a -> Returns<'b,'c>)
            *)

// -------------------------------------------------------------------------------------- //
// -------------------------------------------------------------------------------------- //

/// <summary>
/// Computation-expression builder type that enables the <c>returns { ... }</c> syntax, giving Railway-Oriented
/// error handling (<c>let!</c>/<c>do!</c> that short-circuit on Failure, <c>and!</c> for accumulating parallel
/// validation) over the <c>Returns&lt;'TSuccess,'TMessage&gt;</c> type.
/// </summary>
[<Sealed>]
type ReturnsBuilder() =

    /// <summary>Produces the "no result" value for an empty CE block: a warning-free Success of unit.</summary>
    /// <returns>A Success of <c>()</c> with no warnings.</returns>
    member __.Zero() = Returns.ok()

    /// <summary>Identity hook the F# compiler uses to normalize already-<c>Returns</c>-typed expressions used in <c>let!</c>/<c>and!</c>.</summary>
    /// <param name="returns">The <c>Returns</c> value to pass through unchanged.</param>
    /// <returns><paramref name="returns"/>, unchanged.</returns>
    member inline _.Source(returns: Returns<_, _>) : Returns<_, _> = returns

    /// <summary>Implements <c>let! x = m in ...</c>: sequential (short-circuiting) binding.</summary>
    /// <param name="m">The <c>Returns</c> value being bound.</param>
    /// <param name="f">The continuation to run with the unwrapped Success value.</param>
    /// <returns>The result of <c>Returns.bind f m</c>.</returns>
    member __.Bind(m, f) = Returns.bind f m

    /// <summary>Implements <c>let! x = m in return f x</c> as a single step (used by the compiler as an optimization of Bind+Return).</summary>
    /// <param name="x">The <c>Returns</c> value being bound.</param>
    /// <param name="f">The plain (non-Returns-returning) function to map the unwrapped Success value with.</param>
    /// <returns>The result of <c>Returns.map f x</c>.</returns>
    member __.BindReturn(x: Returns<'T, 'U>, f) = Returns.map f x

    /// Enables the <c>and!</c> syntax for parallel binding.
    /// Both branches are evaluated independently; errors from both failures are accumulated.
    /// <param name="t1">The first parallel branch.</param>
    /// <param name="t2">The second parallel branch.</param>
    /// <returns>A Success of the tupled values (warnings merged) if both succeeded; a Failure with both branches' errors concatenated if both failed; otherwise the single Failure that occurred.</returns>
    member __.MergeSources(t1: Returns<'T1,'M>, t2: Returns<'T2,'M>) : Returns<'T1 * 'T2,'M> =
        match t1, t2 with
        // Both succeeded: tuple the values, merge the warnings.
        | Success (v1, msgs1), Success (v2, msgs2) -> Success ((v1, v2), msgs1 @ msgs2)
        // Both failed: this is what gives `and!` its error-accumulating (as opposed to short-circuiting) behavior.
        | Failure errs1,       Failure errs2        -> Failure (errs1 @ errs2)
        // Exactly one failed: propagate that single failure.
        | Failure errs,        _                    -> Failure errs
        | _,                   Failure errs         -> Failure errs

    /// <summary>Implements <c>return x</c>: wraps a plain value in a warning-free Success.</summary>
    /// <param name="x">The value to wrap.</param>
    /// <returns>The result of <c>Returns.ok x</c>.</returns>
    member __.Return(x) = Returns.ok x

    /// <summary>Implements <c>return! m</c>: returns an already-<c>Returns</c>-typed value as-is.</summary>
    /// <param name="x">The <c>Returns</c> value to return directly.</param>
    /// <returns><paramref name="x"/>, unchanged.</returns>
    member __.ReturnFrom(x) = x

    /// <summary>Implements sequencing of two statements in the CE body (e.g. <c>do! a; b</c>): runs <paramref name="a"/>, and if it succeeds, continues with <paramref name="b"/>.</summary>
    /// <param name="a">The first (unit-typed) computation to run.</param>
    /// <param name="b">The continuation to run if <paramref name="a"/> succeeded.</param>
    /// <returns>The result of <c>Returns.bind b a</c>.</returns>
    member __.Combine (a, b) = Returns.bind b a

    /// <summary>Delays evaluation of a CE block's body until it is actually run, as required by the CE machinery.</summary>
    /// <param name="f">The thunk wrapping the delayed computation.</param>
    /// <returns><paramref name="f"/>, unchanged.</returns>
    member __.Delay f = f

    /// <summary>Forces evaluation of a delayed computation produced by <see cref="Delay"/>.</summary>
    /// <param name="f">The thunk to invoke.</param>
    /// <returns>The result of invoking <paramref name="f"/>.</returns>
    member __.Run f = f ()

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
    member x.Using(d:#IDisposable, body) =
        let returns = fun () -> body d
        x.TryFinally (returns, fun () ->
            match d with
            | null -> ()
            | d -> d.Dispose())

    /// <summary>Implements <c>while guard do body</c> inside the CE, accumulating warnings from every iteration and stopping (propagating the error) at the first Failure.</summary>
    /// <param name="guard">The loop condition, re-checked before each iteration.</param>
    /// <param name="body">The loop body; must produce a <c>Returns&lt;unit,'TMessage&gt;</c> each iteration.</param>
    /// <returns>A Success of unit with every iteration's warnings collected (in order) if the loop ran to completion; otherwise the first Failure encountered, with all warnings collected up to that point prepended to its errors.</returns>
    member _.While (guard, body) =
        // Iterative (not recursive): a recursive formulation via Returns.bind would grow the call
        // stack by one frame per loop iteration and overflow on long-running `while` loops.
        // warningsRev accumulates warnings in reverse (cheap prepend) across iterations; reversed back at the end.
        let mutable warningsRev : 'TMessage list = []
        // Once set, `failure` holds the terminal Failure result and the loop condition below stops iterating.
        let mutable failure : Returns<unit,'TMessage> option = None
        while failure.IsNone && guard () do
            match body () with
            // Iteration succeeded (with possibly some warnings): fold its warnings into the running total and keep looping.
            | Success ((), msgs) -> warningsRev <- List.fold (fun acc m -> m :: acc) warningsRev msgs
            // Iteration failed: capture the failure (prefixed with every warning collected so far) and stop looping.
            | Failure errs        -> failure <- Some (Failure (List.rev warningsRev @ errs))
        match failure with
        | Some f -> f
        | None   -> Success ((), List.rev warningsRev)

    /// <summary>Implements <c>for x in s do body</c> inside the CE by translating it into a <see cref="While"/> loop driven by the sequence's enumerator (itself disposed via <see cref="Using"/>).</summary>
    /// <param name="s">The sequence to iterate over.</param>
    /// <param name="body">The loop body, invoked once per element.</param>
    /// <returns>The result of the equivalent <see cref="While"/> loop over the sequence's enumerator.</returns>
    member x.For(s:seq<_>, body) =
        x.Using(s.GetEnumerator(), fun enum ->
            x.While(enum.MoveNext,
                x.Delay(fun () -> body enum.Current)))

/// <summary>
/// Module hosting the single, shared <see cref="ReturnsBuilder"/> instance used to enter the <c>returns { ... }</c> computation expression.
/// </summary>
[<AutoOpen>]
module ReturnsBuilder =
    /// Wraps computations in an error handling computation expression.
    let returns = ReturnsBuilder()

// ****************************************************************************************************** //
// ****************************************************************************************************** //

