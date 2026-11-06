# Equipment

Modello F# di apparecchio a pressione (grafo di componenti e porte) con calcolo di peso, baricentro,
camere di fluido e ingombri. Vedi [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) per architettura e roadmap.

```bash
dotnet build Equipment.sln
dotnet run --project tests/Equipment.Tests/Equipment.Tests.fsproj --framework net8.0
dotnet run --project tests/Equipment.Tests/Equipment.Tests.fsproj --framework net10.0
```

```fsharp
open ROP
open Equipment.Core

let get r = match r with Success (v, _) -> v | Failure e -> failwithf "%A" e
let steel = MaterialRef.carbonSteel

let eq =
    Equipment.empty "Demo"
    |> Equipment.addComponent (get (Component.create "S-1" (Shape.cylindricalShell 1000.0 10.0 3000.0) steel)) |> get
    |> Equipment.addComponent (get (Component.create "H-1" (Shape.kloepperHead 1000.0 10.0 40.0) steel)) |> get
    |> Equipment.connectTags "S-1" "End2" "H-1" "Weld" Frame.identity |> get |> fst

let props = Properties.ofEquipment eq |> get
printfn "mass %.1f kg, volume %.0f L, envelope %A" props.SuppliedMass (props.Chambers.Head.Volume / 1e6) props.Envelope
```
