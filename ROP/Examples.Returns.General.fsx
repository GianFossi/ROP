// ------------------------------------------------------------------------------------------------------ //

#load "Returns.fs"

// ------------------------------------------------------------------------------------------------------ //

open System
open ROP
// Custom operators (>>=, >=>, <=<, <!>, <*>, &&&) live in a nested [<AutoOpen>] module,
// which only auto-opens once its immediately-enclosing module is opened. `Returns` itself
// is [<RequireQualifiedAccess>] (so `open ROP` alone does not cascade into it) - hence this
// explicit open is required for the operators used below to resolve.
open Returns.Operators

// ------------------------------------------------------------------------------------------------------ //

let value = 10

let msg x = sprintf "Warning! The specified value is %d" value

let msgs x = [ sprintf "Warning! The specified value is %d" value ; "I have added another warning!" ]

let errs = [ "Error1!"; "Error2!"; "Error3!" ]

Returns.ok<int,string> value // Success (10, [])

Returns.warn<int,string> ( msg value ) value // Success (10, ["Warning! The specified value is 10"])

Returns.warnmany<int,string> ( msgs value ) value // Success (10, ["Warning! The specified value is 10"; "I have added another warning!"])

Returns.fail<int,string> "Error!" // Failure ["Error!"]

Returns.failmany<int,string> errs // Failure ["Error1!"; "Error2!"; "Error3!"]

// ------------------------------------------------------------------------------------------------------ //

let func x = x * 2.0

Returns.ok<double,string> 5.0 |> Returns.map func // Success (10.0, [])

Returns.fail<double,string> "Error!" |> Returns.map func // Failure ["Error!"]

// ------------------------------------------------------------------------------------------------------ //

Returns.ok<double,string> 5.0 // Success (5.0, [])
|> Returns.map (fun x -> x * 2.0 ) // Success (10.0, [])
|> Returns.map (fun x -> x + 5.0 ) // Success (15.0, [])
|> Returns.map (fun x -> x / 10.0 ) // Success (1.5, [])

// ------------------------------------------------------------------------------------------------------ //

type Messages =
| IsNegativeValue
| AbsoluteValueExceed of int
| IsOdd
| IsEven

// Even numbers are divisible by 2.
let isEven x = (x % 2) = 0

// Odd numbers are not even.
let isOdd x = isEven x = false

let checkIsEven (x:int) = 
    if isEven x  then Returns.fail Messages.IsEven
    else Returns.warn Messages.IsOdd x

let checkIsNegative (x:int) = 
    if x < 0  then Returns.fail Messages.IsNegativeValue
    else Returns.ok x

let checkIsLargerThan10 (x:int) = 
    if Math.Abs( x ) > 10  then Returns.fail ( Messages.AbsoluteValueExceed 10 )
    else Returns.ok x

// Validation in SERIE (AND).
Returns.ok 9 >>= checkIsEven >>= checkIsNegative >>= checkIsLargerThan10 // Success (9, [IsOdd])
Returns.ok 8 >>= checkIsEven >>= checkIsNegative >>= checkIsLargerThan10 // Failure [IsEven]
Returns.ok 11 >>= checkIsEven >>= checkIsNegative >>= checkIsLargerThan10 // Failure [AbsoluteValueExceed 10; IsOdd]
Returns.ok -9 >>= checkIsEven >>= checkIsNegative >>= checkIsLargerThan10 //  Failure [IsNegativeValue; IsOdd]
Returns.ok -20 >>= checkIsEven >>= checkIsNegative >>= checkIsLargerThan10 //  Failure [IsNegativeValue; IsOdd]

// Validation in SERIE (AND).
let combined1 = Returns.bind checkIsEven >> Returns.bind checkIsNegative >> Returns.bind checkIsLargerThan10 // f: (Returns<int,Messages> -> Returns<int,Messages>)
Returns.ok -20 |> combined1 // Failure [IsEven]
Returns.ok -11 |> combined1 // Failure [IsNegativeValue; IsOdd]

// Validation in SERIE (AND).
let combined2 = checkIsEven >=> checkIsNegative >=> checkIsLargerThan10 // f: (int -> Returns<int,Messages>)
-20 |> combined2 // Failure [IsEven]
-11 |> combined2 // Failure [IsNegativeValue; IsOdd]

// Validation in SERIE (AND).
let combined3 = checkIsLargerThan10 <=< checkIsNegative <=< checkIsEven // f: (int -> Returns<int,Messages>)
-20 |> combined3 // Failure [IsEven]
-11 |> combined3 // Failure [IsNegativeValue; IsOdd]

// // Validation in PARALLEL (OR)
let combined4 = checkIsLargerThan10 &&& checkIsNegative &&& checkIsEven // f: (int -> Returns<int,Messages>)
-100 |> combined4 // Failure [AbsoluteValueExceed 10; IsNegativeValue; IsEven]

// ------------------------------------------------------------------------------------------------------ //

