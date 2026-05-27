module Tests

open Expecto
open ROP
open ROP.Validation
open Returns.Operators

// ============================================================
// Helpers
// ============================================================

let isSuccess r = match r with | Success _ -> true | _ -> false
let isFailure r = match r with | Failure _ -> true | _ -> false
let successValue r = match r with | Success (v,_) -> v | _ -> failwith "Expected Success"
let failureMessages r = match r with | Failure msgs -> msgs | _ -> failwith "Expected Failure"

// ============================================================
// Returns module tests
// ============================================================

let returnsCreationTests =
    testList "Returns - Creation" [

        test "ok wraps value in Success with no messages" {
            let r = Returns.ok 42
            Expect.isTrue (isSuccess r) "should be Success"
            Expect.equal (successValue r) 42 "value should match"
        }

        test "warn wraps value in Success with one warning" {
            let r = Returns.warn "w" 99
            match r with
            | Success (v, msgs) ->
                Expect.equal v 99 "value should match"
                Expect.equal msgs ["w"] "should have one warning"
            | _ -> failtest "Expected Success"
        }

        test "warnmany wraps value in Success with multiple warnings" {
            let r = Returns.warnmany ["w1";"w2"] 7
            match r with
            | Success (v, msgs) ->
                Expect.equal v 7 "value should match"
                Expect.equal msgs ["w1";"w2"] "should have two warnings"
            | _ -> failtest "Expected Success"
        }

        test "fail wraps single message in Failure" {
            let r : Returns<int,string> = Returns.fail "err"
            Expect.isTrue (isFailure r) "should be Failure"
            Expect.equal (failureMessages r) ["err"] "should have one error"
        }

        test "failmany wraps multiple messages in Failure" {
            let r : Returns<int,string> = Returns.failmany ["e1";"e2"]
            Expect.equal (failureMessages r) ["e1";"e2"] "should have two errors"
        }
    ]

let returnsPredicateTests =
    testList "Returns - Predicates" [

        test "isSucceeded returns true for Success" {
            Expect.isTrue (Returns.isSucceeded (Returns.ok 1)) "should be succeeded"
        }

        test "isSucceeded returns false for Failure" {
            Expect.isFalse (Returns.isSucceeded (Returns.fail "e")) "should not be succeeded"
        }

        test "isFailure returns true for Failure" {
            Expect.isTrue (Returns.isFailure (Returns.fail "e")) "should be failure"
        }

        test "isFailure returns false for Success" {
            Expect.isFalse (Returns.isFailure (Returns.ok 1)) "should not be failure"
        }

        test "hasWarnings returns true when warnings present" {
            Expect.isTrue (Returns.hasWarnings (Returns.warn "w" 1)) "should have warnings"
        }

        test "hasWarnings returns false for clean success" {
            Expect.isFalse (Returns.hasWarnings (Returns.ok 1)) "should not have warnings"
        }

        test "hasWarnings returns false for Failure" {
            Expect.isFalse (Returns.hasWarnings (Returns.fail "e")) "should not have warnings"
        }
    ]

let returnsDefaultTests =
    testList "Returns - Default" [

        test "defaultValue returns value on Success" {
            let r = Returns.defaultValue 0 (Returns.ok 42)
            Expect.equal r 42 "should return Success value"
        }

        test "defaultValue returns default on Failure" {
            let r = Returns.defaultValue 99 (Returns.fail "e")
            Expect.equal r 99 "should return default value"
        }

        test "defaultWith returns value on Success" {
            let r = Returns.defaultWith (fun _ -> 0) (Returns.ok 42)
            Expect.equal r 42 "should return Success value"
        }

        test "defaultWith applies function on Failure" {
            let r = Returns.defaultWith (fun msgs -> msgs.Length) (Returns.failmany ["a";"b"])
            Expect.equal r 2 "should apply compensation function"
        }
    ]

let returnsValueOrFailwithTests =
    testList "Returns - valueOrFailwith" [

        test "valueOrFailwith returns value on Success" {
            let r = Returns.valueOrFailwith (Returns.ok 42)
            Expect.equal r 42 "should return value"
        }

        test "valueOrFailwith throws on Failure" {
            Expect.throws (fun () -> Returns.valueOrFailwith (Returns.fail "err") |> ignore)
                "should throw on failure"
        }
    ]

let returnsFailOnWarningsTests =
    testList "Returns - failOnWarnings" [

        test "failOnWarnings converts Success with warnings to Failure" {
            let r = Returns.warn "w" 1 |> Returns.failOnWarnings
            Expect.isTrue (isFailure r) "should be Failure"
            Expect.equal (failureMessages r) ["w"] "should carry warning as error"
        }

        test "failOnWarnings leaves clean Success (no warnings) unchanged" {
            let r = Returns.ok 1 |> Returns.failOnWarnings
            Expect.isTrue (isSuccess r) "clean Success should pass through"
            Expect.equal (successValue r) 1 "value unchanged"
        }

        test "failOnWarnings leaves Failure unchanged" {
            let r = Returns.fail "e" |> Returns.failOnWarnings
            Expect.isTrue (isFailure r) "should remain Failure"
            Expect.equal (failureMessages r) ["e"] "messages unchanged"
        }
    ]

let returnsTryCatchTests =
    testList "Returns - tryCatch" [

        test "tryCatch returns Success when function succeeds" {
            let r = Returns.tryCatch (fun x -> x + 1) 5
            match r with
            | Success (v, []) -> Expect.equal v 6 "should return 6"
            | _ -> failtest "Expected Success"
        }

        test "tryCatch returns Failure when function throws" {
            let r = Returns.tryCatch (fun _ -> failwith "boom") 0
            Expect.isTrue (isFailure r) "should be Failure"
        }
    ]

let returnsConversionTests =
    testList "Returns - Conversions" [

        test "toOption returns Some for Success" {
            let r = Returns.toOption (Returns.warn "w" 42)
            Expect.equal r (Some (42, ["w"])) "should return Some"
        }

        test "toOption returns None for Failure" {
            let r = Returns.toOption (Returns.fail "e")
            Expect.equal r None "should return None"
        }

        test "ofOption returns Success for Some" {
            let r = Returns.ofOption "err" (Some 5)
            Expect.equal (successValue r) 5 "should return Success"
        }

        test "ofOption returns Failure for None" {
            let r : Returns<int,string> = Returns.ofOption "err" None
            Expect.equal (failureMessages r) ["err"] "should return Failure"
        }

        test "toChoice returns Choice1Of2 for Success" {
            let r = Returns.toChoice (Returns.warn "w" 7)
            match r with
            | Choice1Of2 (v, msgs) ->
                Expect.equal v 7 "value should be 7"
                Expect.equal msgs ["w"] "should carry warnings"
            | _ -> failtest "Expected Choice1Of2"
        }

        test "toChoice returns Choice2Of2 for Failure" {
            let r = Returns.toChoice (Returns.fail "e")
            match r with
            | Choice2Of2 msgs -> Expect.equal msgs ["e"] "should carry errors"
            | _ -> failtest "Expected Choice2Of2"
        }

        test "ofChoice wraps Choice1Of2 into Success" {
            let r = Returns.ofChoice (Choice1Of2 10)
            Expect.equal (successValue r) 10 "should be Success with 10"
        }

        test "ofChoice wraps Choice2Of2 into Failure" {
            let r : Returns<int,string> = Returns.ofChoice (Choice2Of2 ["e1";"e2"])
            Expect.equal (failureMessages r) ["e1";"e2"] "should be Failure"
        }

        test "toResult returns Ok for Success" {
            let r = Returns.toResult (Returns.warn "w" 3)
            match r with
            | Result.Ok (v, msgs) ->
                Expect.equal v 3 "value matches"
                Expect.equal msgs ["w"] "warnings propagated"
            | _ -> failtest "Expected Ok"
        }

        test "toResult returns Error for Failure" {
            let r = Returns.toResult (Returns.fail "e")
            match r with
            | Result.Error msgs -> Expect.equal msgs ["e"] "errors propagated"
            | _ -> failtest "Expected Error"
        }

        test "ofResult wraps Ok into Success" {
            let r = Returns.ofResult (Result.Ok 5)
            Expect.equal (successValue r) 5 "should be Success"
        }

        test "ofResult wraps Error into Failure" {
            let r : Returns<int,string> = Returns.ofResult (Result.Error ["e"])
            Expect.equal (failureMessages r) ["e"] "should be Failure"
        }
    ]

let returnsEitherTests =
    testList "Returns - either" [

        test "either applies fSuccess on Success" {
            let r = Returns.either (fun (v,_) -> v * 2) (fun _ -> -1) (Returns.ok 5)
            Expect.equal r 10 "should apply fSuccess"
        }

        test "either applies fFailure on Failure" {
            let r = Returns.either (fun _ -> -1) (fun msgs -> msgs.Length) (Returns.failmany ["a";"b"])
            Expect.equal r 2 "should apply fFailure"
        }
    ]

let returnsJointMessagesTests =
    testList "Returns - jointMessages / jointMessage" [

        test "jointMessages appends to Success warnings" {
            let r = Returns.ok 1 |> Returns.jointMessages ["w1";"w2"]
            match r with
            | Success (v, msgs) ->
                Expect.equal v 1 "value unchanged"
                Expect.equal msgs ["w1";"w2"] "warnings appended"
            | _ -> failtest "Expected Success"
        }

        test "jointMessages appends to Failure errors" {
            let r = Returns.fail "e1" |> Returns.jointMessages ["e2"]
            Expect.equal (failureMessages r) ["e1";"e2"] "errors appended"
        }

        test "jointMessage appends single message" {
            let r = Returns.ok 1 |> Returns.jointMessage "w"
            match r with
            | Success (_, msgs) -> Expect.equal msgs ["w"] "single warning appended"
            | _ -> failtest "Expected Success"
        }
    ]

let returnsBindTests =
    testList "Returns - bind" [

        test "bind applies function on Success" {
            let r = Returns.ok 5 |> Returns.bind (fun v -> Returns.ok (v * 2))
            Expect.equal (successValue r) 10 "should apply binding function"
        }

        test "bind propagates Failure" {
            let r = Returns.fail "err" |> Returns.bind (fun v -> Returns.ok (v * 2))
            Expect.isTrue (isFailure r) "should remain Failure"
            Expect.equal (failureMessages r) ["err"] "errors propagated"
        }

        test "bind propagates existing warnings to result" {
            let r = Returns.warn "w" 3 |> Returns.bind (fun v -> Returns.ok (v + 1))
            match r with
            | Success (v, msgs) ->
                Expect.equal v 4 "value should be 4"
                Expect.equal msgs ["w"] "warning propagated"
            | _ -> failtest "Expected Success"
        }

        test "bind accumulates warnings from both steps" {
            let r = Returns.warn "w1" 3 |> Returns.bind (fun v -> Returns.warn "w2" (v + 1))
            match r with
            | Success (v, msgs) ->
                Expect.equal v 4 "value should be 4"
                // bind uses jointMessages which appends prior warnings after the new ones: ["w2"] @ ["w1"]
                Expect.equal msgs ["w2";"w1"] "both warnings present"
            | _ -> failtest "Expected Success"
        }

        test "bind operator >>= works correctly" {
            let r = Returns.ok 5 >>= (fun v -> Returns.ok (v + 1))
            Expect.equal (successValue r) 6 "should be 6"
        }
    ]

let returnsApplyTests =
    testList "Returns - apply" [

        test "apply applies wrapped function to Success value" {
            let f = Returns.ok (fun x -> x + 10)
            let r = Returns.apply f (Returns.ok 5)
            Expect.equal (successValue r) 15 "should be 15"
        }

        test "apply propagates function Failure" {
            let f : Returns<int->int, string> = Returns.fail "fn-err"
            let r = Returns.apply f (Returns.ok 5)
            Expect.equal (failureMessages r) ["fn-err"] "function error propagated"
        }

        test "apply propagates value Failure" {
            let f = Returns.ok (fun x -> x + 10)
            let r = Returns.apply f (Returns.fail "val-err")
            Expect.equal (failureMessages r) ["val-err"] "value error propagated"
        }

        test "apply concatenates errors when both Failure" {
            let f : Returns<int->int, string> = Returns.fail "fn-err"
            let r = Returns.apply f (Returns.fail "val-err")
            Expect.equal (failureMessages r) ["fn-err";"val-err"] "both errors concatenated"
        }

        test "apply concatenates warnings from function and value" {
            let f = Returns.warn "wf" (fun x -> x + 1)
            let r = Returns.apply f (Returns.warn "wv" 5)
            match r with
            | Success (v, msgs) ->
                Expect.equal v 6 "value correct"
                Expect.equal msgs ["wf";"wv"] "warnings concatenated"
            | _ -> failtest "Expected Success"
        }
    ]

let returnsMapTests =
    testList "Returns - map / map2 / map3 / map4" [

        test "map applies function on Success" {
            let r = Returns.ok 5 |> Returns.map (fun v -> v * 3)
            Expect.equal (successValue r) 15 "should be 15"
        }

        test "map propagates warnings" {
            let r = Returns.warn "w" 4 |> Returns.map (fun v -> v + 1)
            match r with
            | Success (v, msgs) ->
                Expect.equal v 5 "value correct"
                Expect.equal msgs ["w"] "warning propagated"
            | _ -> failtest "Expected Success"
        }

        test "map propagates Failure" {
            let r = Returns.fail "e" |> Returns.map (fun v -> v * 2)
            Expect.isTrue (isFailure r) "should remain Failure"
        }

        test "map2 combines two Successes" {
            let r = Returns.map2 (fun a b -> a + b) (Returns.ok 3) (Returns.ok 4)
            Expect.equal (successValue r) 7 "should be 7"
        }

        test "map2 propagates first Failure" {
            let r = Returns.map2 (fun a b -> a + b) (Returns.fail "e") (Returns.ok 4)
            Expect.isTrue (isFailure r) "should be Failure"
        }

        test "map <!> operator works" {
            let r = (<!>) (fun v -> v + 1) (Returns.ok 9)
            Expect.equal (successValue r) 10 "should be 10"
        }
    ]

let returnsMapMessagesTests =
    testList "Returns - mapMessages" [

        test "mapMessages transforms warning messages on Success" {
            let r = Returns.warn 1 42 |> Returns.mapMessages (fun n -> n * 10)
            match r with
            | Success (v, msgs) ->
                Expect.equal v 42 "value unchanged"
                Expect.equal msgs [10] "message transformed"
            | _ -> failtest "Expected Success"
        }

        test "mapMessages transforms error messages on Failure" {
            let r = Returns.fail 1 |> Returns.mapMessages (fun n -> n * 10)
            Expect.equal (failureMessages r) [10] "error transformed"
        }
    ]

let returnsFlattenTests =
    testList "Returns - flatten" [

        test "flatten collapses nested Success" {
            let r = Returns.ok (Returns.ok 42) |> Returns.flatten
            Expect.equal (successValue r) 42 "should unwrap to 42"
        }

        test "flatten propagates inner Failure" {
            let r = Returns.ok (Returns.fail "inner") |> Returns.flatten
            Expect.equal (failureMessages r) ["inner"] "inner failure propagated"
        }

        test "flatten propagates outer Failure" {
            let r : Returns<Returns<int,string>,string> = Returns.fail "outer" |> Returns.flatten
            Expect.equal (failureMessages r) ["outer"] "outer failure propagated"
        }
    ]

let returnsMergeTests =
    testList "Returns - merge" [

        test "merge combines two Successes" {
            let r = Returns.merge (+) (@) (Returns.ok 3) (Returns.ok 4)
            Expect.equal (successValue r) 7 "should be 7"
        }

        test "merge concatenates warnings from two Successes" {
            let r = Returns.merge (+) (@) (Returns.warn "w1" 3) (Returns.warn "w2" 4)
            match r with
            | Success (v, msgs) ->
                Expect.equal v 7 "value sum"
                Expect.equal msgs ["w1";"w2"] "warnings concatenated"
            | _ -> failtest "Expected Success"
        }

        test "merge propagates first Failure when second is Success" {
            let r = Returns.merge (+) (@) (Returns.fail "e") (Returns.ok 4)
            Expect.equal (failureMessages r) ["e"] "first error propagated"
        }

        test "merge propagates second Failure when first is Success" {
            let r = Returns.merge (+) (@) (Returns.ok 3) (Returns.fail "e")
            Expect.equal (failureMessages r) ["e"] "second error propagated"
        }

        test "merge concatenates errors from two Failures" {
            let r = Returns.merge (+) (@) (Returns.fail "e1") (Returns.fail "e2")
            Expect.equal (failureMessages r) ["e1";"e2"] "errors concatenated"
        }
    ]

let returnsFoldTests =
    testList "Returns - fold" [

        test "fold accumulates successes" {
            let items = [Returns.ok 1; Returns.ok 2; Returns.ok 3]
            let r = Returns.fold (+) (Returns.ok 0) items
            Expect.equal (successValue r) 6 "should sum to 6"
        }

        test "fold propagates failure" {
            let items = [Returns.ok 1; Returns.fail "e"; Returns.ok 3]
            let r = Returns.fold (+) (Returns.ok 0) items
            Expect.isTrue (isFailure r) "should be Failure"
        }

        test "fold on empty sequence returns state" {
            let r = Returns.fold (+) (Returns.ok 99) []
            Expect.equal (successValue r) 99 "should return state"
        }
    ]

let returnsPartitionTests =
    testList "Returns - partition" [

        test "partition separates successes and failures" {
            let items = [Returns.ok 1; Returns.fail "e1"; Returns.ok 2; Returns.fail "e2"]
            let (successes, failures) = Returns.partition items
            Expect.equal (successes |> List.map fst) [1;2] "should have successes 1 and 2"
            Expect.equal failures [["e1"];["e2"]] "should have two failure lists"
        }

        test "partition all successes" {
            let (successes, failures) = Returns.partition [Returns.ok 1; Returns.ok 2]
            Expect.equal (successes |> List.map fst) [1;2] "all successes"
            Expect.equal failures [] "no failures"
        }

        test "partition all failures" {
            let (successes, failures) = Returns.partition [Returns.fail "e1"; Returns.fail "e2"]
            Expect.equal successes [] "no successes"
            Expect.equal failures [["e1"];["e2"]] "all failures"
        }
    ]

let returnsZipTests =
    testList "Returns - zip" [

        test "zip combines two Successes into a tuple" {
            let r = Returns.zip (Returns.ok 1) (Returns.ok 2)
            match r with
            | Success ((a,b), _) ->
                Expect.equal a 1 "first element"
                Expect.equal b 2 "second element"
            | _ -> failtest "Expected Success"
        }

        test "zip propagates first Failure" {
            let r = Returns.zip (Returns.fail "e") (Returns.ok 2)
            Expect.equal (failureMessages r) ["e"] "first error propagated"
        }

        test "zip propagates second Failure" {
            let r = Returns.zip (Returns.ok 1) (Returns.fail "e")
            Expect.equal (failureMessages r) ["e"] "second error propagated"
        }
    ]

let returnsComposeTests =
    testList "Returns - compose / >>= / >=> / <=<" [

        test "compose chains two switch functions" {
            let f1 v = Returns.ok (v + 1)
            let f2 v = Returns.ok (v * 2)
            let composed = Returns.compose f1 f2
            let r = composed 3
            Expect.equal (successValue r) 8 "should be (3+1)*2 = 8"
        }

        test "compose propagates first failure" {
            let f1 _ = Returns.fail "step1"
            let f2 v = Returns.ok (v * 2)
            let r = Returns.compose f1 f2 5
            Expect.equal (failureMessages r) ["step1"] "first failure propagated"
        }

        test "compose propagates second failure" {
            let f1 v = Returns.ok (v + 1)
            let f2 _ = Returns.fail "step2"
            let r = Returns.compose f1 f2 5
            Expect.equal (failureMessages r) ["step2"] "second failure propagated"
        }

        test ">=> operator composes in series" {
            let f1 v = Returns.ok (v + 1)
            let f2 v = Returns.ok (v * 2)
            let r = (f1 >=> f2) 3
            Expect.equal (successValue r) 8 "should be 8"
        }

        test "<=< operator composes in reverse series" {
            let f1 v = Returns.ok (v + 1)
            let f2 v = Returns.ok (v * 2)
            let r = (f2 <=< f1) 3
            Expect.equal (successValue r) 8 "should be 8"
        }
    ]

let returnsPlusTests =
    testList "Returns - plus / &&& operator" [

        test "plus returns combined success" {
            let f1 v = Returns.ok v
            let f2 v = Returns.ok (v * 2)
            let r = Returns.plus (+) (@) f1 f2 5
            Expect.equal (successValue r) 15 "should be 5+10=15"
        }

        test "plus propagates first failure" {
            let f1 _ = Returns.fail "e1"
            let f2 v = Returns.ok v
            let r = Returns.plus (+) (@) f1 f2 5
            Expect.equal (failureMessages r) ["e1"] "first failure propagated"
        }

        test "plus concatenates both failures" {
            let f1 _ = Returns.fail "e1"
            let f2 _ = Returns.fail "e2"
            let r = Returns.plus (+) (@) f1 f2 5
            Expect.equal (failureMessages r) ["e1";"e2"] "both errors concatenated"
        }

        test "&&& operator accumulates errors from both branches" {
            let f1 _ = Returns.fail "e1"
            let f2 _ = Returns.fail "e2"
            let r = (f1 &&& f2) 5
            Expect.equal (failureMessages r) ["e1";"e2"] "both errors concatenated"
        }
    ]

let returnsTeeTests =
    testList "Returns - tee / eitherTee / successTee / failureTee" [

        test "tee executes side effect and returns original value" {
            let mutable sideEffect = 0
            let result = Returns.tee (fun v -> sideEffect <- v) 42
            Expect.equal result 42 "original value returned"
            Expect.equal sideEffect 42 "side effect was executed"
        }

        test "eitherTee calls fSuccess on Success and propagates unchanged" {
            let mutable called = false
            let r = Returns.ok 5 |> Returns.eitherTee (fun _ -> called <- true) ignore
            Expect.isTrue called "fSuccess was called"
            Expect.equal (successValue r) 5 "returns unchanged"
        }

        test "eitherTee calls fFailure on Failure and propagates unchanged" {
            let mutable called = false
            let r = Returns.fail "e" |> Returns.eitherTee ignore (fun _ -> called <- true)
            Expect.isTrue called "fFailure was called"
            Expect.equal (failureMessages r) ["e"] "failure unchanged"
        }

        test "successTee only executes on Success" {
            let mutable count = 0
            Returns.ok 1 |> Returns.successTee (fun _ -> count <- count + 1) |> ignore
            Returns.fail "e" |> Returns.successTee (fun _ -> count <- count + 1) |> ignore
            Expect.equal count 1 "called only once for Success"
        }

        test "failureTee only executes on Failure" {
            let mutable count = 0
            Returns.ok 1 |> Returns.failureTee (fun _ -> count <- count + 1) |> ignore
            Returns.fail "e" |> Returns.failureTee (fun _ -> count <- count + 1) |> ignore
            Expect.equal count 1 "called only once for Failure"
        }
    ]

let returnsActivePatternTests =
    testList "Returns - Active Patterns (Pass|Warn|Fail)" [

        test "Pass pattern matches clean Success" {
            let r = Returns.ok 42
            match r with
            | Returns.Pass v -> Expect.equal v 42 "should match Pass"
            | _ -> failtest "Expected Pass"
        }

        test "Warn pattern matches Success with warnings" {
            let r = Returns.warn "w" 7
            match r with
            | Returns.Warn (v, msgs) ->
                Expect.equal v 7 "value matches"
                Expect.equal msgs ["w"] "warning matches"
            | _ -> failtest "Expected Warn"
        }

        test "Fail pattern matches Failure" {
            let r : Returns<int,string> = Returns.fail "e"
            match r with
            | Returns.Fail msgs -> Expect.equal msgs ["e"] "error matches"
            | _ -> failtest "Expected Fail"
        }
    ]

let returnsToStringTests =
    testList "Returns - ToString" [

        test "Success with no messages has correct string format" {
            let r = Returns.ok 42
            let s = r.ToString()
            Expect.stringContains s "Success" "should contain 'Success'"
            Expect.stringContains s "42" "should contain the value"
        }

        test "Failure has correct string format" {
            let r : Returns<int,string> = Returns.fail "err"
            let s = r.ToString()
            Expect.stringContains s "Failure" "should contain 'Failure'"
            Expect.stringContains s "err" "should contain the error"
        }
    ]

// ============================================================
// ReturnsBuilder computation expression tests
// ============================================================

let returnsBuilderTests =
    testList "ReturnsBuilder - computation expression" [

        test "returns CE wraps value in Success" {
            let r = returns { return 42 }
            Expect.equal (successValue r) 42 "should be Success 42"
        }

        test "returns CE binds two successes" {
            let r = returns {
                let! x = Returns.ok 3
                let! y = Returns.ok 4
                return x + y
            }
            Expect.equal (successValue r) 7 "should be 7"
        }

        test "returns CE short-circuits on Failure" {
            let r : Returns<int,string> = returns {
                let! x = Returns.ok 3
                let! _ = Returns.fail "oops"
                return x + 1
            }
            Expect.equal (failureMessages r) ["oops"] "should propagate failure"
        }

        test "returns CE propagates warnings through bind" {
            let r = returns {
                let! x = Returns.warn "w" 3
                return x + 1
            }
            match r with
            | Success (v, msgs) ->
                Expect.equal v 4 "value is 4"
                Expect.equal msgs ["w"] "warning propagated"
            | _ -> failtest "Expected Success"
        }

        test "returns CE handles returnFrom" {
            let r = returns { return! Returns.ok 99 }
            Expect.equal (successValue r) 99 "returnFrom works"
        }

        test "returns CE handles exceptions with TryWith" {
            let r = returns {
                try
                    return 1
                with _ ->
                    return! Returns.fail "caught"
            }
            Expect.equal (successValue r) 1 "normal path"
        }
    ]

// ============================================================
// Result.Extension module tests
// ============================================================

let resultExtensionTests =
    testList "Result.Extension" [

        test "defaultValue returns value on Ok" {
            Expect.equal (Result.defaultValue 0 (Result.Ok 42)) 42 "should return 42"
        }

        test "defaultValue returns default on Error" {
            Expect.equal (Result.defaultValue 99 (Result.Error "e")) 99 "should return 99"
        }

        test "defaultWith returns value on Ok" {
            Expect.equal (Result.defaultWith (fun _ -> 0) (Result.Ok 42)) 42 "should return 42"
        }

        test "defaultWith applies function on Error" {
            Expect.equal (Result.defaultWith (fun (e:string) -> e.Length) (Result.Error "err")) 3 "should be 3"
        }

        test "valueOrFailwith returns value on Ok" {
            Expect.equal (Result.valueOrFailwith (Result.Ok 5)) 5 "should be 5"
        }

        test "valueOrFailwith throws on Error" {
            Expect.throws (fun () -> Result.valueOrFailwith (Result.Error "e") |> ignore) "should throw"
        }

        test "tryCatch returns Ok when function succeeds" {
            let r = Result.tryCatch (fun x -> x + 1) 5
            match r with
            | Result.Ok v -> Expect.equal v 6 "should be 6"
            | _ -> failtest "Expected Ok"
        }

        test "tryCatch returns Error when function throws" {
            let r = Result.tryCatch (fun _ -> failwith "boom") 0
            match r with
            | Result.Error _ -> ()
            | _ -> failtest "Expected Error"
        }

        test "isOk returns true for Ok" {
            Expect.isTrue (Result.isOk (Result.Ok 1)) "should be true"
        }

        test "isOk returns false for Error" {
            Expect.isFalse (Result.isOk (Result.Error "e")) "should be false"
        }

        test "isError returns true for Error" {
            Expect.isTrue (Result.isError (Result.Error "e")) "should be true"
        }

        test "isError returns false for Ok" {
            Expect.isFalse (Result.isError (Result.Ok 1)) "should be false"
        }

        test "toChoice converts Ok to Choice1Of2" {
            match Result.toChoice (Result.Ok 5) with
            | Choice1Of2 v -> Expect.equal v 5 "should be 5"
            | _ -> failtest "Expected Choice1Of2"
        }

        test "toChoice converts Error to Choice2Of2" {
            match Result.toChoice (Result.Error "e") with
            | Choice2Of2 e -> Expect.equal e "e" "should be 'e'"
            | _ -> failtest "Expected Choice2Of2"
        }

        test "ofChoice converts Choice1Of2 to Ok" {
            match Result.ofChoice (Choice1Of2 7) with
            | Result.Ok v -> Expect.equal v 7 "should be 7"
            | _ -> failtest "Expected Ok"
        }

        test "ofChoice converts Choice2Of2 to Error" {
            match Result.ofChoice (Choice2Of2 "e") with
            | Result.Error e -> Expect.equal e "e" "should be 'e'"
            | _ -> failtest "Expected Error"
        }

        test "ofOption returns Ok for Some" {
            match Result.ofOption "err" (Some 3) with
            | Result.Ok v -> Expect.equal v 3 "should be 3"
            | _ -> failtest "Expected Ok"
        }

        test "ofOption returns Error for None" {
            match Result.ofOption "err" None with
            | Result.Error e -> Expect.equal e "err" "should be 'err'"
            | _ -> failtest "Expected Error"
        }

        test "either applies fOk on Ok" {
            let r = Result.either (fun v -> v + 1) (fun _ -> -1) (Result.Ok 5)
            Expect.equal r 6 "should apply fOk"
        }

        test "either applies fError on Error" {
            let r = Result.either (fun _ -> -1) (fun (e:string) -> e.Length) (Result.Error "ab")
            Expect.equal r 2 "should apply fError"
        }

        test "apply applies Ok function to Ok value" {
            let r = Result.apply (Result.Ok (fun x -> x + 1)) (Result.Ok 5)
            match r with
            | Result.Ok v -> Expect.equal v 6 "should be 6"
            | _ -> failtest "Expected Ok"
        }

        test "apply propagates function Error" {
            let r : Result<int,string> = Result.apply (Result.Error "fn-err") (Result.Ok 5)
            match r with
            | Result.Error e -> Expect.equal e "fn-err" "fn error propagated"
            | _ -> failtest "Expected Error"
        }

        test "apply propagates value Error" {
            let r = Result.apply (Result.Ok (fun (x:int) -> x)) (Result.Error "val-err")
            match r with
            | Result.Error e -> Expect.equal e "val-err" "value error propagated"
            | _ -> failtest "Expected Error"
        }

        test "Pass active pattern matches Ok" {
            match Result.Ok 42 with
            | Result.Pass v -> Expect.equal v 42 "Pass matches Ok"
            | _ -> failtest "Expected Pass"
        }

        test "Fail active pattern matches Error" {
            match Result.Error "e" with
            | Result.Fail e -> Expect.equal e "e" "Fail matches Error"
            | _ -> failtest "Expected Fail"
        }
    ]

// ============================================================
// Choice.Extension module tests
// ============================================================

let choiceExtensionTests =
    testList "Choice.Extension" [

        test "result wraps value in Choice1Of2" {
            match Choice.result 5 with
            | Choice1Of2 v -> Expect.equal v 5 "should be 5"
            | _ -> failtest "Expected Choice1Of2"
        }

        test "throw wraps value in Choice2Of2" {
            match Choice.throw "err" with
            | Choice2Of2 e -> Expect.equal e "err" "should be 'err'"
            | _ -> failtest "Expected Choice2Of2"
        }

        test "map transforms Choice1Of2 value" {
            let r = Choice.map (fun x -> x * 2) (Choice1Of2 5)
            match r with
            | Choice1Of2 v -> Expect.equal v 10 "should be 10"
            | _ -> failtest "Expected Choice1Of2"
        }

        test "map propagates Choice2Of2" {
            let r = Choice.map (fun x -> x * 2) (Choice2Of2 "e")
            match r with
            | Choice2Of2 e -> Expect.equal e "e" "error propagated"
            | _ -> failtest "Expected Choice2Of2"
        }

        test "apply applies Choice1Of2 function to Choice1Of2 value" {
            let r = Choice.apply (Choice1Of2 (fun x -> x + 1)) (Choice1Of2 5)
            match r with
            | Choice1Of2 v -> Expect.equal v 6 "should be 6"
            | _ -> failtest "Expected Choice1Of2"
        }

        test "apply propagates first Choice2Of2" {
            let r = Choice.apply (Choice2Of2 "e") (Choice1Of2 5)
            match r with
            | Choice2Of2 e -> Expect.equal e "e" "error propagated"
            | _ -> failtest "Expected Choice2Of2"
        }

        test "map2 combines two Choice1Of2 values" {
            let r = Choice.map2 (+) (Choice1Of2 3) (Choice1Of2 4)
            match r with
            | Choice1Of2 v -> Expect.equal v 7 "should be 7"
            | _ -> failtest "Expected Choice1Of2"
        }

        test "map2 propagates first error" {
            let r = Choice.map2 (+) (Choice2Of2 "e") (Choice1Of2 4)
            match r with
            | Choice2Of2 e -> Expect.equal e "e" "error propagated"
            | _ -> failtest "Expected Choice2Of2"
        }

        test "flatten collapses nested Choice1Of2" {
            let r = Choice.flatten (Choice1Of2 (Choice1Of2 42))
            match r with
            | Choice1Of2 v -> Expect.equal v 42 "should unwrap to 42"
            | _ -> failtest "Expected Choice1Of2"
        }

        test "flatten propagates inner Choice2Of2" {
            let r = Choice.flatten (Choice1Of2 (Choice2Of2 "e"))
            match r with
            | Choice2Of2 e -> Expect.equal e "e" "inner error propagated"
            | _ -> failtest "Expected Choice2Of2"
        }

        test "flatten propagates outer Choice2Of2" {
            let r : Choice<int,string> = Choice.flatten (Choice2Of2 "e")
            match r with
            | Choice2Of2 e -> Expect.equal e "e" "outer error propagated"
            | _ -> failtest "Expected Choice2Of2"
        }

        test "bind applies function to Choice1Of2" {
            let r = Choice.bind (fun x -> Choice1Of2 (x + 1)) (Choice1Of2 5)
            match r with
            | Choice1Of2 v -> Expect.equal v 6 "should be 6"
            | _ -> failtest "Expected Choice1Of2"
        }

        test "bind propagates Choice2Of2" {
            let r = Choice.bind (fun x -> Choice1Of2 (x + 1)) (Choice2Of2 "e")
            match r with
            | Choice2Of2 e -> Expect.equal e "e" "error propagated"
            | _ -> failtest "Expected Choice2Of2"
        }

        test "bindChoice2Of2 applies function to Choice2Of2" {
            let r = Choice.bindChoice2Of2 (fun e -> Choice1Of2 (e + "!")) (Choice2Of2 "err")
            match r with
            | Choice1Of2 v -> Expect.equal v "err!" "should recover"
            | _ -> failtest "Expected Choice1Of2"
        }

        test "bindChoice2Of2 leaves Choice1Of2 unchanged" {
            let r = Choice.bindChoice2Of2 (fun _ -> Choice2Of2 "never") (Choice1Of2 5)
            match r with
            | Choice1Of2 v -> Expect.equal v 5 "unchanged"
            | _ -> failtest "Expected Choice1Of2"
        }

        test "either applies fChoice1Of2 on Choice1Of2" {
            let r = Choice.either (fun v -> v + 1) (fun _ -> -1) (Choice1Of2 5)
            Expect.equal r 6 "should apply fChoice1Of2"
        }

        test "either applies fChoice2Of2 on Choice2Of2" {
            let r = Choice.either (fun _ -> -1) (fun (e:string) -> e.Length) (Choice2Of2 "ab")
            Expect.equal r 2 "should apply fChoice2Of2"
        }

        test "protect returns Choice1Of2 when no exception" {
            let r = Choice.protect (fun x -> x + 1) 5
            match r with
            | Choice1Of2 v -> Expect.equal v 6 "should be 6"
            | _ -> failtest "Expected Choice1Of2"
        }

        test "protect returns Choice2Of2 when exception thrown" {
            let r = Choice.protect (fun _ -> failwith "boom") 0
            match r with
            | Choice2Of2 _ -> ()
            | _ -> failtest "Expected Choice2Of2"
        }
    ]

// ============================================================
// Option.Extension module tests
// ============================================================

let optionExtensionTests =
    testList "Option.Extension" [

        test "apply Some function to Some value yields Some result" {
            let r = Option.apply (Some (fun x -> x + 1)) (Some 5)
            Expect.equal r (Some 6) "should be Some 6"
        }

        test "apply None function yields None" {
            let r = Option.apply None (Some 5)
            Expect.equal r None "should be None"
        }

        test "apply Some function to None yields None" {
            let r = Option.apply (Some (fun x -> x + 1)) None
            Expect.equal r None "should be None"
        }

        test "unzip Some tuple yields two Somes" {
            let a, b = Option.unzip (Some (1, 2))
            Expect.equal a (Some 1) "first should be Some 1"
            Expect.equal b (Some 2) "second should be Some 2"
        }

        test "unzip None yields two Nones" {
            let a, b = Option.unzip None
            Expect.equal a None "first should be None"
            Expect.equal b None "second should be None"
        }

        test "zip two Somes yields Some tuple" {
            let r = Option.zip (Some 1) (Some 2)
            Expect.equal r (Some (1, 2)) "should be Some (1,2)"
        }

        test "zip None and Some yields None" {
            let r = Option.zip None (Some 2)
            Expect.equal r None "should be None"
        }

        test "toResult converts Some to Ok" {
            let r = Option.toResult (Some 5)
            match r with
            | Result.Ok v -> Expect.equal v 5 "should be Ok 5"
            | _ -> failtest "Expected Ok"
        }

        test "toResult converts None to Error ()" {
            let r = Option.toResult (None : int option)
            match r with
            | Result.Error () -> ()
            | _ -> failtest "Expected Error ()"
        }

        test "toResultWith converts Some to Ok" {
            let r = Option.toResultWith "err" (Some 3)
            match r with
            | Result.Ok v -> Expect.equal v 3 "should be Ok 3"
            | _ -> failtest "Expected Ok"
        }

        test "toResultWith converts None to Error with value" {
            let r = Option.toResultWith "err" None
            match r with
            | Result.Error e -> Expect.equal e "err" "should be Error 'err'"
            | _ -> failtest "Expected Error"
        }

        test "ofResult converts Ok to Some" {
            let r = Option.ofResult (Result.Ok 5)
            Expect.equal r (Some 5) "should be Some 5"
        }

        test "ofResult converts Error to None" {
            let r = Option.ofResult (Result.Error "e")
            Expect.equal r None "should be None"
        }

        test "protect returns Some when no exception" {
            let r = Option.protect (fun x -> x + 1) 5
            Expect.equal r (Some 6) "should be Some 6"
        }

        test "protect returns None when exception thrown" {
            let r = Option.protect (fun _ -> failwith "boom") 0
            Expect.equal r None "should be None"
        }

        test "ofPair returns Some when bool is true" {
            let r = Option.ofPair (true, 42)
            Expect.equal r (Some 42) "should be Some 42"
        }

        test "ofPair returns None when bool is false" {
            let r = Option.ofPair (false, 42)
            Expect.equal r None "should be None"
        }
    ]

// ============================================================
// Validation module tests
// ============================================================

type TestPerson =
    {
        name: string
        age: int
        email: string option
        tags: string list
    }

type TestBox =
    {
        width: float
        height: float
        label: string
    }

let validationBasicValidatorTests =
    testList "Validation - Basic Validators" [

        test "isEqualTo returns Ok when equal" {
            let v = isEqualTo 5 "prop" 5
            Expect.equal v Ok "should be Ok"
        }

        test "isEqualTo returns Errors when not equal" {
            let v = isEqualTo 5 "prop" 4
            match v with
            | Errors es ->
                Expect.equal es.Length 1 "one error"
                Expect.equal es.[0].errorCode "isEqualTo" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "isNotEqualTo returns Ok when not equal" {
            let v = isNotEqualTo 5 "prop" 4
            Expect.equal v Ok "should be Ok"
        }

        test "isNotEqualTo returns Errors when equal" {
            let v = isNotEqualTo 5 "prop" 5
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isNotEqualTo" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "isNotNull returns Ok when not null" {
            let v = isNotNull "prop" "hello"
            Expect.equal v Ok "should be Ok"
        }

        test "isNotNull returns Errors when null" {
            let v = isNotNull "prop" null
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isNotNull" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "isGreaterThan returns Ok when greater" {
            let v = isGreaterThan 0 "prop" 5
            Expect.equal v Ok "should be Ok"
        }

        test "isGreaterThan returns Errors when not greater" {
            let v = isGreaterThan 5 "prop" 5
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isGreaterThan" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "isGreaterThanOrEqualTo returns Ok when equal" {
            let v = isGreaterThanOrEqualTo 5 "prop" 5
            Expect.equal v Ok "should be Ok"
        }

        test "isGreaterThanOrEqualTo returns Errors when less" {
            let v = isGreaterThanOrEqualTo 5 "prop" 4
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isGreaterThanOrEqualTo" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "isLessThan returns Ok when less" {
            let v = isLessThan 10 "prop" 5
            Expect.equal v Ok "should be Ok"
        }

        test "isLessThan returns Errors when equal or greater" {
            let v = isLessThan 5 "prop" 5
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isLessThan" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "isLessThanOrEqualTo returns Ok when equal" {
            let v = isLessThanOrEqualTo 5 "prop" 5
            Expect.equal v Ok "should be Ok"
        }

        test "isLessThanOrEqualTo returns Errors when greater" {
            let v = isLessThanOrEqualTo 5 "prop" 6
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isLessThanOrEqualTo" "correct error code"
            | Ok -> failtest "Expected Errors"
        }
    ]

let validationCollectionValidatorTests =
    testList "Validation - Collection Validators" [

        test "isNotEmpty returns Ok for non-empty sequence" {
            let v = isNotEmpty "prop" [1;2;3]
            Expect.equal v Ok "should be Ok"
        }

        test "isNotEmpty returns Errors for empty sequence" {
            let v = isNotEmpty "prop" []
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isNotEmpty" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "isNotEmpty returns Errors for null sequence" {
            let v = isNotEmpty "prop" (null : seq<int>)
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isNotEmpty" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "isEmpty returns Ok for empty sequence" {
            let v = isEmpty "prop" []
            Expect.equal v Ok "should be Ok"
        }

        test "isEmpty returns Ok for null sequence" {
            let v = isEmpty "prop" (null : seq<int>)
            Expect.equal v Ok "should be Ok for null"
        }

        test "isEmpty returns Errors for non-empty sequence" {
            let v = isEmpty "prop" [1]
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isEmpty" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "hasLengthOf returns Ok for matching length" {
            let v = hasLengthOf 3 "prop" [1;2;3]
            Expect.equal v Ok "should be Ok"
        }

        test "hasLengthOf returns Errors for wrong length" {
            let v = hasLengthOf 3 "prop" [1;2]
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "hasLengthOf" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "hasMinLengthOf returns Ok when length meets minimum" {
            let v = hasMinLengthOf 2 "prop" [1;2;3]
            Expect.equal v Ok "should be Ok"
        }

        test "hasMinLengthOf returns Errors when too short" {
            let v = hasMinLengthOf 3 "prop" [1;2]
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "hasMinLengthOf" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "hasMaxLengthOf returns Ok when within limit" {
            let v = hasMaxLengthOf 5 "prop" [1;2;3]
            Expect.equal v Ok "should be Ok"
        }

        test "hasMaxLengthOf returns Errors when too long" {
            let v = hasMaxLengthOf 2 "prop" [1;2;3]
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "hasMaxLengthOf" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "eachItemWith validates all items" {
            let itemValidator (x:int) = if x > 0 then Ok else Errors [{ errorCode="pos"; message="must be positive"; property="val" }]
            let v = eachItemWith itemValidator "items" [1;2;3]
            Expect.equal v Ok "all positive should be Ok"
        }

        test "eachItemWith accumulates errors for invalid items" {
            let itemValidator (x:int) = if x > 0 then Ok else Errors [{ errorCode="pos"; message="must be positive"; property="val" }]
            let v = eachItemWith itemValidator "items" [1;-1;2;-2]
            match v with
            | Errors es ->
                Expect.equal es.Length 2 "two errors"
                Expect.stringContains es.[0].property "items.[1]" "first error indexed"
                Expect.stringContains es.[1].property "items.[3]" "second error indexed"
            | Ok -> failtest "Expected Errors"
        }
    ]

let validationStringValidatorTests =
    testList "Validation - String Validators" [

        test "isNotEmptyOrWhitespace returns Ok for valid string" {
            let v = isNotEmptyOrWhitespace "prop" "hello"
            Expect.equal v Ok "should be Ok"
        }

        test "isNotEmptyOrWhitespace returns Errors for null" {
            let v = isNotEmptyOrWhitespace "prop" null
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isNotEmptyOrWhitespace" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "isNotEmptyOrWhitespace returns Errors for empty string" {
            let v = isNotEmptyOrWhitespace "prop" ""
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isNotEmptyOrWhitespace" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "isNotEmptyOrWhitespace returns Errors for whitespace" {
            let v = isNotEmptyOrWhitespace "prop" "   "
            match v with
            | Errors es -> Expect.equal es.[0].errorCode "isNotEmptyOrWhitespace" "correct error code"
            | Ok -> failtest "Expected Errors"
        }
    ]

let validationBuilderTests =
    testList "Validation - ValidatorBuilder" [

        test "validate returns Ok when all validators pass" {
            let validate = createValidatorFor<TestBox>() {
                validate (fun o -> o.width) [ isGreaterThan 0.0 ]
                validate (fun o -> o.height) [ isGreaterThan 0.0 ]
            }
            let box = { width = 10.0; height = 5.0; label = "box" }
            let r = validate box
            Expect.equal r Ok "should be Ok"
        }

        test "validate returns Errors when a validator fails" {
            let validate = createValidatorFor<TestBox>() {
                validate (fun o -> o.width) [ isGreaterThan 0.0 ]
            }
            let box = { width = -1.0; height = 5.0; label = "box" }
            let r = validate box
            match r with
            | Errors es ->
                Expect.equal es.Length 1 "one error"
                Expect.equal es.[0].errorCode "isGreaterThan" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "validate accumulates errors from multiple failing validators" {
            let validate = createValidatorFor<TestBox>() {
                validate (fun o -> o.width) [ isGreaterThan 0.0 ]
                validate (fun o -> o.height) [ isGreaterThan 0.0 ]
            }
            let box = { width = -1.0; height = -1.0; label = "box" }
            let r = validate box
            match r with
            | Errors es -> Expect.equal es.Length 2 "two errors"
            | Ok -> failtest "Expected Errors"
        }

        test "validate with withFunction works correctly" {
            let validate = createValidatorFor<TestBox>() {
                validate (fun o -> o) [
                    withFunction (fun b ->
                        if b.width > b.height
                        then Errors [{ errorCode="WidthTooLarge"; message="Width > height"; property="width" }]
                        else Ok)
                ]
            }
            let goodBox = { width = 5.0; height = 10.0; label = "" }
            let badBox = { width = 20.0; height = 10.0; label = "" }
            Expect.equal (validate goodBox) Ok "good box is valid"
            match validate badBox with
            | Errors es -> Expect.equal es.[0].errorCode "WidthTooLarge" "correct error code"
            | Ok -> failtest "Expected Errors"
        }

        test "validateRequired returns Ok when option is Some" {
            let validate = createValidatorFor<TestPerson>() {
                validateRequired (fun o -> o.email) [ isNotEmptyOrWhitespace ]
            }
            let person = { name = "Alice"; age = 30; email = Some "alice@test.com"; tags = [] }
            Expect.equal (validate person) Ok "should be Ok"
        }

        test "validateRequired returns Errors when option is None" {
            let validate = createValidatorFor<TestPerson>() {
                validateRequired (fun o -> o.email) [ isNotEmptyOrWhitespace ]
            }
            let person = { name = "Alice"; age = 30; email = None; tags = [] }
            match validate person with
            | Errors es ->
                Expect.equal es.Length 1 "one error"
                Expect.equal es.[0].errorCode "validatorRequired" "required error code"
            | Ok -> failtest "Expected Errors"
        }

        test "validateUnrequired returns Ok when option is None" {
            let validate = createValidatorFor<TestPerson>() {
                validateUnrequired (fun o -> o.email) [ isNotEmptyOrWhitespace ]
            }
            let person = { name = "Alice"; age = 30; email = None; tags = [] }
            Expect.equal (validate person) Ok "None is allowed"
        }

        test "validateUnrequired validates when option is Some" {
            let validate = createValidatorFor<TestPerson>() {
                validateUnrequired (fun o -> o.email) [ isNotEmptyOrWhitespace ]
            }
            let person = { name = "Alice"; age = 30; email = Some ""; tags = [] }
            match validate person with
            | Errors es -> Expect.equal es.Length 1 "one error for invalid Some"
            | Ok -> failtest "Expected Errors"
        }

        test "validateWhen only validates when predicate is true" {
            let validate = createValidatorFor<TestBox>() {
                validateWhen (fun b -> b.label <> "") (fun o -> o.width) [ isGreaterThan 100.0 ]
            }
            let unlabeledBox = { width = 5.0; height = 5.0; label = "" }
            let labeledBox = { width = 5.0; height = 5.0; label = "hi" }
            Expect.equal (validate unlabeledBox) Ok "unlabeled: predicate false, skip validation"
            match validate labeledBox with
            | Errors _ -> ()
            | Ok -> failtest "labeled: predicate true, should fail"
        }

        test "validateWhen passes when predicate is true and value is valid" {
            let validate = createValidatorFor<TestBox>() {
                validateWhen (fun _ -> true) (fun o -> o.width) [ isGreaterThan 0.0 ]
            }
            let box = { width = 10.0; height = 5.0; label = "" }
            Expect.equal (validate box) Ok "should be Ok"
        }

        test "validateRequiredWhen only validates when predicate is true" {
            let validate = createValidatorFor<TestPerson>() {
                validateRequiredWhen (fun p -> p.age > 18) (fun o -> o.email) [ isNotEmptyOrWhitespace ]
            }
            let minor = { name = "Bob"; age = 15; email = None; tags = [] }
            let adult = { name = "Alice"; age = 25; email = None; tags = [] }
            Expect.equal (validate minor) Ok "minor: predicate false, skip"
            match validate adult with
            | Errors es -> Expect.equal es.[0].errorCode "validatorRequired" "adult: email required"
            | Ok -> failtest "Expected Errors for adult"
        }

        test "validateUnrequiredWhen only validates when predicate is true" {
            let validate = createValidatorFor<TestPerson>() {
                validateUnrequiredWhen (fun p -> p.age > 18) (fun o -> o.email) [ isNotEmptyOrWhitespace ]
            }
            let minor = { name = "Bob"; age = 15; email = Some ""; tags = [] }
            let adult = { name = "Alice"; age = 25; email = Some ""; tags = [] }
            Expect.equal (validate minor) Ok "minor: predicate false, skip"
            match validate adult with
            | Errors _ -> ()
            | Ok -> failtest "adult: predicate true, should fail"
        }

        test "withValidator applies sub-validator and prefixes property path" {
            let validateBox = createValidatorFor<TestBox>() {
                validate (fun o -> o.width) [ isGreaterThan 0.0 ]
            }
            let validate = createValidatorFor<TestPerson>() {
                validate (fun _ -> { width = -1.0; height = 5.0; label = "" }) [
                    withValidator validateBox
                ]
            }
            let person = { name = "Alice"; age = 30; email = None; tags = [] }
            match validate person with
            | Errors es ->
                Expect.equal es.Length 1 "one error"
                Expect.stringContains es.[0].property "width" "property path prefixed"
            | Ok -> failtest "Expected Errors"
        }

        test "withValidatorWhen applies sub-validator only when predicate is true" {
            let validateBox = createValidatorFor<TestBox>() {
                validate (fun o -> o.width) [ isGreaterThan 100.0 ]
            }
            let box = { width = 5.0; height = 5.0; label = "" }
            let validate = createValidatorFor<TestBox>() {
                validate (fun o -> o) [
                    withValidatorWhen (fun b -> b.height > 10.0) validateBox
                ]
            }
            let shortBox = { width = 5.0; height = 5.0; label = "" }
            let tallBox = { width = 5.0; height = 20.0; label = "" }
            Expect.equal (validate shortBox) Ok "predicate false: skip"
            match validate tallBox with
            | Errors _ -> ()
            | Ok -> failtest "predicate true: should fail"
        }
    ]

// ============================================================
// Integration tests (end-to-end using computation expression)
// ============================================================

let integrationTests =
    testList "Integration - End-to-end" [

        test "pipeline of validate -> transform -> validate" {
            let parseAge (s:string) =
                match System.Int32.TryParse(s) with
                | true, v -> Returns.ok v
                | _ -> Returns.fail "Invalid age"

            let validateAge age =
                if age >= 0 && age <= 150
                then Returns.ok age
                else Returns.fail "Age out of range"

            let r =
                returns {
                    let! age = parseAge "25"
                    let! validated = validateAge age
                    return validated * 2
                }
            Expect.equal (successValue r) 50 "should be 50"
        }

        test "pipeline short-circuits on first failure" {
            let step1 _ = Returns.fail "step1 failed"
            let step2 v = Returns.ok (v + 1)
            let r = returns {
                let! x = step1 ()
                let! y = step2 x
                return y
            }
            Expect.equal (failureMessages r) ["step1 failed"] "only first failure"
        }

        test "Cylinder geometry validation passes for valid input" {
            let validateCylinder = createValidatorFor<{| id: float; od: float; l: float |}> () {
                validate (fun o -> o.id) [ isGreaterThan 0.0 ]
                validate (fun o -> o.od) [ isGreaterThan 0.0 ]
                validate (fun o -> o.l)  [ isGreaterThan 0.0 ]
                validate (fun o -> o) [
                    withFunction (fun o ->
                        if o.od <= o.id
                        then Errors [{ errorCode="InvalidGeometry"; message="od <= id"; property="od" }]
                        else Ok)
                ]
            }
            let result = validateCylinder {| id = 100.0; od = 200.0; l = 1000.0 |}
            Expect.equal result Ok "should pass for valid cylinder"
        }

        test "Cylinder geometry validation fails when od <= id" {
            let validateCylinder = createValidatorFor<{| id: float; od: float; l: float |}> () {
                validate (fun o -> o.id) [ isGreaterThan 0.0 ]
                validate (fun o -> o.od) [ isGreaterThan 0.0 ]
                validate (fun o -> o.l)  [ isGreaterThan 0.0 ]
                validate (fun o -> o) [
                    withFunction (fun o ->
                        if o.od <= o.id
                        then Errors [{ errorCode="InvalidGeometry"; message="od <= id"; property="od" }]
                        else Ok)
                ]
            }
            let result = validateCylinder {| id = 200.0; od = 100.0; l = 1000.0 |}
            match result with
            | Errors es -> Expect.equal es.[0].errorCode "InvalidGeometry" "correct error"
            | Ok -> failtest "Expected Errors"
        }
    ]

// ============================================================
// Entry point
// ============================================================

[<EntryPoint>]
let main argv =
    let allTests =
        testList "All Tests" [
            returnsCreationTests
            returnsPredicateTests
            returnsDefaultTests
            returnsValueOrFailwithTests
            returnsFailOnWarningsTests
            returnsTryCatchTests
            returnsConversionTests
            returnsEitherTests
            returnsJointMessagesTests
            returnsBindTests
            returnsApplyTests
            returnsMapTests
            returnsMapMessagesTests
            returnsFlattenTests
            returnsMergeTests
            returnsFoldTests
            returnsPartitionTests
            returnsZipTests
            returnsComposeTests
            returnsPlusTests
            returnsTeeTests
            returnsActivePatternTests
            returnsToStringTests
            returnsBuilderTests
            resultExtensionTests
            choiceExtensionTests
            optionExtensionTests
            validationBasicValidatorTests
            validationCollectionValidatorTests
            validationStringValidatorTests
            validationBuilderTests
            integrationTests
        ]
    runTestsWithCLIArgs [] argv allTests
