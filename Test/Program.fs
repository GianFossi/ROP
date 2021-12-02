// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
// See the 'F# Tutorial' project for more help.

open ROP.Validation

// Define a function to construct a message to print
let from whom =
    sprintf "from %s" whom

// ------------------------------------------------------------------------------------------------------ //

type Cylinder( id, od, l ) =
    
    member this.id=id
    member this.od=od
    member this.l=l

    static member builder( id, od, l ) =

        let validateCylinder = createValidatorFor<Cylinder>() {
                validate (fun o -> o.id) [
                    isGreaterThan 0.0
                ]
                validate (fun o -> o.od) [
                    isGreaterThan 0.0
                ]
                validate (fun o -> o.l) [
                    isGreaterThan 0.0
                ]
                validate (fun o -> o) [
                    withFunction (fun o ->
                        if o.od <= o.id
                        then Errors ([
                            {
                                errorCode = "InvalidGeometry"
                                message = "The Outside Diameter is less than or equal to the Inside Diameter."
                                property = "od"
                            }
                        ])
                        else Ok
                    )
                ]
                    
            }

        ( new Cylinder(id, od, l) ) |> validateCylinder

// ------------------------------------------------------------------------------------------------------ //

[<EntryPoint>]
let main argv =
    
    let c = Cylinder.builder( 100., 200., 1000.) // ValidationState = Ok
    
    0 // return an integer exit code
