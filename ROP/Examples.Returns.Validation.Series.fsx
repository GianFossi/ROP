// ------------------------------------------------------------------------------------------------------ //

#load "Returns.fs"

// ------------------------------------------------------------------------------------------------------ //

open System
open ROP
// Custom operators (>>=, >=>, &&&, ...) live in a nested [<AutoOpen>] module that only
// auto-opens once its immediately-enclosing module is opened; `open ROP` alone does not
// reach it because `Returns` itself is [<RequireQualifiedAccess>].
open Returns.Operators

// ------------------------------------------------------------------------------------------------------ //

// Input record to be validated.
type Request = {name:string; email:string}

// ------------------------------------------------------------------------------------------------------ //

// Testing
let input1 = {name=""; email=""} // Failure.
let input2 = {name="Alice"; email=""} // Failure.
let input3 = {name="Alice Vien Dal Mare"; email=""} // Failure.
let input4 = {name="Alice"; email="good"} // Success.

// ------------------------------------------------------------------------------------------------------ //

// Switch Function: validate "name": Length > 0 (NOT EMPTY!).
// (Request -> ROP.Returns<Request,string>)
let validate1 input =
   if input.name = "" then Returns.fail "Name must not be blank"
   else Returns.ok input

// Switch Function: validate "name": Length <= 10 chars.
// (Request -> ROP.Returns<Request,string>)
let validate2 input =
   if input.name.Length > 10 then Returns.fail "Name must not be longer than 10 chars"
   else Returns.ok input

// Switch Function: validate "email".
// (Request -> ROP.Returns<Request,string>)
let validate3 input =
   if input.email = "" then Returns.fail "Email must not be blank"
   else Returns.ok input

// ------------------------------------------------------------------------------------------------------ //

// MODE A: glue the three validation functions together - IN SERIE.
// (Request -> ROP.Returns<Request,string>)
let combinedValidationA = 
    // convert the switch functions into a two-track input
    let validate2' = Returns.bind validate2
    let validate3' = Returns.bind validate3
    // connect the two-tracks together
    validate1 
    >> validate2' 
    >> validate3' 

// Check ...

// let input1 = {name=""; email=""} // Failure.
combinedValidationA input1 
|> printfn "Result1=%A" // ==> Failure ["Name must not be blank"]

// let input2 = {name="Alice"; email=""} // Failure.
combinedValidationA input2
|> printfn "Result2=%A" // ==> Failure ["Email must not be blank"]

// let input3 = {name="Alice Vien Dal Mare"; email=""} // Failure.
combinedValidationA input3
|> printfn "Result3=%A" // ==> Failure ["Name must not be longer than 10 chars"]

// let input4 = {name="Alice"; email="good"} // Success.
combinedValidationA input4
|> printfn "Result4=%A" // ==> Success {name = "Alice"; email = "good";}

// ------------------------------------------------------------------------------------------------------ //

// MODE B: glue the three validation functions together - IN SERIE.
// (Request -> ROP.Returns<Request,string>)
let combinedValidationB = 
    // connect the two-tracks together, moving the BIND operator.
    validate1 
    >> Returns.bind validate2 
    >> Returns.bind validate3

// Check ...

// let input1 = {name=""; email=""} // Failure.
combinedValidationB input1 
|> printfn "Result1=%A" // ==> Failure ["Name must not be blank"]

// let input2 = {name="Alice"; email=""} // Failure.
combinedValidationB input2
|> printfn "Result2=%A" // ==> Failure ["Email must not be blank"]

// let input3 = {name="Alice Vien Dal Mare"; email=""} // Failure.
combinedValidationB input3
|> printfn "Result3=%A" // ==> Failure ["Name must not be longer than 10 chars"]

// let input4 = {name="Alice"; email="good"} // Success.
combinedValidationB input4
|> printfn "Result4=%A" // ==> Success {name = "Alice"; email = "good";}

// ------------------------------------------------------------------------------------------------------ //

// MODE C: glue the three validation functions together - IN SERIE
// (Request -> ROP.Returns<Request,string>)
let combinedValidationC x = 
    
    x // Request
    |> validate1 // Request -> Return<Request,String>
    >>= validate2 // Return<Request,String> -> Return<Request,String> 
    >>= validate3 // Return<Request,String> -> Return<Request,String> 

// Check ...

// let input1 = {name=""; email=""} // Failure.
combinedValidationC input1 
|> printfn "Result1=%A" // ==> Failure ["Name must not be blank"]

// let input2 = {name="Alice"; email=""} // Failure.
combinedValidationC input2
|> printfn "Result2=%A" // ==> Failure ["Email must not be blank"]

// let input3 = {name="Alice Vien Dal Mare"; email=""} // Failure.
combinedValidationC input3
|> printfn "Result3=%A" // ==> Failure ["Name must not be longer than 10 chars"]

// let input4 = {name="Alice"; email="good"} // Success.
combinedValidationC input4
|> printfn "Result4=%A" // ==> Success {name = "Alice"; email = "good";}

// ------------------------------------------------------------------------------------------------------ //

// MODE D: glue the three validation functions together - IN SERIE
// (Request -> ROP.Returns<Request,string>)
let combinedValidationD = 

    validate1 // Request -> Return<Request,String>
    >=> validate2 // Return<Request,String> -> Return<Request,String> 
    >=> validate3 // Return<Request,String> -> Return<Request,String> 

// Check ...

// let input1 = {name=""; email=""} // Failure.
combinedValidationD input1 
|> printfn "Result1=%A" // ==> Failure ["Name must not be blank"]

// let input2 = {name="Alice"; email=""} // Failure.
combinedValidationD input2
|> printfn "Result2=%A" // ==> Failure ["Email must not be blank"]

// let input3 = {name="Alice Vien Dal Mare"; email=""} // Failure.
combinedValidationD input3
|> printfn "Result3=%A" // ==> Failure ["Name must not be longer than 10 chars"]

// let input4 = {name="Alice"; email="good"} // Success.
combinedValidationD input4
|> printfn "Result4=%A" // ==> Success {name = "Alice"; email = "good";}
    
// ------------------------------------------------------------------------------------------------------ //

// MODE E: glue the three validation functions together - IN PARALLEL
// (Request -> ROP.Returns<Request,string>)
let combinedValidationE = 
    validate1
    &&& validate2
    &&& validate3

// Check ...

// let input1 = {name=""; email=""} // Failure.
combinedValidationE input1 
|> printfn "Result1=%A" // ==> Failure ["Name must not be blank"; "Email must not be blank"]

// let input2 = {name="Alice"; email=""} // Failure.
combinedValidationE input2
|> printfn "Result2=%A" // ==> Failure ["Email must not be blank"]

// let input3 = {name="Alice Vien Dal Mare"; email=""} // Failure.
combinedValidationE input3
|> printfn "Result3=%A" // ==> Failure ["Name must not be longer than 10 chars"; "Email must not be blank"]

// let input4 = {name="Alice"; email="good"} // Success.
combinedValidationE input4
|> printfn "Result4=%A" // ==> Result4=Success {name = "Alice"; email = "good";}

// ------------------------------------------------------------------------------------------------------ //
