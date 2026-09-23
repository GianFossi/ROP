// ****************************************************************************************************** //
// ****************************************************************************************************** //
// Author: G.L. Anfossi (ALO/UTEC).
//
// REVISION HYSTORY:
//
// 23/09/2026: Start.
//
// ****************************************************************************************************** //
// ****************************************************************************************************** //

/// <summary>
/// Test-framework-agnostic assertion helpers for <c>Returns</c> (result) containers.
/// Every helper either returns what the test needs next or raises a plain <see cref="System.Exception"/> whose message
/// renders EVERY message carried by the container (not just the first), which xUnit, NUnit and Expecto all report as a
/// test failure.
/// </summary>
/// <remarks>
/// Replaces the hand-written <c>let value r = match r with Success (v, _) -> v | Failure msgs -> failwith ...</c>
/// helper that every consuming test project otherwise reinvents, each slightly differently.
/// </remarks>
module ROP.Testing

open System

// ****************************************************************************************************** //
// ****************************************************************************************************** //

// Renders a message list as one numbered line per message, so a multi-error Failure stays readable in a test report.
let private render (msgs: 'TMessage list) =
    msgs
    |> List.mapi (fun i m -> sprintf "  [%d] %s" (i + 1) (string m))
    |> String.concat Environment.NewLine

// Renders the whole container, for "expected X but got Y" messages.
let private describe (returns: Returns<'TSuccess,'TMessage>) =
    match returns with
    | Success (value, [])   -> sprintf "Success %A with no warnings" value
    | Success (value, msgs) -> sprintf "Success %A with %d warning(s):%s%s" value msgs.Length Environment.NewLine (render msgs)
    | Failure errs          -> sprintf "Failure with %d error(s):%s%s" errs.Length Environment.NewLine (render errs)

// -------------------------------------------------------------------------------------- //

/// <summary>
/// Unwraps the value of a Success (ignoring its warnings), or fails the test with every error message rendered.
/// </summary>
/// <param name="returns">The <c>Returns</c> (result) under test.</param>
/// <returns>The Success value.</returns>
/// <exception cref="System.Exception">When the input is a Failure.</exception>
let getOrFail (returns: Returns<'TSuccess,'TMessage>) : 'TSuccess =
    match returns with
    | Success (value, _) -> value
    | Failure _ -> failwithf "Expected Success, but got %s" (describe returns)

/// <summary>
/// Unwraps a Success, returning its value together with its warnings, or fails the test with every error message rendered.
/// </summary>
/// <param name="returns">The <c>Returns</c> (result) under test.</param>
/// <returns>The Success value and its (possibly empty) warning list.</returns>
/// <exception cref="System.Exception">When the input is a Failure.</exception>
let getWithWarnings (returns: Returns<'TSuccess,'TMessage>) : 'TSuccess * 'TMessage list =
    match returns with
    | Success (value, msgs) -> value, msgs
    | Failure _ -> failwithf "Expected Success, but got %s" (describe returns)

/// <summary>
/// Asserts that the input is a Failure and returns its error messages.
/// </summary>
/// <param name="returns">The <c>Returns</c> (result) under test.</param>
/// <returns>The error messages of the Failure.</returns>
/// <exception cref="System.Exception">When the input is a Success.</exception>
let expectFailure (returns: Returns<'TSuccess,'TMessage>) : 'TMessage list =
    match returns with
    | Failure errs -> errs
    | Success _ -> failwithf "Expected Failure, but got %s" (describe returns)

/// <summary>
/// Asserts that the input is a Failure carrying at least one error message that matches the predicate.
/// </summary>
/// <param name="predicate">The condition at least one error message must satisfy.</param>
/// <param name="returns">The <c>Returns</c> (result) under test.</param>
/// <exception cref="System.Exception">When the input is a Success, or a Failure none of whose messages match.</exception>
let expectFailureMatching (predicate: 'TMessage -> bool) (returns: Returns<'TSuccess,'TMessage>) : unit =
    match returns with
    | Failure errs when List.exists predicate errs -> ()
    | Failure _ -> failwithf "Expected a Failure with an error matching the predicate, but none matched in %s" (describe returns)
    | Success _ -> failwithf "Expected a Failure with an error matching the predicate, but got %s" (describe returns)

/// <summary>
/// Asserts that the input is a Success carrying at least one warning that matches the predicate, and returns its value.
/// </summary>
/// <param name="predicate">The condition at least one warning must satisfy.</param>
/// <param name="returns">The <c>Returns</c> (result) under test.</param>
/// <returns>The Success value.</returns>
/// <exception cref="System.Exception">When the input is a Failure, or a Success none of whose warnings match.</exception>
let expectWarningMatching (predicate: 'TMessage -> bool) (returns: Returns<'TSuccess,'TMessage>) : 'TSuccess =
    match returns with
    | Success (value, msgs) when List.exists predicate msgs -> value
    | _ -> failwithf "Expected a Success with a warning matching the predicate, but got %s" (describe returns)

/// <summary>
/// Asserts that the input is a Success with no warnings at all, and returns its value.
/// </summary>
/// <param name="returns">The <c>Returns</c> (result) under test.</param>
/// <returns>The Success value.</returns>
/// <exception cref="System.Exception">When the input is a Failure, or a Success carrying any warning.</exception>
let expectNoWarnings (returns: Returns<'TSuccess,'TMessage>) : 'TSuccess =
    match returns with
    | Success (value, []) -> value
    | _ -> failwithf "Expected a Success with no warnings, but got %s" (describe returns)

// ****************************************************************************************************** //
// ****************************************************************************************************** //
