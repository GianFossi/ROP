// ****************************************************************************************************** //
// ****************************************************************************************************** //
// Author: James Randall ( https://github.com/JamesRandall/AccidentalFish.FSharp.Validation ).
//
// REVISION HYSTORY:
//
// 21/11/2021: Start.
//
// ****************************************************************************************************** //
// ****************************************************************************************************** //

/// Contains error propagation functions and a computation expression builder for Railway-Oriented-Programming (ROP).
namespace ROP

// ****************************************************************************************************** //
// ****************************************************************************************************** //

open System
open System.Linq.Expressions

// ****************************************************************************************************** //
// ****************************************************************************************************** //

/// <summary>
/// A record/property validation DSL built on top of a <c>ValidatorBuilder&lt;'targetType&gt;</c> computation
/// expression. Validators are declared per-property (using quoted property-getter expressions so the
/// property name can be recovered at validator-construction time), and every validator is run against the
/// record when the resulting validation function is invoked, collecting ALL failing properties rather than
/// short-circuiting on the first one.
/// </summary>
module Validation =

    /// <summary>
    /// A single validation failure: the human-readable message, the (possibly dotted/indexed) property path
    /// it applies to, and a machine-readable error code identifying which validator raised it.
    /// </summary>
    type ValidationItem =
        {
            /// Human-readable description of why the validation failed.
            message: string
            /// The property path (e.g. "Name", "Address.City", "Items.[2].Sku") the failure relates to.
            property: string
            /// A machine-readable code identifying the validator that produced this failure (e.g. "isNotEmpty").
            errorCode: string
        }

    /// <summary>
    /// The outcome of running a validator (or a full validation pipeline): either <c>Ok</c> (nothing failed)
    /// or <c>Errors</c> carrying every <c>ValidationItem</c> that failed.
    /// </summary>
    type ValidationState =
        /// One or more validation failures occurred.
        | Errors of ValidationItem list
        /// Validation succeeded; there is nothing to report.
        | Ok

    /// <summary>
    /// Internal, type-erased representation of a single "validate this property" declaration inside a
    /// <c>ValidatorBuilder</c> computation expression.
    /// </summary>
    type PropertyValidatorConfig =
        {
            /// Determines whether this property's validators should run at all for a given record instance
            /// (used to implement the conditional "...When" operations); always-true for the unconditional ones.
            predicate: (obj -> bool)
            /// The compiled validator functions to run against the record (each closes over the property getter),
            /// each producing its own independent <c>ValidationState</c>.
            validators: (obj -> ValidationState) list
        }

    /// <summary>
    /// The result of unwrapping a union-typed property before validating its payload:
    /// either the unwrapped inner value should be validated, or validation of this property should be skipped entirely.
    /// </summary>
    type MatchResult<'propertyType> =
        /// The wrapped value was matched and should be validated as 'propertyType.
        | Unwrapped of 'propertyType
        /// The wrapped value did not match the expected case; skip validation for this property.
        | Ignore

    /// <summary>
    /// Extracts the property name (or path) referenced by a quoted property-getter expression, e.g.
    /// <c>fun (x:Person) -> x.Name</c> yields <c>"Name"</c>.
    /// </summary>
    /// <param name="expression">The quoted lambda expression that reads the target property off the record.</param>
    /// <returns>The property name/path with the leading parameter-qualifier stripped off, if any.</returns>
    let private getPropertyPath (expression:Expression<Func<'commandType, 'propertyType>>) =
        // The compiled expression tree's default ToString() renders something like "x.Name" (parameter name
        // followed by the member-access chain) - splitting on the first dot strips the parameter identifier,
        // leaving just the property path.
        let objectQualifiedExpression = expression.Body.ToString()
        let indexOfDot = objectQualifiedExpression.IndexOf('.')
        if indexOfDot = -1 then
            // No dot found (unexpected shape) - fall back to the raw rendered expression.
            objectQualifiedExpression
        else
            objectQualifiedExpression.Substring(indexOfDot+1)

    /// <summary>
    /// Wraps a plain property validator (string -> 'propertyType -> ValidationState) into a type-erased
    /// <c>obj -> ValidationState</c> function bound to a specific property, ready to be stored in a
    /// <c>PropertyValidatorConfig</c>.
    /// </summary>
    /// <param name="propertyGetterExpr">The quoted property-getter expression identifying which property to read and name.</param>
    /// <param name="validator">The validator function to invoke with the property name and the property's value.</param>
    let private packageValidator (propertyGetterExpr:Expression<Func<'targetType, 'propertyType>>) (validator:(string -> 'propertyType -> ValidationState)) =
        // Recover the property's display name from the quoted expression once, at validator-construction time.
        let propertyName = propertyGetterExpr |> getPropertyPath

        // Compile the expression tree into an actual delegate once (not per validated record) for fast repeated invocation.
        let propertyGetter = propertyGetterExpr.Compile()
        // The record is passed through as `obj` because PropertyValidatorConfig is type-erased; downcast it back
        // to 'targetType before reading the property off it.
        fun (value:obj) -> validator propertyName (propertyGetter.Invoke(value :?> 'targetType))

    /// <summary>
    /// Like <see cref="packageValidator"/>, but first unwraps a single-case union (e.g. a domain primitive such
    /// as <c>EmailAddress</c>) into its underlying primitive value before running the validator on it.
    /// </summary>
    /// <param name="propertyGetterExpr">The quoted property-getter expression identifying the wrapped property.</param>
    /// <param name="unwrapper">Extracts the underlying primitive value out of the single-case union.</param>
    /// <param name="validator">The validator function to invoke with the property name and the unwrapped value.</param>
    let private packageValidatorWithSingleCaseUnwrapper (propertyGetterExpr:Expression<Func<'targetType, 'wrappedPropertyType>>)
                                                        (unwrapper:'wrappedPropertyType -> 'propertyType)
                                                        (validator:(string -> 'propertyType -> ValidationState)) =
        let propertyName = propertyGetterExpr |> getPropertyPath

        let propertyGetter = propertyGetterExpr.Compile()
        // Read the wrapped value off the record, then unwrap it before handing it to the validator.
        fun (value:obj) -> validator propertyName (unwrapper (propertyGetter.Invoke(value :?> 'targetType)))

    /// <summary>
    /// Like <see cref="packageValidatorWithSingleCaseUnwrapper"/>, but for multi-case unions where the unwrap
    /// operation may legitimately fail to match (in which case validation of this property is skipped).
    /// </summary>
    /// <param name="propertyGetterExpr">The quoted property-getter expression identifying the wrapped property.</param>
    /// <param name="unwrapper">Attempts to unwrap the union case; returns <c>Ignore</c> if it doesn't apply.</param>
    /// <param name="validator">The validator function to invoke with the property name and the unwrapped value.</param>
    let private packageValidatorWithUnwrapper (propertyGetterExpr:Expression<Func<'targetType, 'wrappedPropertyType>>)
                                                        (unwrapper:'wrappedPropertyType -> MatchResult<'propertyType>)
                                                        (validator:(string -> 'propertyType -> ValidationState)) =
        let propertyName = propertyGetterExpr |> getPropertyPath

        let propertyGetter = propertyGetterExpr.Compile()
        fun (value:obj) ->
            // Read the wrapped value and attempt to unwrap it; only run the validator if the case matched.
            match (unwrapper (propertyGetter.Invoke(value :?> 'targetType))) with
            | Unwrapped unwrappedValue -> validator propertyName unwrappedValue
            | Ignore -> Ok

    /// <summary>
    /// Wraps a property validator for a required (<c>option</c>-typed) property: <c>None</c> is itself reported
    /// as a validation failure, and the validator only runs against the unwrapped value when it is <c>Some</c>.
    /// </summary>
    /// <param name="propertyGetterExpr">The quoted property-getter expression identifying the optional property.</param>
    /// <param name="validator">The validator function to invoke with the property name and the unwrapped value when present.</param>
    let private packageValidatorRequired (propertyGetterExpr:Expression<Func<'targetType, 'propertyType option>>) (validator:(string -> 'propertyType -> ValidationState)) =
        let propertyName = propertyGetterExpr |> getPropertyPath

        let propertyGetter = propertyGetterExpr.Compile()
        fun (value:obj) -> match propertyGetter.Invoke(value :?> 'targetType) with
                           | Some v -> validator propertyName v
                           // Missing (None) is itself the failure for a "required" property - the inner validator never runs.
                           | None -> Errors([{ errorCode="validatorRequired" ; property=propertyName ; message = "Option type is required" }])

    /// <summary>
    /// Wraps a property validator for an optional (<c>option</c>-typed) property: <c>None</c> is treated as a
    /// pass (nothing to validate), and the validator only runs against the unwrapped value when it is <c>Some</c>.
    /// </summary>
    /// <param name="propertyGetterExpr">The quoted property-getter expression identifying the optional property.</param>
    /// <param name="validator">The validator function to invoke with the property name and the unwrapped value when present.</param>
    let private packageValidatorUnrequired (propertyGetterExpr:Expression<Func<'targetType, 'propertyType option>>) (validator:(string -> 'propertyType -> ValidationState)) =
        let propertyName = propertyGetterExpr |> getPropertyPath

        let propertyGetter = propertyGetterExpr.Compile()
        fun (value:obj) -> match propertyGetter.Invoke(value :?> 'targetType) with
                           | Some v -> validator propertyName v
                           // Absent (None) is fine for an "unrequired" property - simply skip validation.
                           | None -> Ok

    /// <summary>
    /// Wraps a strongly-typed record predicate (used by the "...When" operations to decide whether a property's
    /// validators should run at all) into the type-erased <c>obj -> bool</c> shape stored in <c>PropertyValidatorConfig</c>.
    /// </summary>
    /// <param name="predicate">The strongly-typed predicate over the whole record.</param>
    let private packagePredicate (predicate:('targetType -> bool)) =
        fun (value:obj) -> predicate (value :?> 'targetType)

    /// <summary>
    /// Computation-expression builder that accumulates per-property validator declarations (via the
    /// <c>validate</c>/<c>validateWhen</c>/... custom operations) and, on <c>Run</c>, produces a single
    /// <c>'targetType -> ValidationState</c> function that runs every declared validator against a record
    /// and aggregates ALL resulting failures (it does not short-circuit on the first failing property).
    /// </summary>
    type ValidatorBuilder<'targetType>() =

        /// <summary>Starts the computation expression with an empty list of property-validator declarations.</summary>
        member __.Yield (_: unit) : PropertyValidatorConfig list   =
            []

        /// <summary>
        /// Finishes the computation expression, compiling the accumulated <c>PropertyValidatorConfig</c> list
        /// into the actual validation function that will be invoked against records.
        /// </summary>
        /// <param name="config">The list of property-validator declarations accumulated by the CE.</param>
        /// <returns>A function that validates a <c>'targetType</c> record, aggregating every failure found.</returns>
        member __.Run (config: PropertyValidatorConfig list) =
            let execValidation (record:'targetType) : ValidationState =
                let results =
                    config
                    // Skip any property block whose predicate says it doesn't apply to this record (e.g. validateWhen).
                    |> List.filter (fun p -> p.predicate(record :> obj))
                    // Run every remaining property's validators against the record, producing one ValidationState per validator.
                    |> List.collect (fun p -> p.validators |> List.map (fun v -> v(record)))
                    // Keep only the failing ones and flatten their ValidationItem lists into a single list.
                    |> List.collect (fun f -> match f with | Errors e -> e | _ -> [])
                match results with
                | [] -> Ok
                | _  -> Errors results
            execValidation

        /// <summary>
        /// Declares one or more validators for a property, unconditionally run for every record instance.
        /// </summary>
        /// <param name="config">The validator declarations accumulated so far.</param>
        /// <param name="propertyGetter">Quoted expression selecting the property to validate.</param>
        /// <param name="validatorFunctions">The validators to run against the selected property's value.</param>
        [<CustomOperation("validate")>]
        member this.validate (config: PropertyValidatorConfig list,
                              propertyGetter:Expression<Func<'targetType,'propertyType>>,
                              validatorFunctions:(string -> 'propertyType -> ValidationState) list) =
            config @ [
                {
                    // Always applies - no conditional gating.
                    predicate  = (fun _ -> true) |> packagePredicate
                    validators = validatorFunctions |> List.map (packageValidator propertyGetter)
                }
            ]

        /// <summary>
        /// Declares one or more validators for a property wrapped in a single-case union, unwrapping it to its
        /// underlying primitive value before validating.
        /// </summary>
        /// <param name="config">The validator declarations accumulated so far.</param>
        /// <param name="propertyGetter">Quoted expression selecting the wrapped property.</param>
        /// <param name="unwrapper">Extracts the underlying primitive value out of the single-case union.</param>
        /// <param name="validatorFunctions">The validators to run against the unwrapped value.</param>
        [<CustomOperation("validateSingleCaseUnion")>]
        member this.validateSingleCaseUnion(config: PropertyValidatorConfig list,
                                            propertyGetter:Expression<Func<'targetType,'wrappedPropertyType>>,
                                            (unwrapper:'wrappedPropertyType -> 'propertyType),
                                            validatorFunctions:(string -> 'propertyType -> ValidationState) list) =
            config @ [
                {
                    predicate  = (fun _ -> true) |> packagePredicate
                    validators = validatorFunctions |> List.map (packageValidatorWithSingleCaseUnwrapper propertyGetter unwrapper)
                }
            ]

        /// <summary>
        /// Declares one or more validators for a property wrapped in a multi-case union, unwrapping it (when the
        /// case matches) before validating; when the union case doesn't match, validation is skipped for this property.
        /// </summary>
        /// <param name="config">The validator declarations accumulated so far.</param>
        /// <param name="propertyGetter">Quoted expression selecting the wrapped property.</param>
        /// <param name="unwrapper">Attempts to unwrap the relevant case; returns <c>Ignore</c> when it doesn't apply.</param>
        /// <param name="validatorFunctions">The validators to run against the unwrapped value.</param>
        [<CustomOperation("validateUnion")>]
        member this.validateUnion(config: PropertyValidatorConfig list,
                                  propertyGetter:Expression<Func<'targetType,'wrappedPropertyType>>,
                                  (unwrapper:'wrappedPropertyType -> MatchResult<'propertyType>),
                                  validatorFunctions:(string -> 'propertyType -> ValidationState) list) =
            config @ [
                {
                    predicate  = (fun _ -> true) |> packagePredicate
                    validators = validatorFunctions |> List.map (packageValidatorWithUnwrapper propertyGetter unwrapper)
                }
            ]

        /// <summary>
        /// Declares one or more validators for a required (<c>option</c>-typed) property: missing (<c>None</c>)
        /// is itself reported as a failure, and the validators run against the value only when present.
        /// </summary>
        /// <param name="config">The validator declarations accumulated so far.</param>
        /// <param name="propertyGetter">Quoted expression selecting the optional property.</param>
        /// <param name="validatorFunctions">The validators to run against the unwrapped value when present.</param>
        [<CustomOperation("validateRequired")>]
        member this.validateRequired (config: PropertyValidatorConfig list,
                                      propertyGetter:Expression<Func<'targetType,'propertyType option>>,
                                      validatorFunctions:(string -> 'propertyType -> ValidationState) list) =
            config @ [
                {
                    predicate  = (fun _ -> true) |> packagePredicate
                    validators = validatorFunctions |> List.map (packageValidatorRequired propertyGetter)
                }
            ]

        /// <summary>
        /// Declares one or more validators for an optional (<c>option</c>-typed) property: missing (<c>None</c>)
        /// is treated as a pass, and the validators run against the value only when present.
        /// </summary>
        /// <param name="config">The validator declarations accumulated so far.</param>
        /// <param name="propertyGetter">Quoted expression selecting the optional property.</param>
        /// <param name="validatorFunctions">The validators to run against the unwrapped value when present.</param>
        [<CustomOperation("validateUnrequired")>]
        member this.validateUnrequired (config: PropertyValidatorConfig list,
                                        propertyGetter:Expression<Func<'targetType,'propertyType option>>,
                                        validatorFunctions:(string -> 'propertyType -> ValidationState) list) =
            config @ [
                {
                    predicate  = (fun _ -> true) |> packagePredicate
                    validators = validatorFunctions |> List.map (packageValidatorUnrequired propertyGetter)
                }
            ]

        /// <summary>
        /// Declares one or more validators for a property that only run when the given record-level predicate holds.
        /// </summary>
        /// <param name="config">The validator declarations accumulated so far.</param>
        /// <param name="predicate">Record-level condition gating whether this property's validators run at all.</param>
        /// <param name="propertyGetter">Quoted expression selecting the property to validate.</param>
        /// <param name="validatorFunctions">The validators to run against the selected property's value.</param>
        [<CustomOperation("validateWhen")>]
        member this.validateWhen (config: PropertyValidatorConfig list,
                                  predicate:('targetType -> bool),
                                  propertyGetter:Expression<Func<'targetType,'propertyType>>,
                                  validatorFunctions:(string -> 'propertyType -> ValidationState) list) =
            config @ [
                {
                    // Only applies when the caller-supplied record-level predicate holds.
                    predicate  = predicate |> packagePredicate
                    validators = validatorFunctions |> List.map (packageValidator propertyGetter)
                }
            ]

        /// <summary>
        /// Conditional variant of <see cref="validateRequired"/>: the required-option validators only run
        /// (and <c>None</c> is only treated as a failure) when the given record-level predicate holds.
        /// </summary>
        /// <param name="config">The validator declarations accumulated so far.</param>
        /// <param name="predicate">Record-level condition gating whether this property's validators run at all.</param>
        /// <param name="propertyGetter">Quoted expression selecting the optional property.</param>
        /// <param name="validatorFunctions">The validators to run against the unwrapped value when present.</param>
        [<CustomOperation("validateRequiredWhen")>]
        member this.validateRequiredWhen (config: PropertyValidatorConfig list,
                                          predicate:('targetType -> bool),
                                          propertyGetter:Expression<Func<'targetType,'propertyType option>>,
                                          validatorFunctions:(string -> 'propertyType -> ValidationState) list) =
            config @ [
                {
                    predicate  = predicate |> packagePredicate
                    validators = validatorFunctions |> List.map (packageValidatorRequired propertyGetter)
                }
            ]

        /// <summary>
        /// Conditional variant of <see cref="validateUnrequired"/>: the optional-option validators only run
        /// when the given record-level predicate holds.
        /// </summary>
        /// <param name="config">The validator declarations accumulated so far.</param>
        /// <param name="predicate">Record-level condition gating whether this property's validators run at all.</param>
        /// <param name="propertyGetter">Quoted expression selecting the optional property.</param>
        /// <param name="validatorFunctions">The validators to run against the unwrapped value when present.</param>
        [<CustomOperation("validateUnrequiredWhen")>]
        member this.validateUnrequiredWhen (config: PropertyValidatorConfig list,
                                            predicate:('targetType -> bool),
                                            propertyGetter:Expression<Func<'targetType,'propertyType option>>,
                                            validatorFunctions:(string -> 'propertyType -> ValidationState) list) =
            config @ [
                {
                    predicate  = predicate |> packagePredicate
                    validators = validatorFunctions |> List.map (packageValidatorUnrequired propertyGetter)
                }
            ]


    // General validators

    /// <summary>
    /// Creates a validator that succeeds only if the property value equals <paramref name="comparisonValue"/>.
    /// </summary>
    /// <param name="comparisonValue">The value the property must equal.</param>
    /// <returns>A <c>string -&gt; 'a -&gt; ValidationState</c> validator function (property name, then value).</returns>
    let isEqualTo comparisonValue =
        let comparator propertyName value =
            // Structural equality check against the closed-over comparison value.
            match value = comparisonValue with
            | true -> Ok
            | false -> Errors([{ message = sprintf "Must be equal to %O" comparisonValue; property = propertyName ; errorCode = "isEqualTo" }])
        comparator

    /// <summary>
    /// Creates a validator that succeeds only if the property value does NOT equal <paramref name="comparisonValue"/>.
    /// </summary>
    /// <param name="comparisonValue">The value the property must not equal.</param>
    /// <returns>A <c>string -&gt; 'a -&gt; ValidationState</c> validator function (property name, then value).</returns>
    let isNotEqualTo comparisonValue =
        let comparator propertyName value =
            match not (value = comparisonValue) with
            | true -> Ok
            | false -> Errors([{ message = sprintf "Must not be equal to %O" comparisonValue; property = propertyName ; errorCode = "isNotEqualTo" }])
        comparator

    /// <summary>
    /// Validator that succeeds only if the property value is not <c>null</c>.
    /// </summary>
    /// <param name="propertyName">The name/path of the property being validated (supplied by the CE plumbing).</param>
    /// <param name="value">The property value to check.</param>
    let isNotNull propertyName value =
        match isNull(value) with
        | true -> Errors([{ message = "Must not be null"; property = propertyName ; errorCode = "isNotNull" }])
        | false -> Ok

    // Numeric validators

    /// <summary>
    /// Creates a validator that succeeds only if the property value is greater than or equal to <paramref name="minValue"/>.
    /// </summary>
    /// <param name="minValue">The inclusive minimum allowed value.</param>
    /// <returns>A <c>string -&gt; 'a -&gt; ValidationState</c> validator function (property name, then value).</returns>
    let isGreaterThanOrEqualTo minValue =
        let comparator propertyName value =
            match value >= minValue with
            | true -> Ok
            | false -> Errors([{ message = sprintf "Must have a minimum value of %O" minValue; property = propertyName ; errorCode = "isGreaterThanOrEqualTo" }])
        comparator

    /// <summary>
    /// Creates a validator that succeeds only if the property value is strictly greater than <paramref name="minValue"/>.
    /// </summary>
    /// <param name="minValue">The exclusive minimum allowed value.</param>
    /// <returns>A <c>string -&gt; 'a -&gt; ValidationState</c> validator function (property name, then value).</returns>
    let isGreaterThan minValue =
        let comparator propertyName value =
            match value > minValue with
            | true -> Ok
            | false -> Errors([{ message = sprintf "Must be greater than %O" minValue; property = propertyName ; errorCode = "isGreaterThan" }])
        comparator

    /// <summary>
    /// Creates a validator that succeeds only if the property value is less than or equal to <paramref name="maxValue"/>.
    /// </summary>
    /// <param name="maxValue">The inclusive maximum allowed value.</param>
    /// <returns>A <c>string -&gt; 'a -&gt; ValidationState</c> validator function (property name, then value).</returns>
    let isLessThanOrEqualTo maxValue =
        let comparator propertyName value =
            match value <= maxValue with
            | true -> Ok
            | false -> Errors([{ message = sprintf "Must have a maximum value of %O" maxValue; property = propertyName ; errorCode = "isLessThanOrEqualTo" }])
        comparator

    /// <summary>
    /// Creates a validator that succeeds only if the property value is strictly less than <paramref name="lessThanValue"/>.
    /// </summary>
    /// <param name="lessThanValue">The exclusive maximum allowed value.</param>
    /// <returns>A <c>string -&gt; 'a -&gt; ValidationState</c> validator function (property name, then value).</returns>
    let isLessThan lessThanValue =
        let comparator propertyName value =
            match value < lessThanValue with
            | true -> Ok
            | false -> Errors([{ message = sprintf "Must be less than %O" lessThanValue; property = propertyName ; errorCode = "isLessThan" }])
        comparator

    // Collection validators

    /// <summary>
    /// Validator that succeeds only if the sequence (this also applies to strings) is neither <c>null</c> nor empty.
    /// </summary>
    /// <param name="propertyName">The name/path of the property being validated (supplied by the CE plumbing).</param>
    /// <param name="value">The sequence to check.</param>
    let isNotEmpty propertyName (value:seq<'item>) = // this also applies to strings
        // Seq.isEmpty short-circuits on the first element instead of fully enumerating (Seq.length),
        // which is both faster for large sequences and correct for infinite/lazy ones.
        if isNull(value) then
            Errors([{ message = "Must not be null"; property = propertyName ; errorCode = "isNotEmpty" }])
        elif Seq.isEmpty value then
            Errors([{ message = "Must not be empty"; property = propertyName ; errorCode = "isNotEmpty" }])
        else
            Ok

    /// <summary>
    /// Validator that succeeds only if the sequence (this also applies to strings) is <c>null</c> or empty.
    /// </summary>
    /// <param name="propertyName">The name/path of the property being validated (supplied by the CE plumbing).</param>
    /// <param name="value">The sequence to check.</param>
    let isEmpty propertyName (value:seq<'item>) = // this also applies to strings
        if not ( isNull(value) || Seq.isEmpty value) then
            Errors([{ message = "Must be empty"; property = propertyName ; errorCode = "isEmpty" }])
        else
            Ok

    /// <summary>
    /// Creates a validator that runs <paramref name="validatorFunc"/> against every element of a sequence
    /// property, aggregating all per-item failures with their property path prefixed by the item's index
    /// (e.g. <c>"Items.[2].Sku"</c>).
    /// </summary>
    /// <param name="validatorFunc">The validator to apply to each item in the sequence.</param>
    /// <returns>A <c>string -&gt; seq&lt;'a&gt; -&gt; ValidationState</c> validator function (property name, then sequence).</returns>
    let eachItemWith (validatorFunc:('validatorTargetType->ValidationState)) =
        // Rewrites a single item-level ValidationItem's property to be prefixed with the root property name
        // and the item's index, e.g. "Sku" at index 2 under "Items" becomes "Items.[2].Sku".
        let buildIndexedPropertyName rootPropertyName index error =
            { error with property = sprintf "%s.[%d].%s" rootPropertyName index error.property }

        let comparator propertyName (items:seq<'validatorTargetType>) =
            // Single pass with a mutable accumulator, instead of building and concatenating a list-of-lists per item.
            let failures = ResizeArray()
            items
            |> Seq.iteri (fun index item ->
                // Validate each item independently; on failure, re-index its errors and collect them.
                match validatorFunc item with
                | Ok -> ()
                | Errors errors ->
                    for error in errors do
                        failures.Add(buildIndexedPropertyName propertyName index error))
            if failures.Count = 0 then Ok
            else Errors(List.ofSeq failures)
        comparator

    /// <summary>
    /// Creates a validator that succeeds only if the sequence has exactly <paramref name="length"/> elements.
    /// </summary>
    /// <param name="length">The required element count.</param>
    /// <returns>A <c>string -&gt; seq&lt;'a&gt; -&gt; ValidationState</c> validator function (property name, then sequence).</returns>
    let hasLengthOf length =
        let comparator propertyName (value:seq<'item>) =
            // Exact-count checks genuinely need the full length, so the sequence must be fully enumerated here.
            match (Seq.length value) = length with
            | true -> Ok
            | false -> Errors([{ message = sprintf "Must have a length of %O" length; property = propertyName ; errorCode = "hasLengthOf" }])
        comparator

    /// <summary>
    /// Creates a validator that succeeds only if the sequence has at least <paramref name="length"/> elements.
    /// </summary>
    /// <param name="length">The minimum required element count.</param>
    /// <returns>A <c>string -&gt; seq&lt;'a&gt; -&gt; ValidationState</c> validator function (property name, then sequence).</returns>
    let hasMinLengthOf length =
        let comparator propertyName (value:seq<'item>) =
            match (Seq.length value) >= length with
            | true -> Ok
            | false -> Errors([{ message = sprintf "Must have a length no less than %O" length; property = propertyName ; errorCode = "hasMinLengthOf" }])
        comparator

    /// <summary>
    /// Creates a validator that succeeds only if the sequence has at most <paramref name="length"/> elements.
    /// </summary>
    /// <param name="length">The maximum allowed element count.</param>
    /// <returns>A <c>string -&gt; seq&lt;'a&gt; -&gt; ValidationState</c> validator function (property name, then sequence).</returns>
    let hasMaxLengthOf length =
        let comparator propertyName (value:seq<'item>) =
            match (Seq.length value) <= length with
            | true -> Ok
            | false -> Errors([{ message = sprintf "Must have a length no greater than %O" length; property = propertyName ; errorCode = "hasMaxLengthOf" }])
        comparator

    // String validators

    /// <summary>
    /// Validator that succeeds only if the string is neither <c>null</c>, empty, nor made up entirely of whitespace.
    /// </summary>
    /// <param name="propertyName">The name/path of the property being validated (supplied by the CE plumbing).</param>
    /// <param name="value">The string to check.</param>
    let isNotEmptyOrWhitespace propertyName (value:string) =
        // Checked from most to least specific so the error message pinpoints exactly which condition failed.
        if isNull(value) then
            Errors([{ message = "Must not be null"; property = propertyName ; errorCode = "isNotEmptyOrWhitespace" }])
        elif String.IsNullOrEmpty(value) then
            Errors([{ message = "Must not be empty"; property = propertyName ; errorCode = "isNotEmptyOrWhitespace" }])
        elif String.IsNullOrWhiteSpace(value) then
            Errors([{ message = "Must not be whitespace"; property = propertyName ; errorCode = "isNotEmptyOrWhitespace" }])
        else
            Ok

    // Function

    /// <summary>
    /// Adapts an arbitrary custom validator function (that ignores the property name) into the standard
    /// <c>string -&gt; 'a -&gt; ValidationState</c> validator shape expected by the CE.
    /// </summary>
    /// <param name="validatorFunc">The custom validation logic to run against the property's value.</param>
    /// <returns>A <c>string -&gt; 'a -&gt; ValidationState</c> validator function (property name, then value).</returns>
    let withFunction (validatorFunc:('validatorTargetType->ValidationState)) =
        let comparator _ (value:'validatorTargetType) =
            // The property name is intentionally discarded (`_`); this validator only cares about the value.
            validatorFunc value
        comparator


    // Sub-validators - very similar to functions but prefix the property name with the current property path

    /// <summary>
    /// Runs a nested validator (typically one produced by <see cref="createValidatorFor"/> for a child record
    /// type) against a value, and if it fails, prefixes every resulting error's property path with the current
    /// property path so nested failures read as e.g. <c>"Address.City"</c> rather than just <c>"City"</c>.
    /// </summary>
    /// <param name="value">The (child) value to validate.</param>
    /// <param name="propertyPath">The property path of the parent property under which the child is nested.</param>
    /// <param name="validatorFunc">The child validator to run.</param>
    let private runChildValidator value propertyPath (validatorFunc:('validatorTargetType->ValidationState)) =
        // Rewrites a single child-level ValidationItem's property to be prefixed with the parent property path.
        let buildPrefixedPropertyName error =
            { error with property = sprintf "%s.%s" propertyPath error.property }
        match (validatorFunc value) with
        | Ok -> Ok
        | Errors e -> Errors(e |> Seq.map buildPrefixedPropertyName |> Seq.toList)

    /// <summary>
    /// Creates a sub-validator that only runs a nested validator when <paramref name="predicate"/> holds for
    /// the value; otherwise it is treated as a pass. Resulting errors have their property path prefixed with
    /// the current property path.
    /// </summary>
    /// <param name="predicate">Condition gating whether the nested validator runs at all.</param>
    /// <param name="validatorFunc">The nested validator to run when the predicate holds.</param>
    /// <returns>A <c>string -&gt; 'a -&gt; ValidationState</c> validator function (property path, then value).</returns>
    let withValidatorWhen (predicate:'validatorTargetType->bool) (validatorFunc:('validatorTargetType->ValidationState)) =
        let comparator propertyPath (value:'validatorTargetType) =
            match predicate(value) with
            | true -> runChildValidator value propertyPath validatorFunc
            | false -> Ok
        comparator

    /// <summary>
    /// Creates a sub-validator that unconditionally runs a nested validator (typically for a child record
    /// property), prefixing any resulting errors' property paths with the current property path.
    /// </summary>
    /// <param name="validatorFunc">The nested validator to run.</param>
    /// <returns>A <c>string -&gt; 'a -&gt; ValidationState</c> validator function (property path, then value).</returns>
    let withValidator (validatorFunc:('validatorTargetType->ValidationState)) =
        let comparator propertyPath (value:'validatorTargetType) =
            runChildValidator value propertyPath validatorFunc
        comparator

    /// <summary>
    /// Entry point of the validation DSL: creates a new <c>ValidatorBuilder&lt;'targetType&gt;</c> computation
    /// expression used to declare per-property validators for <c>'targetType</c>, e.g.
    /// <c>createValidatorFor&lt;Person&gt;() { validate (fun p -> p.Name) [isNotEmptyOrWhitespace] }</c>.
    /// </summary>
    /// <typeparam name="targetType">The record/class type the resulting validator will validate.</typeparam>
    /// <returns>A fresh <c>ValidatorBuilder&lt;'targetType&gt;</c> ready to accept <c>validate</c>/... declarations.</returns>
    let createValidatorFor<'targetType>() =
        ValidatorBuilder<'targetType>()
