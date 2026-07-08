// ------------------------------------------------------------------------------------------------------ //

// Relative paths in #r resolve against this script's own directory (ROP/), not the process's
// working directory, so this keeps working regardless of where the repo is cloned. It does NOT
// self-update across a Debug/Release or TargetFramework change though -- #r only accepts a static
// literal path (no __SOURCE_DIRECTORY__ concatenation, no runtime-computed path), so if you build
// Release instead, or the project ever retargets off net8.0, update this literal to match.
// Also note: this references the DLL as of your LAST `dotnet build` -- rebuild first, or edits to
// ROP/*.fs won't be reflected here. If you'd rather always run against current source with no
// build step at all, replace this line with `#load "Returns.fs"` (as the other example scripts do).
#r "bin/Debug/net8.0/ROP.dll"

// ------------------------------------------------------------------------------------------------------ //

open ROP
// Custom operators (>=>, &&&, ...) live in a nested [<AutoOpen>] module that only auto-opens
// once its immediately-enclosing module is opened; `open ROP` alone does not reach it because
// `Returns` itself is [<RequireQualifiedAccess>].
open Returns.Operators

// ------------------------------------------------------------------------------------------------------ //

type MyMessages =
|   RecordNotAccepted
|   RecordNameIsNull
|   RecordSurnameIsNull
|   RecordNameTooLong
|   RecordSurnameTooLong
|   RecordAcceptedWithReserve

// ------------------------------------------------------------------------------------------------------ //

// Define the Input Record.
type Input = {Name: string; Surname: string } 

// Define the Input Record.
let validateNameNotEmpty ( input: Input ) =
    if input.Name = "" then 
        Returns.fail RecordNameIsNull
    else 
        Returns.ok input

let validateSurnameNameNotEmpty ( input: Input ) =
    if input.Surname = "" then 
        Returns.fail RecordSurnameIsNull
    else 
        Returns.ok input

let validateNameMaxLenghth ( input: Input ) =
    if input.Name.Length > 10 then 
        Returns.fail RecordNameTooLong
    else 
        Returns.ok input

let validateSurnameMaxLenghth ( input: Input ) =
    if input.Surname.Length > 10 then 
        Returns.fail RecordSurnameTooLong
    else 
        Returns.ok input

let toLowerCase ( input: Input ) = 
    { Name = input.Name.ToLower(); Surname = input.Surname.ToLower() }

let updateDatabase ( input: Input ) =
    printfn  ""
    printfn  "****** Welcome Mr. %s %s! ******" input.Name input.Surname |> ignore
    printfn  ""

// Lifts a plain 'a -> 'b function into a switch function 'a -> Returns<'b,msg> compatible with
// `>=>`. `Returns.switch` (from Scott Wlaschin's original ROP blog post) was never actually
// implemented in this library -- `f >> Returns.ok` is the direct equivalent.
let createRecord1 =
    validateNameNotEmpty
    >=> validateNameMaxLenghth          // >> bind validateNameMaxLenghth
    >=> validateSurnameNameNotEmpty     // >> bind validateSurnameNameNotEmpty
    >=> validateSurnameMaxLenghth       // >> bind validateSurnameMaxLenghth
    >=> (toLowerCase >> Returns.ok)                    // >> map toLowerCase
    >=> (Returns.tee updateDatabase >> Returns.ok)      // >> successTee updateDatabase
    >> Returns.log (printfn "%s") true "createRecord1"

createRecord1 {Name="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"; Surname="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"}

let createRecord2 =
    validateNameNotEmpty
    >> Returns.bind validateNameMaxLenghth
    >> Returns.bind validateSurnameNameNotEmpty
    >> Returns.bind validateSurnameMaxLenghth
    >> Returns.map toLowerCase
    >> Returns.successTee ( fun (input, _msgs) -> updateDatabase input )
    >> Returns.log (printfn "%s") true "createRecord2"

createRecord2 {Name="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"; Surname="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"}

let createRecord3 record =
    validateNameNotEmpty
    &&& validateNameMaxLenghth
    &&& validateSurnameNameNotEmpty
    &&& validateSurnameMaxLenghth
    >> Returns.map toLowerCase
    >> Returns.successTee ( fun (input, _msgs) -> updateDatabase input )
    >> Returns.log (printfn "%s") record "createRecord3"

createRecord3 true {Name=""; Surname=""} 
createRecord3 true {Name="Pippo"; Surname=""} 
createRecord3 true {Name=""; Surname="Franco"} 
createRecord3 true {Name="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"; Surname="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"} 
createRecord3 true {Name="Pippo"; Surname="Franco"} 
createRecord3 false {Name="Pippo"; Surname="Franco"} 

// ------------------------------------------------------------------------------------------------------ //
