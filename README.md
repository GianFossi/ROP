# ROP (Railway-Oriented Programming in F#)

This repository contains an F# library that implements a Railway-Oriented Programming model around a custom `Returns<'TSuccess,'TMessage>` type, plus helper modules, validation utilities, examples, and an Expecto test suite.

## What this codebase provides

- A custom **result container** with three practical states:
  - `Success(value, warnings)`
  - `Failure(errors)`
- Functional combinators to compose validations and business rules:
  - sequential (`>>=`, `>=>`, `<=<`)
  - applicative (`<!>`, `<*>`)
  - parallel validation (`&&&`, `and!` in computation expression)
- Utility extensions for standard F# types:
  - `Result` extensions
  - `Choice` extensions
  - `Option` extensions
- A separate `Validation` module for record/property-level validators.

---

## Technology stack

- **Language**: F#
- **Runtime**: .NET 8 (`TargetFramework: net8.0`)
- **Library project**: `/tmp/workspace/GianFossi/ROP/ROP/ROP.fsproj`
- **Tests**: Expecto (`/tmp/workspace/GianFossi/ROP/Test/Test.fsproj`)
- **Solution**: `/tmp/workspace/GianFossi/ROP/ROP.sln`

---

## Repository structure

```text
/tmp/workspace/GianFossi/ROP
├── ROP.sln
├── README.md
├── ROP/
│   ├── ROP.fsproj
│   ├── Returns.fs
│   ├── Validation.fs
│   ├── Result.Extension.fs
│   ├── Choice.Extension.fs
│   ├── Option.Extension.fs
│   ├── Example.Result.fsx
│   ├── Examples.Returns.General.fsx
│   ├── Examples.Returns.Validation.Series.fsx
│   ├── Examples.Returns.Validation.Parallel.1.fsx
│   ├── Examples.Returns.Validation.Parallel.2.fsx
│   └── Examples.Returns.HeatExchanger.fsx
├── Test/
│   ├── Test.fsproj
│   └── Program.fs
└── Setup/
    └── Setup.vdproj
```

---

## How the core library is organized

### 1) `Returns.fs` (core)

`Returns.fs` is the central file. It defines:

- `type Returns<'TSuccess,'TMessage> = Success of ... | Failure of ...`
- Module `Returns` with constructors, transformations, composition, and utility helpers.
- Operators under `Returns.Operators`.
- `ReturnsBuilder` computation expression (`returns { ... }`) with support for `and!` parallel binding.

#### Key functional groups inside `Returns`

- Creation: `ok`, `warn`, `warnmany`, `fail`, `failmany`
- Classification: `isSucceeded`, `isFailure`, `hasWarnings`
- Conversion: `toOption/ofOption`, `toChoice/ofChoice`, `toResult/ofResult`
- Composition:
  - Sequential: `bind`, `compose`, `>>=`, `>=>`, `<=<`
  - Applicative: `apply`, `map`, `map2`, `map3`, `map4`, `<!>`, `<*>`
  - Parallel: `plus`, `&&&`, `validateAll`
- Message handling: `jointMessages`, `mapMessages`, `mapWarnings`, `mapErrors`, `warnIf`
- Collection helpers: `traverseList`, `sequenceList`, `partition`, `zip`, `fold`

---

### 2) `Validation.fs` (validator DSL)

`Validation.fs` defines a validator builder for richer object/record validation:

- `ValidationItem`, `ValidationState` (`Ok | Errors`)
- Validator combinators for scalar, optional, collection, and nested values
- `createValidatorFor<'T>() { ... }` DSL

Example capabilities include:

- `validate`, `validateWhen`
- `validateRequired`, `validateUnrequired`
- `validateSingleCaseUnion`, `validateUnion`
- Primitive validators like `isGreaterThan`, `isNotEmpty`, `isNotEmptyOrWhitespace`

---

### 3) Extension modules

- `Result.Extension.fs`: additional helpers for `Result<'T,'E>` (`defaultValue`, `either`, `apply`, `mapError`, `toChoice`, etc.)
- `Choice.Extension.fs`: helpers for `Choice<'T,'E>` (`apply`, `map2`, `bind`, `bindChoice2Of2`, `either`, etc.)
- `Option.Extension.fs`: helpers for `option<'T>` (`apply`, `zip`, `toResultWith`, `protect`, etc.)

These modules are independent utility layers and can be used outside the `Returns` workflow.

---

## Detailed usage examples from this repository

### A) Sequential validation (short-circuit with warning propagation)

From `Examples.Returns.General.fsx`:

- `checkIsEven`, `checkIsNegative`, `checkIsLargerThan10` each return `Returns<int, Messages>`
- They are composed sequentially with:
  - `Returns.ok 9 >>= checkIsEven >>= checkIsNegative >>= checkIsLargerThan10`
  - or function composition `checkIsEven >=> checkIsNegative >=> checkIsLargerThan10`

This pattern stops at failure while keeping accumulated warnings/errors already produced.

### B) Parallel validation (accumulate failures)

From `Examples.Returns.Validation.Series.fsx` and `Returns.fs` operator `&&&`:

- `validate1 &&& validate2 &&& validate3`
- All validators run against the same input
- If multiple validations fail, their error lists are concatenated

Useful for form-style validation where you want to report all issues at once.

### C) Applicative construction with `<*>`

From `Examples.Returns.Validation.Parallel.1.fsx`:

- `create x y = fun a b -> (a,b) <!> check1 x <*> check2 y`
- Both arguments are validated independently
- You get either a constructed value or merged failures

This is ideal for validating constructor arguments in parallel.

### D) Realistic domain example (ingredient checks)

From `Examples.Returns.HeatExchanger.fsx`:

- Domain type: `Ingredient = { Name; Quantity; UMeasure }`
- Name and quantity validators are composed from smaller modules (`CheckStrings`, `CheckNumbers`)
- Errors are remapped to domain-specific messages
- Final constructor `Ingredient.Create(...)` combines validated inputs and applies formatting

This demonstrates layering generic validators into domain-specific business rules.

---

## Test organization

Tests are in `/tmp/workspace/GianFossi/ROP/Test/Program.fs` and grouped by behavior:

- `Returns - Creation`, `Predicates`, `Conversions`, `bind`, `apply`, `map`, `&&&`, etc.
- `ReturnsBuilder - computation expression` and `and! parallel binding`
- `Result.Extension`, `Choice.Extension`, `Option.Extension`
- `Validation - Basic Validators`, `Collection Validators`, `ValidatorBuilder`
- `Integration - End-to-end`

The test entry point composes all test lists into a single `All Tests` suite.

---

## Build and run

From repository root:

```bash
dotnet build /tmp/workspace/GianFossi/ROP/ROP.sln
```

To execute the Expecto test executable:

```bash
dotnet run --project /tmp/workspace/GianFossi/ROP/Test/Test.fsproj
```

---

## Where to start reading the code

1. `/tmp/workspace/GianFossi/ROP/ROP/Returns.fs` — core type + operators + CE builder
2. `/tmp/workspace/GianFossi/ROP/ROP/Validation.fs` — validator DSL
3. `/tmp/workspace/GianFossi/ROP/ROP/Examples.Returns.General.fsx` — quick mental model
4. `/tmp/workspace/GianFossi/ROP/Test/Program.fs` — behavior coverage and edge cases

