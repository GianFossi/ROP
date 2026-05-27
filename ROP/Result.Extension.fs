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

/// Additional operations on Result<'T,'Error> (from FSharpPlus: https://github.com/fsprojects/FSharpPlus/blob/master/src/FSharpPlus/Extensions/Result.fs)
module Result =
    
    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Result</c> container and extracts the Ok value or use the supplied default value when it's an Error.
    /// </summary>
    /// <param name="value">The default value to be used in case of Erroro status.</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    let defaultValue (value:'T) (source: Result<'T,'Error>) : 'T = match source with Ok v -> v | _ -> value

    /// <summary>
    /// Takes a <c>Result</c> container and extracts the Ok value or use the supplied function value to get a default value when it's an Error.
    /// </summary>
    /// <param name="compensation">The default value to be used in case of Erroro status.</param>
    /// <param name="source">The input <c>Result</c> type.</param>
    let defaultWith (compensation: 'Error->'T) (source: Result<'T,'Error>) : 'T = match source with Ok v -> v | Error e -> compensation e

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Result</c> container and extract its Value in case of Success. 
    /// Otherwise, raise/throw an exception with the list of Failure Messages associated to the Failure status.
    /// </summary>
    /// <param name="source">The input <c>Returns</c> (result) type.</param>
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
    let tryCatch (f: 'T->'U) (value:'T) : Result<'U,exn>=
        try
            Ok (f value)
        with
        | ex when not (ex :? OutOfMemoryException) -> Error ex

    /// <summary>
    /// Creates a safe version of the supplied function, which returns a Result instead of throwing exceptions.
    /// </summary>
    /// <param name="f">The supplied function (that may fail raising an exceptions).</param>
    /// <param name="value">The argument value of the supplied function.</param>
    let protect (f: 'T->'U) (value:'T) : Result<'U,exn>=
        tryCatch f value

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Takes a <c>Result</c> container container and transfor it into an Active Pattern result (|Pass|Fail|).
    /// </summary>
    /// <param name="source">The input <c>Result</c> type.</param>
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
    let toOption (source: Result<'T,'Error>) : 'T option =
        match source with
        | Ok x    -> Some x
        | Error _ -> None

    /// <summary>
    /// Creates a <c>Result</c> container from a Option.
    /// </summary>
    let ofOption (errorMesssage:'Error ) (option: Option<'T>) = 
        match option with 
        | Some x-> Ok x 
        | None ->  Error errorMesssage

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Converts a <c>Result</c> container into a Choice container.
    /// </summary>
    /// <param name="source">The input <c>Result</c> type.</param>
    let toChoice (source: Result<'T,'Error>) = 
        match source with 
        | Ok x-> Choice1Of2 x 
        | Error x -> Choice2Of2 x

    /// <summary>
    /// Creates a <c>Result</c> container from a Choice.
    /// </summary>
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
    let mapError (conversionFunction: 'Error1 -> 'Error2) (source: Result<'T,'Error1>) = 
        match source with 
        | Ok x -> Ok x
        | Error error -> 
            let error' = conversionFunction error
            Error error'

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
        Ok f
        |> apply <| x
        |> apply <| y
        |> apply <| z
        //match x, y, z  with 
        //| Ok a, Ok b, Ok c -> Ok ( f a b c ) 
        //| Error e, _, _ | _, Error e, _ | _, _, Error e -> Error e

    // -------------------------------------------------------------------------------------- //

    let inline map4 (okFunction : 'T -> 'U -> 'V -> 'W -> 'Z) 
                    (source1: Result<'T,'Error>) 
                    (source2: Result<'U,'Error>)
                    (source3: Result<'V,'Error>) 
                    (source4: Result<'W,'Error>) : Result<'Z,'Error> = 
        source1
        |> Result.map okFunction
        |> apply <| source2
        |> apply <| source3
        |> apply <| source4

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
    /// </summary>
    let inline fold (folder: 'State->'T->'State) (state: Result<'State,'Error>) (sources: Result<'T,'Error> seq) =
        use e = sources.GetEnumerator()
        let mutable state = state
        while e.MoveNext() do
            let addSuccess stateOk currentOk = folder stateOk currentOk
            let addFailure stateError _currentError = stateError
            state <- merge addSuccess addFailure state e.Current
        state

    /// <summary>
    /// Variant of fold for Result with list-typed errors that accumulates all errors across failures,
    /// rather than keeping only the first error encountered.
    /// </summary>
    let inline foldList (folder: 'State->'T->'State) (state: Result<'State,'Error list>) (sources: Result<'T,'Error list> seq) =
        use e = sources.GetEnumerator()
        let mutable state = state
        while e.MoveNext() do
            let addSuccess stateOk currentOk = folder stateOk currentOk
            let addFailure stateErrors currentErrors = stateErrors @ currentErrors
            state <- merge addSuccess addFailure state e.Current
        state

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
    let inline eitherTee fOk fError (source: Result<'T,'Error>) =
        let tee f x = f x; x;
        tee (either fOk fError) source

    /// <summary>
    /// Takes an input <c>Result</c> container and executes the given ok function on the (Success) Value and on its warning messages only.
    ///
    /// NOTE: the input <c>Result</c> container is propagated unchanged.
    /// </summary>
    let successTee fOk (source: Result<'T,'Error>) = 
        eitherTee fOk ignore source

    /// <summary>
    /// Takes an input <c>Returns</c> (result) container and executes the given Failure function on the Error Messages only.
    ///
    /// NOTE: the input <c>Returns</c> (result) container is propagated unchanged.
    /// </summary>
    let failureTee fError (source: Result<'T,'Error>) = 
        eitherTee ignore fError source

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **     LOG       ***
    // ********************
    
    /// <summary>
    /// </summary>
    let log (logger: string -> unit) (record:bool) (message:string) (source: Result<'T,'Error>) =
        let fOk s = logger (sprintf ">>> %s: Result is Ok: %A" message s)
        let fError err = logger (sprintf ">>> %s: Result is a Error: %A" message err)
        if record then
            eitherTee fOk fError source
        else
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
    let inline compose 
        ( switchFunction1: 'T -> Result<'U,'Message>) 
        ( switchFunction2: 'U -> Result<'V,'Message>)
        (value : 'T) = 

        match ( switchFunction1 value ) with
        | Ok s -> ( switchFunction2 s ) 
        | Error e -> Error e
        (*
        
            Alternative code:
                value
                |> bind switchFunction1 >> bind switchFunction2
        
        *)

    // -------------------------------------------------------------------------------------- //

    /// Applies the given "dead-end function" to a "value" and return the input "value" ignoring the results of the given function.
    /// (">=> toSwitchFunction" is exactly the same as ">> map").
    let tee successFunction (value:'T) = 
        successFunction value |> ignore
        value

    // -------------------------------------------------------------------------------------- //

    /// <summary>
    /// Creates two lists by classifying the values depending on whether they were wrapped with Ok or Error.
    /// </summary>
    /// <returns>
    /// A tuple with both resulting lists, Oks are in the first list.
    /// </returns>
    let partition (source: list<Result<'T,'Error>>) =
        let rec loop ((acc1, acc2) as acc) = function
            | [] -> acc
            | x::xs ->
                match x with
                | Ok x -> loop (x::acc1, acc2) xs
                | Error x -> loop (acc1, x::acc2) xs
        loop ([], []) (List.rev source)

    // -------------------------------------------------------------------------------------- //

    /// Takes two results and returns a tuple of the pair
    let zip x1 x2 =
        match x1, x2 with
        | Ok x1res, Ok x2res -> Ok(x1res, x2res)
        | Error e, _ -> Error e
        | _, Error e -> Error e

    // -------------------------------------------------------------------------------------- //

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

/// Builder type for error handling computation expressions.
[<Sealed>]
type ResultlBuilder() = 
    
    member __.Zero() = Result.Ok()

    member inline _.Source(result: Result<_, _>) : Result<_, _> = result

    member inline __.Bind(m, f) = Result.bind f m

    member __.BindReturn(x: Result<'T, 'U>, f) = Result.map f x
        
    member __.Return(x) = Result.Ok x
        
    member inline __.ReturnFrom(x) = x
        
    member __.Combine (a, b) = Result.bind b a
        
    member __.Delay f = f
        
    member inline __.Run f = f ()
        
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
        
    member x.Using(d:#System.IDisposable, body) =
        let returns = fun () -> body d
        x.TryFinally (returns, fun () ->
            match d with
            | null -> ()
            | d -> d.Dispose())
        
    member x.While (guard, body) =
        if not <| guard () then
            x.Zero()
        else
            Result.bind (fun () -> x.While(guard, body)) (body())
        
    member x.For(s:seq<_>, body) =
        x.Using(s.GetEnumerator(), fun enum ->
            x.While(enum.MoveNext,
                x.Delay(fun () -> body enum.Current)))
    
[<AutoOpen>]
module ResultlBuilder =
    /// Wraps computations in an error handling computation expression.
    let result = ResultlBuilder()

// ****************************************************************************************************** //
// ****************************************************************************************************** //

module Check =

    /// Converts a nullable value into a Result, using the given error if null
    let isNotNull error value =
        match value with
        | null -> Error error
        | nonnull -> Ok nonnull

    /// Returns the specified error if the value is false.
    let isTrue error value = 
        if value then Ok() else Error error

    /// Returns the specified error if the value is true.
    let isFalse error value = 
        if not value then Ok() else Error error

    /// Converts an Option to a Result, using the given error if None.
    let isSome error option =
        match option with
        | Some value -> Ok value
        | None -> Error error

    /// Converts an Option to a Result, using the given error if Some.
    let isNone error option =
        match option with
        | Some _ -> Error error
        | None -> Ok()

    /// Returns Ok if the sequence is empty, or the specified error if not.
    let isEmpty error values =
        if Seq.isEmpty values then
            Ok()
        else
            Error error

    /// Returns the specified error if the sequence is empty, or Ok if not.
    let isNotEmpty error values =
        if Seq.isEmpty values then
            Error error
        else
            Ok()
        
    /// Returns the first item of the sequence if it exists, or the specified
    /// error if the sequence is empty
    let hasHead error values =
        match Seq.tryHead values with
        | Some value -> Ok value
        | None -> Error error

    /// Returns Ok if the two values are equal, or the specified error if not.
    /// Same as requireEqualTo, but with a signature that fits normal function
    /// application better than piping.
    let areEqualTo other error this = 
        if this = other then Ok() else Error error

    module string =
            
        /// Converts a nullable value into a Result, using the given error if null
        let hasLengthWithin (minLength, maxLength) error (value:string) =
            match value.Length with
            | l when l < minLength || l > maxLength -> Error error
            | _ -> Ok value

    module double =

        /// Check if the given value fall inside a rage defined by a minimum and a maximum value.
        let isWithin (min, max) error (value: double) =
            match value with
            | l when l < min || l > max -> Error error
            | _ -> Ok value

        /// Check if the given value is less than a reference value.
        let isLessThan (reference) error (value: double) =
            match value with
            | l when l < reference -> Ok value
            | _ -> Error error

        /// Check if the given value is less than or equal to a reference value.
        let isLessThanOrEqualTo (reference) error (value: double) =
            match value with
            | l when l <= reference -> Ok value
            | _ -> Error error

        /// Check if the given value is greater than a reference value.
        let isGreaterThan (reference) error (value: double) =
            match value with
            | l when l > reference -> Ok value
            | _ -> Error error

        /// Check if the given value is greater than or equal to a reference value.
        let isGreaterThanOrEqualTo (reference) error (value: double) =
            match value with
            | l when l >= reference -> Ok value
            | _ -> Error error