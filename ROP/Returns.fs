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
type Returns<'TSuccess, 'TMessage> = 

    /// <summary>
    /// Represents the <c>Returns</c> (result) container in a successful status/computation.
    /// </summary>
    | Success of 'TSuccess * 'TMessage list
    
    /// <summary>
    /// Represents the <c>Returns</c> (result) container in a failed status/computation.
    /// </summary>
    | Failure of 'TMessage list
    
    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Convert this <c>Returns</c> (result) container into a string.
    /// </summary>
    override this.ToString() =
        let printMsgs msgs =
            msgs |> List.map (fun x -> x.ToString()) |> String.concat "; "
        match this with
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
    let ok<'TSuccess,'TMessage> (x:'TSuccess) : Returns<'TSuccess,'TMessage> = 
        Success(x, [])

    // -------------------------------------------------------------------------------------- //
    
    /// <summary>
    /// Wraps a value in a Success of a <c>Returns</c> (result) type and adds a single warning message.
    /// </summary>
    /// <param name="msg">The Warning Message associated to the Success value.</param>
    /// <param name="x">The Success value.</param>
    let warn<'TSuccess,'TMessage> (msg:'TMessage) (x:'TSuccess) : Returns<'TSuccess,'TMessage> = 
        Success(x,[msg])

    /// <summary>
    /// Wraps a value in a Success of a <c>Returns</c> (result) type and adds a single warning message.
    /// </summary>
    /// <param name="msgs">The list of Warning Message associated to the Success value.</param>
    /// <param name="x">The Success value.</param>
    let warnmany<'TSuccess,'TMessage> (msgs:'TMessage seq) (x:'TSuccess) : Returns<'TSuccess,'TMessage> = 
        Success(x,msgs |> Seq.toList )

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Wraps a single message in a Failure of a <c>Returns</c> (result) type.
    /// </summary>
    /// <param name="msg">The Failure Message associated to the Failure status.</param>
    let fail<'TSuccess,'TMessage> (msg:'TMessage) : Returns<'TSuccess,'TMessage> = 
        Failure([ msg ])

    /// <summary>
    /// Wraps a list of message in a Failure of a <c>Returns</c> (result) type.
    /// </summary>
    /// <param name="msgs">The list of Failure Messages associated to the Failure status.</param>
    let failmany<'TSuccess,'TMessage> (msgs:'TMessage seq) : Returns<'TSuccess,'TMessage> = 
        Failure( msgs |> Seq.toList )

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Change the Success status of a <c>Returns</c> (result) type into a Failure status, 
    /// if it contain any warning messages, transferring all the Warning Messages into the Failure container.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    let failOnWarnings (returns : Returns<'TSuccess,'TMessage>) : Returns<'TSuccess,'TMessage> =
      match returns with
      | Success (_, msgs) when msgs <> [] -> Failure msgs
      | _ -> returns

    // -------------------------------------------------------------------------------------- //
        
    /// <summary>
    /// Takes a <c>Returns</c> (result) container and extract its Value in case of Success.
    /// Otherwise, returns the given (DEFAULT) value in case of Failure.
    /// </summary>
    /// <param name="value">The default value.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    let defaultValue (value: 'TSuccess) (returns: Returns<'TSuccess,'TMessage>) : 'TSuccess = 
        match returns with 
        | Success (x,_) -> x
        | _ -> value

    /// Takes a <c>Returns</c> (result) container and extract its Value in case of Success.
    /// Otherwise, determine which (default) Value has to be to returned, in base to the generated list of Errors, using a given function as selector.
    /// <param name="compensation">The function that work as a selector to return the prover default value.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
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
    let valueOrFailwith (returns: Returns<'TSuccess,'TMessage>) : 'TSuccess = 
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
    let tryCatch (givenFunction: 'TSuccess1 -> 'TSuccess2) (value:'TSuccess1) : Returns<'TSuccess2,exn> =
        try
            Success (givenFunction value ,[] )
        with
        | ex when not (ex :? OutOfMemoryException) -> Failure [ex]

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Returns</c> (result) container and transfor it into an Active Pattern result (|Pass|Warn|Fail|).
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    let (|Pass|Warn|Fail|) (returns : Returns<'TSuccess,'TMessage>) =
      match returns with
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
        | Success (_, msgs) -> msgs.Length > 0
        | _ -> false

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Returns</c> (result) container and transform it into an Option container.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    let toOption (returns : Returns<'TSuccess,'TMessage>)  = 
        match returns with
        | Success (value,warns) -> Some (value,warns)
        | Failure (_) -> Option.None

    /// <summary>
    /// Takes a Option and transform it into an <c>Returns</c> (result) container.
    /// If Some then its Value is wrapped into a Success otherwise returns a Failure with the specified message.
    /// </summary>
    /// <param name="failureMesssage">The input option type.</param>
    /// <param name="option">The input option type.</param>
    let ofOption (failureMesssage:'TMessage) (option:option<'TSuccess>) = 
        match option with
        | Some x -> ok x
        | None -> fail failureMesssage 

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Converts a <c>Returns</c> (result) container into a Choice container.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    let toChoice (returns : Returns<'TSuccess,'TMessage>) =
        match returns with
        | Success (value,warns) -> Choice1Of2 (value,warns)
        | Failure (errors) -> Choice2Of2 errors

    /// <summary>
    /// Converts a Choice into a r<c>Returns</c> (result) container.
    /// </summary>
    /// <param name="choice">The input choice type.</param>
    let ofChoice (choice : Choice<'TSuccess * 'TMessage list, 'TMessage list>) =
        match choice with
        | Choice1Of2 (value, warns) -> Success (value, warns)
        | Choice2Of2 errors -> failmany errors

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Converts a <c>Returns</c> (result) container into a Choice.
    /// </summary>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    let toResult (returns : Returns<'TSuccess,'TMessage>) =
        match returns with
        | Success (value,warns) -> Result.Ok (value, warns)
        | Failure (errors) -> Result.Error errors

    /// <summary>
    /// Converts a Result into a <c>Returns</c> (result) container.
    /// </summary>
    let ofResult (result: Result<'TSuccess * 'TMessage list, 'TMessage list>) =
        match result with
        | Result.Ok (value, warns) -> Success (value, warns)
        | Result.Error errors -> failmany errors

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
        let fSuccess (x, msgs) = Success(x, msgs @ messages)
        let fFailure errs = Failure(errs @ messages)
        either fSuccess fFailure returns

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
    let inline bind (switchFunction : 'TSuccess1 -> Returns<'TSuccess2,'TMessage> ) (returns : Returns<'TSuccess1,'TMessage>) = 
        let fSuccess (s, msgs) = switchFunction  s |> jointMessages msgs
        let fFailure (msgs) = Failure msgs
        either fSuccess fFailure returns

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
    let inline map (successFunction : 'TSuccess1 -> 'TSuccess2) (returns : Returns<'TSuccess1,'TMessage>) = 
        let wrappedSuccessFunction = ok successFunction
        apply wrappedSuccessFunction returns
    
    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Maps the given function over all existing warning and failure messages of a <c>Returns</c> (result) container (if any).
    /// It works as a sort of Trasformation/Conversion Function of the warning and failure message type.
    /// </summary>
    /// <param name="conversionFunction">The conversion function (common for both the Warning and Error Message).</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    let mapMessages (conversionFunction: 'TMessage1 -> 'TMessage2) (returns : Returns<'TSuccess,'TMessage1>) = 
        match returns with 
        | Success (x,msgs) -> 
            let msgs' = List.map conversionFunction msgs
            Success (x, msgs')
        | Failure errors -> 
            let errors' = List.map conversionFunction errors
            Failure errors'

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
    let inline map2 (successFunction : 'TSuccess1 -> 'TSuccess2 -> 'TSuccess3) 
                    (returns1 : Returns<'TSuccess1,'TMessage>) 
                    (returns2 : Returns<'TSuccess2,'TMessage>) = 
        //successFunction <!> return1 <*> return2
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
    let inline map3 successFunction
                    (returns1 : Returns<'TSuccess1,'TMessage>)
                    (returns2 : Returns<'TSuccess2,'TMessage>)
                    (returns3 : Returns<'TSuccess3,'TMessage>) =
        // successFunction <!> return1 <*> return2 <*> return3
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
    let inline map4 successFunction (returns1 : Returns<'TSuccess1,'TMessage>)
                                    (returns2 : Returns<'TSuccess2,'TMessage>)
                                    (returns3 : Returns<'TSuccess3,'TMessage>)
                                    (returns4 : Returns<'TSuccess4,'TMessage>) =
        // successFunction <!> return1 <*> return2 <*> return3
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

    /// Collects a sequence of <c>Returns</c> (result) container and accumulates their values .
    /// If the sequence contains successes only, any warning messages will be folded together and propagated as an success.
    /// If the sequence contains one or more errors, all the error messages will be folded together and propagated as an error.
    let inline fold (folder: 'SuccesState->'TSuccess->'SuccesState) (state: Returns<'SuccesState,'Message>) (returns : Returns<'TSuccess,'Message> seq ) = 
        use e = returns.GetEnumerator()
        let mutable state = state
        while e.MoveNext() do
            let addSuccess stateOk currentOk = folder stateOk currentOk
            let addFailure stateError currentError = stateError @ currentError
            state <- merge addSuccess addFailure state e.Current
        state

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
    let inline eitherTee fSuccess fFailure (returns : Returns<'TSuccess,'TMessage>) =
        let tee f x = f x; x;
        tee (either fSuccess fFailure) returns

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container and executes the given Success function on the (Success) Value and on its warning messages only.
    ///
    /// NOTE: the input <c>Returns</c> (result) containe is propagated unchanged.
    /// </summary>
    let successTee successFunction (returns : Returns<'TSuccess,'TMessage>) = 
        eitherTee successFunction ignore returns

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container and executes the given Failure function on the Error Messages only.
    ///
    /// NOTE: the input <c>Returns</c> (result) container is propagated unchanged.
    /// </summary>
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
    /// <param name="record">Boolean value to indicate ioif teh Log action need to be performed (TRUE) or skipped (FALSE).</param>
    /// <param name="message">A description message.</param>
    /// <param name="returns">The input <c>Returns</c> (result) type.</param>
    let log (logger: string -> unit) (record:bool) (message:string) (returns : Returns<'TSuccess,'TMessage>) =
        let successFunction (s, msgs) = logger (sprintf ">>> %s: Returns is a Success: %A (%A)" message s msgs)
        let failureFunction errs = logger (sprintf ">>> %s Returns is a Failure: %A" message errs)
        if record then
            eitherTee successFunction failureFunction returns
        else
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
    let inline compose 
        ( switchFunction1: 'TSuccess1 -> Returns<'TSuccess2,'TMessage>) 
        ( switchFunction2: 'TSuccess2 -> Returns<'TSuccess3,'TMessage>)
        (value : 'TSuccess1) = 

        match ( switchFunction1 value ) with
        | Success (s,msgs) -> ( switchFunction2 s ) |> ( jointMessages msgs )
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
    /// <param name="source1">The first input source.</param>
    /// <param name="source2">The second input source.</param>
    let inline plus addSuccess addFailure switchFunction1 switchFunction2 value = 
        match (switchFunction1 value, switchFunction2 value) with
        | Success (s1,msgs1),Success (s2,msgs2) -> Success ( addSuccess s1 s2, msgs1@msgs2 )
        | Failure f1,Success _  -> Failure f1
        | Success _ ,Failure f2 -> Failure f2
        | Failure f1,Failure f2 -> Failure (addFailure f1 f2)

    // -------------------------------------------------------------------------------------- //

    /// Applies the given "dead-end function" to a "value" and return the input "value" ignoring the results of the given function.
    /// (">=> toSwitchFunction" is exactly the same as ">> map").
    let tee (successFunction: 'TSuccess1 -> 'TSuccess2) (value: 'TSuccess1) = 
        successFunction value |> ignore
        value

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Creates two lists by classifying the values depending on whether they were wrapped with Ok or Error.
    /// </summary>
    /// <returns>
    /// A tuple with both resulting lists, Oks are in the first list.
    /// </returns>
    let partition (returns: list<Returns<'TSuccess, 'TMessage>>) =
        let rec loop ((acc1, acc2) as acc) = function
            | [] -> acc
            | x::xs ->
                match x with
                | Success (v,msgs) -> loop ((v,msgs)::acc1, acc2) xs
                | Failure e -> loop (acc1, e::acc2) xs
        loop ([], []) (List.rev returns)

    // -------------------------------------------------------------------------------------- //

    /// Takes two results and returns a tuple of the pair
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
    /// All errors are accumulated: if any element fails, failures from every failing element are merged.
    /// </summary>
    let traverseList (switchFunction: 'TSuccess1 -> Returns<'TSuccess2,'TMessage>) (inputs: 'TSuccess1 list) : Returns<'TSuccess2 list,'TMessage> =
        let folder state current =
            match state, switchFunction current with
            | Success (acc, msgs1), Success (v, msgs2) -> Success (v :: acc, msgs1 @ msgs2)
            | Failure errs, Success _                  -> Failure errs
            | Success _, Failure errs                  -> Failure errs
            | Failure errs1, Failure errs2             -> Failure (errs1 @ errs2)
        List.fold folder (ok []) inputs |> map List.rev

    /// <summary>
    /// Converts a list of Returns into a Returns of a list, accumulating all errors if any element is a Failure.
    /// Equivalent to traverseList id.
    /// </summary>
    let sequenceList (returns: Returns<'TSuccess,'TMessage> list) : Returns<'TSuccess list,'TMessage> =
        traverseList id returns

    // -------------------------------------------------------------------------------------- //

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
        let inline (&&&) switchFunction1 switchFunction2 = 
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

/// Builder type for error handling computation expressions.
[<Sealed>]
type ReturnsBuilder() = 
        
    member __.Zero() = Returns.ok()

    member inline _.Source(returns: Returns<_, _>) : Returns<_, _> = returns
        
    member __.Bind(m, f) = Returns.bind f m

    member __.BindReturn(x: Returns<'T, 'U>, f) = Returns.map f x
        
    member __.Return(x) = Returns.ok x
        
    member __.ReturnFrom(x) = x
        
    member __.Combine (a, b) = Returns.bind b a
        
    member __.Delay f = f
        
    member __.Run f = f ()
        
    member __.TryWith (body, handler) =
        try
            body()
        with
        | e -> handler e
        
    member __.TryFinally (body, compensation) =
        try
            body()
        finally
            compensation()
        
    member x.Using(d:#IDisposable, body) =
        let returns = fun () -> body d
        x.TryFinally (returns, fun () ->
            match d with
            | null -> ()
            | d -> d.Dispose())
        
    member x.While (guard, body) =
        if not <| guard () then
            x.Zero()
        else
            Returns.bind (fun () -> x.While(guard, body)) (body())
        
    member x.For(s:seq<_>, body) =
        x.Using(s.GetEnumerator(), fun enum ->
            x.While(enum.MoveNext,
                x.Delay(fun () -> body enum.Current)))

[<AutoOpen>]
module ReturnsBuilder =
    /// Wraps computations in an error handling computation expression.
    let returns = ReturnsBuilder()

// ****************************************************************************************************** //
// ****************************************************************************************************** //


