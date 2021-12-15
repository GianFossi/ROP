// ------------------------------------------------------------------------------------------------------ //

#r "bin/Debug/net472/ROP.dll"

// ------------------------------------------------------------------------------------------------------ //

open ROP

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

let createRecord1 =
    validateNameNotEmpty
    >=> validateNameMaxLenghth          // >> bind validateNameMaxLenghth
    >=> validateSurnameNameNotEmpty     // >> bind validateSurnameNameNotEmpty
    >=> validateSurnameMaxLenghth       // >> bind validateSurnameMaxLenghth
    >=> Returns.switch toLowerCase              // >> map toLowerCase
    >=> Returns.switch ( Returns.tee updateDatabase )   // >> successTee updateDatabase
    >> Returns.log true

createRecord1 {Name="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"; Surname="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"} 

let createRecord2 =
    validateNameNotEmpty
    >> Returns.bind validateNameMaxLenghth
    >> Returns.bind validateSurnameNameNotEmpty
    >> Returns.bind validateSurnameMaxLenghth
    >> Returns.map toLowerCase
    >> Returns.successTee ( fun (input, msgs) -> updateDatabase input )
    >> Returns.log true

createRecord2 {Name="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"; Surname="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"} 

let createRecord3 record =
    validateNameNotEmpty 
    &&& validateNameMaxLenghth 
    &&& validateSurnameNameNotEmpty 
    &&& validateSurnameMaxLenghth
    >> Returns.map toLowerCase
    >> Returns.successTee ( fun (input, msgs) -> updateDatabase input )
    >> Returns.log record

createRecord3 true {Name=""; Surname=""} 
createRecord3 true {Name="Pippo"; Surname=""} 
createRecord3 true {Name=""; Surname="Franco"} 
createRecord3 true {Name="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"; Surname="djaskldjsajdaskjdlkasdjasildjaslkdjasjdals"} 
createRecord3 true {Name="Pippo"; Surname="Franco"} 
createRecord3 false {Name="Pippo"; Surname="Franco"} 

// ------------------------------------------------------------------------------------------------------ //
