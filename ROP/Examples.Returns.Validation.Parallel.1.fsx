
// ------------------------------------------------------------------------------------------------------ //

#load "Returns.fs"

// ------------------------------------------------------------------------------------------------------ //

open ROP

// ------------------------------------------------------------------------------------------------------ //

// val check1 : x:int -> Returns<int,string>
let check1 x =
    if x >= 0
    then Returns.ok x
    else Returns.fail "x<0!"

// val check2 : y:int -> Returns<int,string>
let check2 y =
    if y >= 0
    then Returns.ok y
    else Returns.fail "y<0!"

// val create : x:int -> y:int -> Returns<(int * int),string>
let create x y = 
    fun a b -> (a,b) // Creator - val it : a:'a -> b:'b -> 'a * 'b
    <!> check1 x // Check the First Parameter x only.
    <*> check2 y // Check the Second Parameter y only.

create +10 +20 // Success ((10, 20), [])

create -10 +20 // Failure ["x<0!"]

create +10 -20 // Failure ["y<0!"]

create -10 -20 // Failure ["x<0!"; "y<0!"]

// ------------------------------------------------------------------------------------------------------ //

// val create : x:int -> y:int -> Returns<(int * int),string>
let createInv x y = 
    fun a b -> (a,b) // Creator - val it : a:'a -> b:'b -> 'a * 'b
    <!> check2 y // Check the First Parameter y only.
    <*> check1 x // Check the Second Parameter x only.

createInv +10 +20 // Success ((20, 10), [])

// ------------------------------------------------------------------------------------------------------ //
