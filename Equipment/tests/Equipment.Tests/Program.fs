module Program

open Expecto

[<EntryPoint>]
let main argv =
    let all =
        testList "Equipment" [
            GeometryTests.tests
            ShapeTests.tests
            EquipmentTests.tests
            ExchangerTests.tests
        ]
    runTestsWithCLIArgs [] argv all
