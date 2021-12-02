// ------------------------------------------------------------------------------------------------------ //

#load "Returns.fs"

// ------------------------------------------------------------------------------------------------------ //

open System
open ROP

// ------------------------------------------------------------------------------------------------------ //

module CheckStrings = 

    /// Possible type of errors.
    type Messages =
    |   IsNullOrEmpty
    |   IsTooLong

    /// Validate is a String is Null or Empty.
    let validateIsNullOrEmpty (x:string) = 
        if String.IsNullOrEmpty x 
        then Returns.fail IsNullOrEmpty
        else Returns.ok x

    /// Validate the length of a String.
    let validateLength (maxLength:int) (x:string)  = 
        if x.Length > maxLength
        then Returns.fail IsTooLong
        else Returns.ok x

module CheckNumbers = 

    /// Possible type of errors.
    type Messages =
    |   IsZero
    |   IsNegative
    |   IsZeroOrNegative

    /// Validate is a number is zero.
    let validateIsZero (x:double) = 
        if Math.Abs( x ) <= 1.0E-6
        then Returns.fail IsZero
        else Returns.ok x

    /// Validate is a number is negative.
    let validateIsNegative (x:double) = 
        if x < 0.0 
        then Returns.fail IsNegative
        else Returns.ok x

    let validateIsZeroOrNegative (x:double) = 
        x |> ( validateIsNegative >=> validateIsZero )

// 1) Name should be a NON empty string.
// 2) Quantity should be an integer > 0.
type Ingredient = {Name: string; Quantity: double; UMeasure: string }

module Ingredient = 

    /// Possible type of errors for Ingredients.
    type Messages =
    |   NameIsNullOrEmpty
    |   NameIsToLong
    |   NameFormatIncorrect
    |   QuantityIsNullOrNegative

    /// Turn to lowercase.
    let validateNameFormat (x:string)  = 
        if x.ToLower() <> x
        then Returns.fail NameFormatIncorrect
        else Returns.ok x

    /// Validate Ingredient Parameter: Name.

    let validateIngredientName  =
        
        let convert err =  
            match err with
            | CheckStrings.Messages.IsNullOrEmpty -> NameIsNullOrEmpty
            | CheckStrings.Messages.IsTooLong -> NameIsToLong
        
        CheckStrings.validateIsNullOrEmpty 
        >=> CheckStrings.validateLength 10
        >> Returns.mapMessages convert
        &&& validateNameFormat

    /// Validate Ingredient Parameter: Quantity.
    let validateIngredientQuantity = 
        
        let convert err  =
            match err with 
            | CheckNumbers.Messages.IsZeroOrNegative
            | CheckNumbers.Messages.IsZero
            | CheckNumbers.Messages.IsNegative -> QuantityIsNullOrNegative

        CheckNumbers.validateIsZeroOrNegative
        >> Returns.mapMessages convert

    /// Correct the Ingredient format.
    let CorrectFormatIngredient (ingredient:Ingredient)  = 
        { ingredient with 
            Name = "*** " + ingredient.Name + " ***";
            UMeasure =  "[" + ingredient.UMeasure + "]" }

    /// Builder
    let  Create( name, quantity, measure ) =
        
        // Create the object.
        fun name quantity -> { Name = name; Quantity = quantity; UMeasure = measure } 
        // Check for 1st parameter: name.
        //<!> validateIngredientName name 
        <!> validateIngredientName name
        // Check for 2nd parameter: quantity.
        <*> validateIngredientQuantity quantity
        |> Returns.map CorrectFormatIngredient
        |> Returns.successTee (fun (v,msgs) -> printfn "%A" v )
        //|> defaultValue {Name="*** Water ***"; Quantity= 1.0; UMeasure="[lt]"}

Ingredient.Create( "",-10.0, "xxx")
Ingredient.Create( "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",-10.0, "xxx")
Ingredient.Create( "Farina",-10.0, "xxx")
Ingredient.Create( "farina",-10.0, "kg")
Ingredient.Create( "farina",10.0, "kg")

