
// ------------------------------------------------------------------------------------------------------ //

#load "Validation.fs"

// ------------------------------------------------------------------------------------------------------ //

open ROP.Validation

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


Cylinder.builder( 100., 200., 1000.) // ValidationState = Ok

Cylinder.builder( 0., 200., 1000.) // ValidationState = Errors [{ message = "Must be greater than 0" property = "id" errorCode = "isGreaterThan" }]

Cylinder.builder( 100., 50., 1000.) // ValidationState = Errors [{ message = "The Outside Diameter is less than or equal to the Inside Diameter." property = "0d" errorCode = "Invalid Geometry" }]

Cylinder.builder( 0., 0., 0.) // ValidationState = Errors [{ message = "The Outside Diameter is less than or equal to the Inside Diameter." property = "0d" errorCode = "Invalid Geometry" }]

