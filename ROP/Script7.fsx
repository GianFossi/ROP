
// ------------------------------------------------------------------------------------------------------ //

#load "Result.Extension.fs"

// ------------------------------------------------------------------------------------------------------ //

open ROP
open System
open System.Linq.Expressions

// ------------------------------------------------------------------------------------------------------ //

type Cylinder ( id: double, od: double, l: double ) =
    
    member this.id=id
    member this.od=od
    member this.l=l

// ------------------------------------------------------------------------------------------------------ //

type CylinderError = IsNegative of string*double | InvalidDiameters of double*double 

// ------------------------------------------------------------------------------------------------------ //

let builder ( insideDiameter: double, outsideDiameter: double, length: double ) =
    
    result {
        
        let! l = length 
                |> ROP.Result.Check.double.islessThenOrEqualTo 0.0 ( IsNegative ("length", length) )

        let! id = insideDiameter 
                |> ROP.Result.Check.double.islessThenOrEqualTo 0.0 ( IsNegative ("insideDiameter", insideDiameter) )

        let! od = outsideDiameter 
                |> ROP.Result.Check.double.islessThenOrEqualTo 0.0 ( IsNegative ("outsideDiameter", insideDiameter) )

        let isIDlessThanOD = 
                outsideDiameter |> ROP.Result.Check.double.islessThenOrEqualTo id ( IsNegative ("outsideDiameter", insideDiameter) )
        
        let cyl = new Cylinder(id, od, l )
        
        return cyl
    }

builder (0.0, 0.0, 0.0)
