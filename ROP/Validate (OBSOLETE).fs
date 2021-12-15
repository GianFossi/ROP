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

/// Contains error propagation functions and a computation expression builder for Railway-Oriented-Programming (ROP).
namespace Validate

// ****************************************************************************************************** //
// ****************************************************************************************************** //

// Open System name space.
open System
//open System.ComponentModel

// ****************************************************************************************************** //
// ****************************************************************************************************** //

// Validation is based on https://github.com/qfpl/validation

/// <summary>
/// A <c>Validation</c> is a container (similar to 'Result') with a status that may be: 
/// a 'Success' containing a VALID VALUE of type 't.  
/// a 'Failure' containing an ERROR MESSAGE of type 'TMessage. 
/// The <c>Validation</c> (result) container accumulates 'Failure' message.
/// </summary>
type Validation<'Error, 'T> =
  
  /// <summary>
  /// Represents the <c>Validation</c> container in a failed status/computation.
  /// </summary>
  | Failure of 'Error
  
  /// <summary>
  /// Represents the <c>Validation</c> container in a successful status/computation.
  /// </summary>
  | Success of 'T

  // -------------------------------------------------------------------------------------- //

  /// <summary>
  /// Convert this <c>Validation</c> container into a string.
  /// </summary>
  override this.ToString() =
      match this with
      | Success(value) -> sprintf "Success: %A" value 
      | Failure(error) -> sprintf "Failure: %A" error 

// ****************************************************************************************************** //
// ****************************************************************************************************** //

/// <summary>
/// Basic operations on <c>Validation</c>.
/// </summary>
module Validation =

    // -------------------------------------------------------------------------------------- //

    /// /// Wraps a value in a Success of a <c>Validation</c> type.
    /// <param name="source">The input <c>Validation</c> container.</param>
    let inline ok<'Error, 'T> (source:'T) : Validation<'Error, 'T> = 
        Success(source)

    /// Wraps a single message in a Failure of a <c>Validation</c> type.
    /// <param name="msg">The Failure Message associated to the Failure status.</param>
    let inline fail<'Error, 'T> (msg:'Error) : Validation<'Error, 'T> = 
        Failure( msg )

    // -------------------------------------------------------------------------------------- //

    /// Takes a <c>Validation</c> container and extract its Value in case of Success.
    /// Otherwise, determine which Value has to be to returned in base to the generated list of Errors, using a specified function as selector.
    /// <param name="source">The input <c>Validation</c> container.</param>
    let defaultValue (value: 'T) (source: Validation<'Error,'T>) : 'T = 
        match source with 
        | Success v -> v 
        | _ -> value
    
    /// Takes a <c>Validation</c> container and extract its Value in case of Success.
    /// Otherwise, determine which Value has to be to returned in base to the generated list of Errors, using a specified function as selector.
    /// <param name="source">The input <c>Validation</c> container.</param>
    let defaultWith (compensation: 'Error->'T) (source: Validation<'Error,'T>) : 'T = match source with | Success x -> x | Failure e -> compensation e

    /// Takes a <c>Returns</c> (result) container and extract its Value in case of Success. 
    /// Otherwise the throws an exception with the Failure message of the container.
    /// <param name="source">The input <c>Validation</c> container.</param>
    let valueOrFailwith (source: Validation<'Error,'T>) = 
        let raiseExn msgs = 
            msgs
            |> Seq.map (sprintf "%O")
            |> String.concat (Environment.NewLine + "\t")
            |> failwith
        match source with
        | Success(s, _) -> s
        | Failure(errs) -> raiseExn (errs)
    
    // -------------------------------------------------------------------------------------- //
    
    /// Takes a source container and categorizes it based on its state and based on the presence of any extra messages.
    /// <param name="source">The input <c>Validation</c> container.</param>
    let (|Pass|Fail|) (source: Validation<'Error,'T>) =
        match source with
        | Failure e -> Fail e
        | Success a -> Pass a

    // -------------------------------------------------------------------------------------- //

    /// Takes a <c>Validation</c> container and check if it is a Success.
    /// <param name="source">The input <c>Validation</c> container.</param>
    /// <returns>Returns True if source is a Success, or False if source is a Failure.</returns>
    let isSucceeded (source: Validation<'Error,'T>) = 
        match source with
        | Success _ -> true
        | _ -> false

    /// Takes a <c>Validation</c> container and check if it is a Failure.
    /// <param name="source">The input <c>Validation</c> container.</param>
    /// <returns>Returns True if source is a Failure, or False if source is a Success.</returns>
    let isFailed (source: Validation<'Error,'T>) = 
        match source with
        | Failure _ -> true
        | _ -> false
    
    // -------------------------------------------------------------------------------------- //

    /// Change the type of Error, specifying a conversion function.
    /// in case of 'Failure', transform the type 'Error1 into type 'Error2 (applying teh conversion function).
    /// In case of 'Success', the valid value is transfered.
    /// <param name="conversionFunction">The conversion function (from 'Error1 to 'Error2).</param>
    /// <param name="source">The input <c>Validation</c> container.</param>
    let mapError (conversionFunction: 'Error1 -> 'Error2) (source: Validation<'Error1,'T>) : Validation<'Error2, 'T> = 
        match source with 
        | Success (x) -> Success (x)
        | Failure error -> 
            let error' = conversionFunction error
            Failure error'
    
    // -------------------------------------------------------------------------------------- //

    /// Creates a safe version of the supplied function, 
    /// applying the given function to the specified input Value and 
    /// catch its output as a <c>Validation</c> container, 
    /// instead of throwing any internally raised exception/s.
    /// <param name="givenFunction">The function to be applied to the input value (that may raise an exception!).</param>
    /// <param name="source">The input <c>Validation</c> container.</param>
    let tryCatch (givenFunction: 'T->'U) (value:'T) : Validation<exn, 'U> = 
        try
            Success (givenFunction value )
        with
        | exn -> Failure exn

    /// Creates a safe version of the supplied function, 
    /// applying the given function to the specified input Value and 
    /// catch its output as a <c>Validation</c> container, 
    /// instead of throwing any internally raised exception/s.
    /// <param name="givenFunction">The function to be applied to the input value (that may raise an exception!).</param>
    /// <param name="source">The input <c>Validation</c> container.</param>
    let protect (f: 'T->'U) (value:'T) : Validation<exn, 'U>  =
       tryCatch f value

    // -------------------------------------------------------------------------------------- //

    /// Takes a Option and transform it into an <c>Validation</c>.
    /// If Some then its Value is wrapped into a Success otherwise returns a Failure with the specified message.
    /// <param name="option">The input option type.</param>
    let ofOption (failureMesssages:'Error) (option:option<'T>) = 
        match option with
        | Some x -> ok x
        | None -> fail failureMesssages 

    /// Takes a <c>Validation</c> container and transform it into an Option.
    /// <param name="source">The input <c>Validation</c> container.</param>
    let toOption (source: Validation<'Error,'T>)  = 
        match source with
        | Success (value) -> Some value
        | Failure (_) -> Option.None

    // -------------------------------------------------------------------------------------- //

    /// Converts a Choice into a <c>Validation</c>.
    /// <param name="choice">The input choice type.</param>
    let ofChoice (choice : Choice<'T, 'Error>)  =
        match choice with
        | Choice1Of2 value -> ok value
        | Choice2Of2 error -> fail error

    /// Converts a <c>Validation</c> (result) container into a Choice.
    /// <param name="source">The input <c>Validation</c> container.</param>
    let toChoice (source: Validation<'Error,'T>) =
        match source with
        | Success (value) -> Choice1Of2 value
        | Failure (error) -> Choice2Of2 error

    // -------------------------------------------------------------------------------------- //

    /// Converts a Result into a <c>Validation</c>.
    /// <param name="result">The input result type.</param>
    let ofResult ( result: Result<'T, 'Error list>)  =
        match result with
        | Result.Ok value -> ok value
        | Result.Error error -> fail error

    /// Converts a <c>Validation</c> container into a Choice.
    /// <param name="source">The input <c>Validation</c> container.</param>
    let toResult (source: Validation<'Error,'T>) =
        match source with
        | Success (value) -> Result.Ok (value)
        | Failure (error) -> Result.Error error

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **    EITHER     ***
    // ********************
   
    /// Takes an input <c>Validation</c> container and 
    /// maps it with "fSuccess" function if it is a Success 
    /// otherwise 
    /// maps it with "fFailure" function if it is a Failure.
    /// <param name="fSuccess">Function to be applied to source, if it contains a Success value.</param>
    /// <param name="fFailure">Function to be applied to source, if it contains a Failure value.</param>
    /// <param name="source">The input <c>Validation</c> container.</param>
    /// <returns>The result of applying either functions.</returns>
    let either (fSuccess: 'T->'U) (fFailure: 'Error->'U) (source: Validation<'Error,'T>) =
        match source with 
        | Success v -> fSuccess v 
        | Failure errs -> fFailure errs

    // -------------------------------------------------------------------------------------- //

    // ********************
    // **     MERGE     ***
    // ********************

    /// Merge two returns (result) containers.
    /// If both the inputs are in a success status, merge their success values (along with their warning messages, if any) in accordance with the behavior specified in given merge function.
    /// Otherwise returns the failure value, merging their error messages if both inputs are in a failure status.
    /// <param name="addSuccess">Define the behavior of the plus (composition), in case of success of both the input sources.</param>
    /// <param name="addFailure">Define the behavior of the plus (composition), in case of failure of both the input sources.</param>
    /// <param name="source1">The first input source.</param>
    /// <param name="source2">The second input source.</param>
    let inline plus addSuccess addFailure source1 source2 = 
        match (source1, source2) with
        | Success s1, Success s2 -> Success ( addSuccess s1 s2 )
        | Failure f1, Success _  -> Failure f1
        | Success _ , Failure f2 -> Failure f2
        | Failure f1, Failure f2 -> Failure ( addFailure f1 f2)
    
    
    /// Binds through a Validation, which is useful for
    /// composing Validations sequentially. Note that despite having a bind
    /// function of the correct type, Validation is not a monad.
    /// The reason is, this bind does not accumulate errors, so it does not
    /// agree with the Applicative instance.
    ///
    /// There is nothing wrong with using this function, it just does not make a
    /// valid Monad instance.
    let bind (f: 'T->Validation<'Error,_>) x : Validation<_,'U> =
        match x with 
        | Failure e -> Failure e
        | Success a -> f a

    // -------------------------------------------------------------------------------------- //

    /// Applies the wrapped value to the wrapped function when both are Success and returns a wrapped result or the Failure(s).
    /// <param name="f">The function wrapped in a Success or a Failure.</param>
    /// <param name="x">The value wrapped in a Success or a Failure.</param>
    /// <returns>A Success of the function applied to the value when both are Success, or the Failure(s) if more than one, combined with the Semigroup (++) operation of the Error type.</returns>
    let inline apply (wrappedSuccessFunction: Validation<'Error,'T->'U>) (source: Validation<'Error,'T>) =
        match wrappedSuccessFunction, source with
        | Failure e1, Failure e2 -> Failure (g e1 e2)
        | Failure e1, Success _  -> Failure e1
        | Success _ , Failure e2 -> Failure e2
        | Success f , Success a  -> Success (f a)

    /// Applies (map) the given function (successFunction: 'T->'U) to the Success Value of the given input <c>Validation</c> container (one input),
    /// otherwise the existing error of the input container is propagated.
    let inline map (successFunction: 'T->'U) (source: Validation<'Error,'T>) =
        match source with
        | Failure e -> Failure e
        | Success a -> Success (successFunction a) 

    /// Applies (map) the given function (successFunction: 'T->'U->'V) to the Success Value of the given input <c>Validation</c> containers (two inputs),
    /// otherwise the existing error of the input container is propagated.
    let inline map2 (successFunction: 'T->'U->'V) (source1: Validation<'Error,'T>) (source2: Validation<'Error,'U>) : Validation<'Error,'V> =
        match source1, source2 with
        | Failure e1, Failure e2 -> Failure (g e1 e2)
        | Failure e1, Success _  -> Failure e1
        | Success _ , Failure e2 -> Failure e2
        | Success x , Success y  -> Success (successFunction x y)

    //let inline bimap (f: 'T1->'U1) (g: 'T2->'U2) = function
    //    | Failure e -> Failure (f e)
    //    | Success a -> Success (g a)

    //let inline foldBack (folder: 'T->'State->'State) (source: Validation<'Error,'T>) (state: 'State) =
    //    match source with
    //    | Success a -> folder a state
    //    | Failure _ -> state

    ///// Traverse the Success case with the supplied function.
    //let inline traverse (f: 'T->'``Functor<'U>``) (source: Validation<'Error,'T>) : '``Functor<Validation<'Error,'U>>`` =
    //    match source with
    //    | Success a -> Validation<'Error,'U>.Success <!> f a
    //    | Failure e -> result (Validation<'Error,'U>.Failure e)

    ///// Traverse the Success case.
    //let inline sequence (source: Validation<'Error,'``Functor<'T>``>) : '``Functor<Validation<'Error,'T>>`` = traverse id source

    //let bifoldBack f g (source: Validation<'Error,'T>) (state: 'State) : 'State =
    //    match source with
    //    | Success a -> g a state
    //    | Failure e -> f e state

    ///// Like traverse but taking an additional function to traverse the Failure part as well.
    //let inline bitraverse (f: 'Error1->'``Functor<'Error2>``) (g: 'T1->'``Functor<'T2>``) (source: Validation<'Error1,'T1>) : '``Functor<Validation<'Error2,'T2>>`` =
    //    match source with
    //    | Success a -> Validation<'Error2,'T2>.Success <!> g a
    //    | Failure e -> Validation<'Error2,'T2>.Failure <!> f e

    ///// Like sequence but traversing the Failure part as well.
    //let inline bisequence (source: Validation<'``Functor<'Error>``,'``Functor<'T>``>) : '``Functor<Validation<'Error,'T>>`` = bitraverse id id source


    






    /// Takes two Validations and returns the first Success.
    /// If both are Failures it returns both Failures combined with the supplied function.
    let appValidation (combine: 'err -> 'err -> 'err) (e1': Validation<'err,'a>) (e2': Validation<'err,'a>) =
        match e1', e2' with
        | Failure e1 , Failure e2 -> Failure (combine e1 e2)
        | Failure _  , Success a2 -> Success a2
        | Success a1 , Failure _  -> Success a1
        | Success a1 , Success _  -> Success a1













type Validation<'err,'a> with

    // as Applicative
    static member Return x = Success x
    static member inline (<*>)  (f: Validation<_,'T->'U>, x: Validation<_,'T>) : Validation<_,_> = Validation.apply f x
    static member inline Lift2  (f, x: Validation<_,'T>, y: Validation<_,'U>) : Validation<_,'V> = Validation.map2 f x y

    #if !FABLE_COMPILER || FABLE_COMPILER_3
    // as Alternative (inherits from Applicative)
    static member inline get_Empty () = Failure (getEmpty ())
    static member inline (<|>) (x: Validation<_,_>, y: Validation<_,_>) = Validation.appValidation Control.Append.Invoke x y
    #endif

    // as Functor
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member Map (x: Validation<_,_>, f) = Validation.map f x
    
    static member (<!>) (f, x: Validation<_,_>) = Validation.map f x

    // as Bifunctor
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member Bimap (x: Validation<'T,'V>, f: 'T->'U, g: 'V->'W) : Validation<'U,'W> = Validation.bimap f g x

    #if !FABLE_COMPILER || FABLE_COMPILER_3

    // as Traversable
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member inline Traverse (t: Validation<'err,'a>, f: 'a->'b) : 'c = Validation.traverse f t

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member inline Sequence (t: Validation<'err,'a>) : 'c = Validation.sequence t

    #endif

    // as Bifoldable
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member inline BifoldMap (t: Validation<'err,'a>, f: 'err->'b, g: 'a->'b) : 'b =
        match t with
        | Failure a -> f a
        | Success a -> g a
        
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member inline BifoldBack (t: Validation<'err,'a>, f: 'err->'b->'b, g: 'a->'b->'b, z: 'b) : 'b = Validation.bifoldBack f g t z
         
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member inline Bifold (t: Validation<'err,'a>, f: 'b->'err->'b, g: 'b->'a->'b, z: 'b) : 'b =
        match t with
        | Failure a -> f z a
        | Success a -> g z a

    
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member inline Bitraverse (t: Validation<'err,'a>, f, g) = Validation.bitraverse f g t
    
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member inline Bisequence (t: Validation<'err,'a>) = Validation.bisequence t

