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

        test "ofChoice wraps Choice1Of2 into Success, restoring warnings" {
            let r = Returns.ofChoice (Choice1Of2 (10, ["w"]))
            match r with
            | Success (v, msgs) ->
                Expect.equal v 10 "should be Success with 10"
                Expect.equal msgs ["w"] "warnings restored"
            | _ -> failtest "Expected Success"
        }

        test "ofChoice wraps Choice1Of2 with no warnings into clean Success" {
            let r = Returns.ofChoice (Choice1Of2 (10, []))
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

        test "ofResult wraps Ok into Success, restoring warnings" {
            let r = Returns.ofResult (Result.Ok (5, ["w"]))
            match r with
            | Success (v, msgs) ->
                Expect.equal v 5 "should be Success with 5"
                Expect.equal msgs ["w"] "warnings restored"
            | _ -> failtest "Expected Success"
        }

        test "ofResult wraps Ok with no warnings into clean Success" {
            let r = Returns.ofResult (Result.Ok (5, []))
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

        test "ToString does not throw on null messages" {
            let r : Returns<int,string> = Returns.failmany [ "e1"; null; "e3" ]
            Expect.equal (r.ToString()) "Failure: e1; ; e3" "null rendered as empty"
            let w : Returns<int,string> = Success (1, [ null ])
            Expect.stringStarts (w.ToString()) "Success: 1" "no exception on a null warning"
        }

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

let returnsWarnIfTests =
    testList "Returns - warnIf" [

        test "warnIf appends warning when predicate holds" {
            let r = Returns.ok 5 |> Returns.warnIf (fun v -> v > 3) "above threshold"
            match r with
            | Success (v, msgs) ->
                Expect.equal v 5 "value unchanged"
                Expect.equal msgs ["above threshold"] "warning appended"
            | _ -> failtest "Expected Success"
        }

        test "warnIf does not append warning when predicate does not hold" {
            let r = Returns.ok 1 |> Returns.warnIf (fun v -> v > 3) "above threshold"
            match r with
            | Success (v, msgs) ->
                Expect.equal v 1 "value unchanged"
                Expect.equal msgs [] "no warning"
            | _ -> failtest "Expected Success"
        }

        test "warnIf passes Failure through unchanged" {
            let r = Returns.fail "err" |> Returns.warnIf (fun _ -> true) "w"
            Expect.isTrue (isFailure r) "should remain Failure"
            Expect.equal (failureMessages r) ["err"] "error unchanged"
        }

        test "warnIf accumulates on existing warnings" {
            let r = Returns.warn "w1" 5 |> Returns.warnIf (fun v -> v > 3) "w2"
            match r with
            | Success (_, msgs) -> Expect.equal msgs ["w1";"w2"] "both warnings present"
            | _ -> failtest "Expected Success"
        }

        test "warnIfLazy appends built message when predicate holds" {
            let r = Returns.ok 5 |> Returns.warnIfLazy (fun v -> v > 3) (fun () -> "above threshold")
            match r with
            | Success (v, msgs) ->
                Expect.equal v 5 "value preserved"
                Expect.equal msgs ["above threshold"] "warning appended"
            | _ -> failtest "Expected Success"
        }

        test "warnIfLazy does not invoke thunk when predicate does not hold" {
            let mutable called = false
            let r =
                Returns.ok 1
                |> Returns.warnIfLazy (fun v -> v > 3) (fun () -> called <- true; "w")
            match r with
            | Success (v, msgs) ->
                Expect.equal v 1 "value preserved"
                Expect.equal msgs [] "no warning appended"
            | _ -> failtest "Expected Success"
            Expect.isFalse called "thunk should not fire when predicate is false"
        }

        test "warnIfLazy does not invoke thunk on Failure" {
            let mutable called = false
            let r =
                Returns.fail "err"
                |> Returns.warnIfLazy (fun _ -> true) (fun () -> called <- true; "w")
            Expect.isTrue (isFailure r) "should remain Failure"
            Expect.equal (failureMessages r) ["err"] "error unchanged"
            Expect.isFalse called "thunk should not fire on Failure"
        }

        test "warnIfLazy accumulates on existing warnings" {
            let r =
                Returns.warn "w1" 5
                |> Returns.warnIfLazy (fun v -> v > 3) (fun () -> "w2")
            match r with
            | Success (_, msgs) -> Expect.equal msgs ["w1";"w2"] "both warnings present"
            | _ -> failtest "Expected Success"
        }

        test "warnIfWith builds warning from the Success value" {
            let r =
                Returns.ok 5
                |> Returns.warnIfWith (fun v -> v > 3) (fun v -> sprintf "v=%d" v)
            match r with
            | Success (v, msgs) ->
                Expect.equal v 5 "value preserved"
                Expect.equal msgs ["v=5"] "value-aware warning appended"
            | _ -> failtest "Expected Success"
        }

        test "warnIfWith does not invoke builder when predicate does not hold" {
            let mutable called = false
            let r =
                Returns.ok 1
                |> Returns.warnIfWith (fun v -> v > 3) (fun v -> called <- true; sprintf "v=%d" v)
            match r with
            | Success (v, msgs) ->
                Expect.equal v 1 "value preserved"
                Expect.equal msgs [] "no warning appended"
            | _ -> failtest "Expected Success"
            Expect.isFalse called "builder should not fire when predicate is false"
        }

        test "warnIfWith does not invoke builder on Failure" {
            let mutable called = false
            let r : Returns<int,string> =
                Returns.fail "err"
                |> Returns.warnIfWith (fun _ -> true) (fun v -> called <- true; sprintf "v=%d" v)
            Expect.isTrue (isFailure r) "should remain Failure"
            Expect.equal (failureMessages r) ["err"] "error unchanged"
            Expect.isFalse called "builder should not fire on Failure"
        }

        test "warnIfWith accumulates on existing warnings" {
            let r =
                Returns.warn "w1" 5
                |> Returns.warnIfWith (fun v -> v > 3) (fun v -> sprintf "v=%d" v)
            match r with
            | Success (_, msgs) -> Expect.equal msgs ["w1";"v=5"] "both warnings present"
            | _ -> failtest "Expected Success"
        }
    ]

let returnsMapWarningsErrorsTests =
    testList "Returns - mapWarnings / mapErrors" [

        test "mapWarnings transforms warnings on Success" {
            let r = Returns.warn "w" 42 |> Returns.mapWarnings (fun m -> m + "!")
            match r with
            | Success (v, msgs) ->
                Expect.equal v 42 "value unchanged"
                Expect.equal msgs ["w!"] "warning transformed"
            | _ -> failtest "Expected Success"
        }

        test "mapWarnings leaves Failure unchanged" {
            let r = Returns.fail "err" |> Returns.mapWarnings (fun m -> m + "!")
            Expect.equal (failureMessages r) ["err"] "error untouched"
        }

        test "mapErrors transforms errors on Failure" {
            let r = Returns.fail "err" |> Returns.mapErrors (fun m -> "[E] " + m)
            Expect.equal (failureMessages r) ["[E] err"] "error transformed"
        }

        test "mapErrors leaves Success warnings unchanged" {
            let r = Returns.warn "w" 42 |> Returns.mapErrors (fun m -> m + "!")
            match r with
            | Success (v, msgs) ->
                Expect.equal v 42 "value unchanged"
                Expect.equal msgs ["w"] "warning untouched"
            | _ -> failtest "Expected Success"
        }

        test "mapMessages transforms both warnings and errors" {
            let rOk  = Returns.warn "w" 1 |> Returns.mapMessages (fun m -> m + "!")
            let rErr = Returns.fail "e"   |> Returns.mapMessages (fun m -> m + "!")
            match rOk with
            | Success (_, msgs) -> Expect.equal msgs ["w!"] "warning transformed"
            | _ -> failtest "Expected Success"
            Expect.equal (failureMessages rErr) ["e!"] "error transformed"
        }
    ]

let returnsValidateAllTests =
    testList "Returns - validateAll" [

        test "validateAll returns Success when all validators pass" {
            let validators = [
                fun v -> if v > 0   then Returns.ok () else Returns.fail "must be positive"
                fun v -> if v < 100 then Returns.ok () else Returns.fail "must be < 100"
            ]
            let r = Returns.validateAll validators 42
            Expect.isTrue (isSuccess r) "should be Success"
            Expect.equal (successValue r) 42 "original value preserved"
        }

        test "validateAll collects ALL errors when multiple validators fail" {
            // Use validators that can fail independently on the same input
            let validators = [
                fun v -> if v > 0      then Returns.ok () else Returns.fail "must be positive"
                fun v -> if v % 2 = 0  then Returns.ok () else Returns.fail "must be even"
            ]
            let r = Returns.validateAll validators -5
            Expect.isTrue (isFailure r) "should be Failure"
            Expect.equal (failureMessages r) ["must be positive";"must be even"] "both errors collected"
        }

        test "validateAll merges warnings from all passing validators" {
            let validators = [
                fun v -> if v > 50 then Returns.warn "near upper limit" () else Returns.ok ()
                fun v -> if v > 90 then Returns.warn "very high"        () else Returns.ok ()
            ]
            let r = Returns.validateAll validators 95
            match r with
            | Success (v, msgs) ->
                Expect.equal v 95 "value preserved"
                Expect.equal msgs ["near upper limit";"very high"] "all warnings collected"
            | _ -> failtest "Expected Success"
        }

        test "validateAll with empty validator list returns Success" {
            let r = Returns.validateAll [] 42
            Expect.equal (successValue r) 42 "value unchanged"
        }
    ]

let returnsAndBangTests =
    testList "ReturnsBuilder - and! parallel binding" [

        test "and! combines two Successes accumulating warnings" {
            let r = returns {
                let! x = Returns.warn "wx" 3
                and! y = Returns.warn "wy" 4
                return x + y
            }
            match r with
            | Success (v, msgs) ->
                Expect.equal v 7 "values combined"
                Expect.equal msgs ["wx";"wy"] "both warnings present"
            | _ -> failtest "Expected Success"
        }

        test "and! accumulates errors from both branches instead of short-circuiting" {
            let r : Returns<int,string> = returns {
                let! _ = Returns.fail "e1"
                and! _ = Returns.fail "e2"
                return 0
            }
            Expect.equal (failureMessages r) ["e1";"e2"] "both errors accumulated"
        }

        test "and! propagates single failure when only one branch fails" {
            let r : Returns<int,string> = returns {
                let! x = Returns.ok 10
                and! _ = Returns.fail "e1"
                return x
            }
            Expect.equal (failureMessages r) ["e1"] "failure propagated"
        }

        test "and! supports three parallel bindings" {
            let r = returns {
                let! a = Returns.ok 1
                and! b = Returns.ok 2
                and! c = Returns.ok 3
                return a + b + c
            }
            Expect.equal (successValue r) 6 "three values combined"
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

        test "toOption converts Ok to Some" {
            let r = Result.toOption (Result.Ok 5)
            Expect.equal r (Some 5) "should be Some 5"
        }

        test "toOption converts Error to None" {
            let r = Result.toOption (Result.Error "e")
            Expect.equal r None "should be None"
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

        test "foldList accumulates all Ok values" {
            let items = [Result.Ok 1; Result.Ok 2; Result.Ok 3]
            let r = Result.foldList (+) (Result.Ok 0) items
            match r with
            | Result.Ok v -> Expect.equal v 6 "should sum to 6"
            | _ -> failtest "Expected Ok"
        }

        test "foldList accumulates errors from multiple failures" {
            let items : Result<int, string list> list = [Result.Ok 1; Result.Error ["e1"]; Result.Error ["e2"; "e3"]]
            let r = Result.foldList (+) (Result.Ok 0) items
            match r with
            | Result.Error errs -> Expect.equal errs ["e1"; "e2"; "e3"] "all errors should be accumulated"
            | _ -> failtest "Expected Error"
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
// v1.1.0 additions
// ============================================================

let returnsBuilderForLoopTests =
    testList "ReturnsBuilder - for loops" [

        test "for over a list collects the warnings of every iteration, in order" {
            let r = returns {
                for x in [ 1; 2; 3; 4 ] do
                    do! (if x % 2 = 0 then Returns.warn $"even {x}" () else Returns.ok ())
            }
            Expect.equal r (Success ((), [ "even 2"; "even 4" ])) "warnings in iteration order"
        }

        test "for stops at the first failing iteration, keeping earlier warnings before the errors" {
            let visited = ResizeArray()
            let r = returns {
                for x in [ 1; 2; 3; 4 ] do
                    visited.Add x
                    do! (if x = 3 then Returns.fail "three" elif x = 2 then Returns.warn "two" () else Returns.ok ())
            }
            Expect.equal r (Failure [ "two"; "three" ]) "earlier warnings, then the error"
            Expect.equal (List.ofSeq visited) [ 1; 2; 3 ] "iteration 4 never runs"
        }

        test "for over an array, a range and an empty sequence" {
            let total = ref 0
            let r1 = returns { for x in [| 1; 2; 3 |] do total.Value <- total.Value + x }
            let r2 = returns { for x in 1 .. 4 do total.Value <- total.Value + x }
            let r3 : Returns<unit,string> = returns { for _ in Seq.empty<int> do () }
            Expect.equal (r1, r2, r3) (Success ((), []), Success ((), []), Success ((), [])) "all succeed"
            Expect.equal total.Value 16 "every element visited"
        }

        test "for followed by return in the same block" {
            let r = returns {
                for x in [ 1; 2 ] do
                    do! Returns.warn $"w{x}" ()
                return 42
            }
            Expect.equal r (Success (42, [ "w1"; "w2" ])) "loop warnings carried to the result"
        }
    ]

let returnsWarnIfLazyTests =
    testList "Returns - warnIfLazy" [

        test "warnIfLazy does not build the message when the predicate is false" {
            let calls = ref 0
            let r = Returns.ok 1 |> Returns.warnIfLazy (fun v -> v > 3) (fun () -> calls.Value <- calls.Value + 1; "w")
            Expect.equal calls.Value 0 "thunk never invoked"
            Expect.equal r (Returns.ok 1) "input unchanged"
        }

        test "warnIfLazy does not build the message on a Failure" {
            let calls = ref 0
            let r = Returns.fail "err" |> Returns.warnIfLazy (fun _ -> true) (fun () -> calls.Value <- calls.Value + 1; "w")
            Expect.equal calls.Value 0 "thunk never invoked"
            Expect.equal (failureMessages r) ["err"] "error unchanged"
        }

        test "warnIfLazy builds the message exactly once when the predicate is true" {
            let calls = ref 0
            let r = Returns.ok 5 |> Returns.warnIfLazy (fun v -> v > 3) (fun () -> calls.Value <- calls.Value + 1; "w")
            Expect.equal calls.Value 1 "thunk invoked once"
            Expect.equal r (Returns.ok 5 |> Returns.warnIf (fun v -> v > 3) "w") "same result as warnIf"
        }

        test "warnIfLazy matches warnIf on existing warnings" {
            let input = Returns.warn "w1" 5
            Expect.equal
                (input |> Returns.warnIfLazy (fun v -> v > 3) (fun () -> "w2"))
                (input |> Returns.warnIf (fun v -> v > 3) "w2")
                "same result as warnIf"
        }
    ]

let returnsPlainResultTests =
    testList "Returns - ofPlainResult / toPlainResult" [

        test "ofPlainResult wraps Ok into a warning-free Success" {
            Expect.equal (Returns.ofPlainResult (Result.Ok 5) : Returns<int,string>) (Success (5, [])) "clean Success"
        }

        test "ofPlainResult wraps Error into a single-error Failure" {
            Expect.equal (Returns.ofPlainResult (Result.Error "e") : Returns<int,string>) (Failure ["e"]) "single error"
        }

        test "toPlainResult discards warnings" {
            Expect.equal (Returns.toPlainResult (Returns.warn "w" 3)) (Result.Ok 3) "warnings dropped"
        }

        test "toPlainResult keeps every error" {
            let r : Returns<int,string> = Returns.failmany ["e1"; "e2"]
            Expect.equal (Returns.toPlainResult r) (Result.Error ["e1"; "e2"]) "all errors kept"
        }

        test "ofPlainResult then toPlainResult round-trips Ok" {
            let r : Result<int,string> = Result.Ok 7
            Expect.equal (r |> Returns.ofPlainResult |> Returns.toPlainResult) (Result.Ok 7) "round trip"
        }
    ]

let returnsAggregationTests =
    testList "Returns - dedupeWarnings / summariseWarnings" [

        test "dedupeWarnings removes duplicates, preserving first-occurrence order" {
            let r = Returns.warnmany ["b"; "a"; "b"; "c"; "a"; "b"] 1 |> Returns.dedupeWarnings
            Expect.equal r (Success (1, ["b"; "a"; "c"])) "order of first occurrences kept"
        }

        test "dedupeWarnings leaves a Failure unchanged, duplicates included" {
            let r : Returns<int,string> = Returns.failmany ["e"; "e"] |> Returns.dedupeWarnings
            Expect.equal r (Failure ["e"; "e"]) "errors untouched"
        }

        test "summariseWarnings counts warnings per key, keeping the first warning of each key" {
            let r =
                Returns.warnmany [ ("range", 1); ("range", 2); ("mesh", 3); ("range", 4) ] 0
                |> Returns.summariseWarnings fst
            Expect.equal r (Success (0, [ (("range", 1), 3); (("mesh", 3), 1) ])) "grouped by key with counts"
        }

        test "summariseWarnings tolerates keys that are null at runtime (unit, None)" {
            let r = Returns.warnmany ["a"; "b"] 0 |> Returns.summariseWarnings (fun _ -> ())
            Expect.equal r (Success (0, [ ("a", 2) ])) "single group"
        }

        test "summariseWarnings pairs each error of a Failure with 1" {
            let r : Returns<int,string * int> = Returns.failmany ["e"; "e"] |> Returns.summariseWarnings id
            Expect.equal r (Failure [ ("e", 1); ("e", 1) ]) "errors not collapsed"
        }
    ]

let returnsWithContextTests =
    testList "Returns - withContextBy" [

        let inContext (ctx: string) (m: string) = $"{ctx} > {m}"

        test "withContextBy annotates every error" {
            let r : Returns<int,string> = Returns.failmany ["e1"; "e2"] |> Returns.withContextBy inContext "load"
            Expect.equal r (Failure ["load > e1"; "load > e2"]) "all errors annotated"
        }

        test "withContextBy leaves a Success and its warnings unchanged" {
            let r = Returns.warn "w" 1 |> Returns.withContextBy inContext "load"
            Expect.equal r (Success (1, ["w"])) "unchanged"
        }

        test "withContextBy builds a breadcrumb trail, outermost label outermost" {
            let step (_: int) : Returns<int,string> = Returns.fail "negative"
            let inner x = step x |> Returns.withContextBy inContext "node 3"
            let outer x = Returns.ok x >>= inner |> Returns.withContextBy inContext "march"
            Expect.equal (outer 1) (Failure ["march > node 3 > negative"]) "breadcrumb trail"
        }
    ]

let returnsTraverseVariantsTests =
    // Fails on odd numbers, warns on every element.
    let check (x: int) : Returns<int,string> =
        if x % 2 = 0 then Returns.warn $"w{x}" (x * 10) else Returns.fail $"odd {x}"

    testList "Returns - traverseList / traverseListFailFast / traverseArray / traverseArrayFailFast" [

        test "traverseList accumulates all failures, traverseListFailFast stops at the first" {
            let inputs = [1; 2; 3; 4; 5]
            Expect.equal (Returns.traverseList check inputs) (Failure ["odd 1"; "odd 3"; "odd 5"]) "three errors"
            Expect.equal (Returns.traverseListFailFast check inputs) (Failure ["odd 1"]) "one error"
        }

        test "traverseListFailFast does not invoke the function after the first failure" {
            let visited = ResizeArray()
            let f x = visited.Add x; check x
            Returns.traverseListFailFast f [2; 4; 5; 6; 7] |> ignore
            Expect.equal (List.ofSeq visited) [2; 4; 5] "stopped at 5"
        }

        test "traverseListFailFast keeps earlier warnings, followed by the failing element's errors" {
            Expect.equal (Returns.traverseListFailFast check [2; 4; 5; 6]) (Failure ["w2"; "w4"; "odd 5"]) "warnings then error"
        }

        test "traverseListFailFast matches traverseList on all-success input" {
            let inputs = [2; 4; 6]
            Expect.equal (Returns.traverseListFailFast check inputs) (Returns.traverseList check inputs) "same result"
            Expect.equal (Returns.traverseListFailFast check inputs) (Success ([20; 40; 60], ["w2"; "w4"; "w6"])) "values and warnings in order"
        }

        test "traverseArray accumulates all failures, traverseArrayFailFast stops at the first" {
            let inputs = [| 1; 2; 3; 4; 5 |]
            Expect.equal (Returns.traverseArray check inputs) (Failure ["odd 1"; "odd 3"; "odd 5"]) "three errors"
            Expect.equal (Returns.traverseArrayFailFast check inputs) (Failure ["odd 1"]) "one error"
        }

        test "traverseArray and traverseArrayFailFast match their list counterparts" {
            for inputs in [ []; [2; 4; 6]; [2; 3; 4; 5]; [1] ] do
                let arr = Array.ofList inputs
                Expect.equal (Returns.traverseArray check arr |> Returns.map List.ofArray) (Returns.traverseList check inputs) $"accumulating: {inputs}"
                Expect.equal (Returns.traverseArrayFailFast check arr |> Returns.map List.ofArray) (Returns.traverseListFailFast check inputs) $"fail-fast: {inputs}"
        }
    ]

let testingModuleTests =
    // Runs an assertion that is expected to throw, and returns the exception message.
    let messageOf (f: unit -> 'a) =
        try f () |> ignore; failtest "Expected an exception" with
        | e when not (e :? Expecto.AssertException) -> e.Message

    testList "Testing - assertion helpers" [

        test "getOrFail returns the Success value" {
            Expect.equal (Testing.getOrFail (Returns.warn "w" 3)) 3 "value"
        }

        test "getOrFail renders every error message on Failure" {
            let msg = messageOf (fun () -> Testing.getOrFail (Returns.failmany ["first"; "second"; "third"] : Returns<int,string>))
            for part in ["3 error(s)"; "[1] first"; "[2] second"; "[3] third"] do
                Expect.stringContains msg part $"message contains {part}"
        }

        test "getWithWarnings returns the value and warnings" {
            Expect.equal (Testing.getWithWarnings (Returns.warnmany ["a"; "b"] 3)) (3, ["a"; "b"]) "value and warnings"
        }

        test "getWithWarnings throws on Failure" {
            let msg = messageOf (fun () -> Testing.getWithWarnings (Returns.fail "boom" : Returns<int,string>))
            Expect.stringContains msg "boom" "error rendered"
        }

        test "expectFailure returns the errors, and throws on Success" {
            Expect.equal (Testing.expectFailure (Returns.failmany ["e1"; "e2"] : Returns<int,string>)) ["e1"; "e2"] "errors"
            let msg = messageOf (fun () -> Testing.expectFailure (Returns.warn "w" 42))
            Expect.stringContains msg "42" "value rendered"
            Expect.stringContains msg "[1] w" "warning rendered"
        }

        test "expectFailureMatching passes when any error matches" {
            Testing.expectFailureMatching ((=) "e2") (Returns.failmany ["e1"; "e2"] : Returns<int,string>)
        }

        test "expectFailureMatching throws when no error matches, rendering all of them" {
            let msg = messageOf (fun () -> Testing.expectFailureMatching ((=) "zzz") (Returns.failmany ["e1"; "e2"] : Returns<int,string>))
            Expect.stringContains msg "[1] e1" "first error rendered"
            Expect.stringContains msg "[2] e2" "second error rendered"
        }

        test "expectFailureMatching throws on Success" {
            messageOf (fun () -> Testing.expectFailureMatching (fun _ -> true) (Returns.ok 1 : Returns<int,string>)) |> ignore
        }

        test "expectWarningMatching returns the value when a warning matches, and throws otherwise" {
            Expect.equal (Testing.expectWarningMatching ((=) "b") (Returns.warnmany ["a"; "b"] 7)) 7 "value"
            messageOf (fun () -> Testing.expectWarningMatching ((=) "z") (Returns.warnmany ["a"; "b"] 7)) |> ignore
            messageOf (fun () -> Testing.expectWarningMatching (fun _ -> true) (Returns.fail "e" : Returns<int,string>)) |> ignore
        }

        test "expectNoWarnings returns the value of a clean Success, and throws otherwise" {
            Expect.equal (Testing.expectNoWarnings (Returns.ok 9 : Returns<int,string>)) 9 "value"
            let msg = messageOf (fun () -> Testing.expectNoWarnings (Returns.warn "careful" 9))
            Expect.stringContains msg "careful" "warning rendered"
            messageOf (fun () -> Testing.expectNoWarnings (Returns.fail "e" : Returns<int,string>)) |> ignore
        }
    ]

// ============================================================
// v1.2.0: filter / recover (from the unmerged 2026-05 review branch)
// ============================================================


let returnsFilterTests =
    testList "Returns - filter" [

        test "filter passes Success when predicate holds" {
            let r = Returns.ok 10 |> Returns.filter (fun v -> v > 5) "too small"
            Expect.isTrue (isSuccess r) "should remain Success"
            Expect.equal (successValue r) 10 "value unchanged"
        }

        test "filter converts Success to Failure when predicate does not hold" {
            let r = Returns.ok 3 |> Returns.filter (fun v -> v > 5) "too small"
            Expect.isTrue (isFailure r) "should be Failure"
            Expect.equal (failureMessages r) ["too small"] "error message set"
        }

        test "filter keeps earlier warnings after the error, like a failing >>= step" {
            let r = Returns.warn "w" 3 |> Returns.filter (fun v -> v > 5) "too small"
            Expect.equal r (Failure ["too small"; "w"]) "error first, then the former warnings"
            Expect.equal r (Returns.warn "w" 3 >>= (fun _ -> Returns.fail "too small")) "same as a failing bind"
        }

        test "filterWith builds the message from the value, only when the check fails" {
            let calls = ref 0
            let build v = calls.Value <- calls.Value + 1; $"{v} is too small"
            let passed = Returns.warn "w" 10 |> Returns.filterWith (fun v -> v > 5) build
            Expect.equal passed (Success (10, ["w"])) "passes unchanged"
            Expect.equal calls.Value 0 "builder not invoked when the check passes"
            let failed = Returns.warn "w" 3 |> Returns.filterWith (fun v -> v > 5) build
            Expect.equal failed (Failure ["3 is too small"; "w"]) "built message, then former warnings"
            Expect.equal calls.Value 1 "builder invoked once"
            let already = Returns.fail "e" |> Returns.filterWith (fun _ -> false) build
            Expect.equal already (Failure ["e"]) "Failure unchanged"
            Expect.equal calls.Value 1 "builder not invoked on a Failure"
        }

        test "filter propagates Failure unchanged" {
            let r = Returns.fail "original" |> Returns.filter (fun _ -> true) "never"
            Expect.equal (failureMessages r) ["original"] "original error preserved"
        }

        test "filter can be chained with bind" {
            let r =
                Returns.ok 10
                |> Returns.bind (fun v -> Returns.ok (v * 2))
                |> Returns.filter (fun v -> v < 100) "result too large"
            Expect.equal (successValue r) 20 "value correct"
        }
    ]

let returnsRecoverTests =
    testList "Returns - recover" [

        test "recover converts Failure to Success via compensation" {
            let r = Returns.fail "err" |> Returns.recover (fun _ -> Returns.ok 0)
            Expect.isTrue (isSuccess r) "should be Success after recovery"
            Expect.equal (successValue r) 0 "recovered value"
        }

        test "recover receives the error messages list" {
            let r = Returns.failmany ["e1";"e2"] |> Returns.recover (fun errs -> Returns.ok (List.length errs))
            Expect.equal (successValue r) 2 "error count used as recovered value"
        }

        test "recover passes Success through unchanged" {
            let r = Returns.warn "w" 99 |> Returns.recover (fun _ -> Returns.ok 0)
            match r with
            | Success (v, msgs) ->
                Expect.equal v 99 "original value preserved"
                Expect.equal msgs ["w"] "original warning preserved"
            | _ -> failtest "Expected Success"
        }

        test "recover can return a new Failure from compensation" {
            let r = Returns.fail "original" |> Returns.recover (fun errs -> Returns.fail (sprintf "recovered: %s" errs.[0]))
            Expect.equal (failureMessages r) ["recovered: original"] "compensation failure propagated"
        }

        test "recover is useful as fallback in a pipeline" {
            let parse (s: string) =
                match System.Int32.TryParse(s) with
                | true, v -> Returns.ok v
                | _       -> Returns.fail (sprintf "cannot parse '%s'" s)
            let r = parse "bad" |> Returns.recover (fun _ -> Returns.ok -1)
            Expect.equal (successValue r) -1 "fallback value used"
        }

        test "recover does not invoke the compensation on Success" {
            let calls = ref 0
            Returns.ok 1 |> Returns.recover (fun _ -> calls.Value <- calls.Value + 1; Returns.ok 0) |> ignore
            Expect.equal calls.Value 0 "compensation not invoked"
        }

        test "recover can flag the fallback with a warning instead of hiding it" {
            let r = Returns.fail "sensor offline" |> Returns.recover (fun errs -> Returns.warn ("used default: " + String.concat "; " errs) 20.0)
            Expect.equal r (Success (20.0, ["used default: sensor offline"])) "fallback value with an explanatory warning"
        }
    ]

// ============================================================
// Coverage tests ported from the unmerged 2026-05 review branch
// ============================================================
// These tests use Result's Ok/Error, which `open ROP.Validation` (ValidationState.Ok) shadows at file level;
// re-opening FSharp.Core inside this module restores them. (In the original branch they never compiled.)
module Ported =
    open Microsoft.FSharp.Core

    // ============================================================
    // Returns - traverseList / sequenceList
    // ============================================================

    let returnsTraverseListTests =
        testList "Returns - traverseList / sequenceList" [

            test "traverseList maps all successes into a list in order" {
                let r = Returns.traverseList (fun x -> Returns.ok (x * 2)) [1;2;3]
                Expect.equal (successValue r) [2;4;6] "values doubled and ordered"
            }

            test "traverseList accumulates ALL failures (no short-circuit)" {
                let f x = if x > 0 then Returns.ok x else Returns.fail (sprintf "bad:%d" x)
                let r = Returns.traverseList f [1; -1; 2; -2]
                Expect.isTrue (isFailure r) "should be Failure"
                Expect.equal (failureMessages r) ["bad:-1";"bad:-2"] "both errors collected"
            }

            test "traverseList merges warnings from all successful elements" {
                let f x = Returns.warn (sprintf "w%d" x) (x * 10)
                let r = Returns.traverseList f [1;2;3]
                match r with
                | Success (vs, msgs) ->
                    Expect.equal vs [10;20;30] "values mapped"
                    Expect.equal msgs ["w1";"w2";"w3"] "all warnings merged in order"
                | _ -> failtest "Expected Success"
            }

            test "traverseList on empty list returns Success of empty list" {
                let r = Returns.traverseList Returns.ok ([] : int list)
                match r with
                | Success (vs, []) -> Expect.equal vs [] "empty list"
                | _ -> failtest "Expected Success []"
            }

            test "sequenceList sequences all successes into a list" {
                let items = [ Returns.ok 1; Returns.ok 2; Returns.ok 3 ]
                let r = Returns.sequenceList items
                Expect.equal (successValue r) [1;2;3] "should be [1;2;3]"
            }

            test "sequenceList accumulates failures from multiple elements" {
                let items : Returns<int,string> list =
                    [ Returns.ok 1; Returns.fail "e1"; Returns.ok 3; Returns.fail "e2" ]
                let r = Returns.sequenceList items
                Expect.equal (failureMessages r) ["e1";"e2"] "both errors collected"
            }

            test "sequenceList merges warnings from all elements" {
                let items = [ Returns.warn "w1" 1; Returns.ok 2; Returns.warn "w2" 3 ]
                let r = Returns.sequenceList items
                match r with
                | Success (vs, msgs) ->
                    Expect.equal vs [1;2;3] "values preserved"
                    Expect.equal msgs ["w1";"w2"] "warnings merged"
                | _ -> failtest "Expected Success"
            }
        ]

    // ============================================================
    // Returns - log
    // ============================================================

    let returnsLogTests =
        testList "Returns - log" [

            test "log calls logger with Success message when record is true" {
                let mutable logged = ""
                let r = Returns.ok 42 |> Returns.log (fun s -> logged <- s) true "step"
                Expect.stringContains logged "Success" "logged message mentions Success"
                Expect.stringContains logged "step" "logged message contains label"
                Expect.equal (successValue r) 42 "returns propagated unchanged"
            }

            test "log calls logger with Failure message when record is true" {
                let mutable logged = ""
                let r = Returns.fail "err" |> Returns.log (fun s -> logged <- s) true "step"
                Expect.stringContains logged "Failure" "logged message mentions Failure"
                Expect.isTrue (isFailure r) "returns unchanged"
            }

            test "log skips logger when record is false" {
                let mutable count = 0
                Returns.ok 1 |> Returns.log (fun _ -> count <- count + 1) false "step" |> ignore
                Expect.equal count 0 "logger not called when record is false"
            }

            test "log propagates the returns value unchanged" {
                let r = Returns.warn "w" 7 |> Returns.log (fun _ -> ()) true "step"
                match r with
                | Success (v, msgs) ->
                    Expect.equal v 7 "value unchanged"
                    Expect.equal msgs ["w"] "warning unchanged"
                | _ -> failtest "Expected Success"
            }
        ]

    // ============================================================
    // Result.Extension — map2 / map3 / map4 / mapError / flatten / merge / zip / partition
    // ============================================================

    let resultExtMapAdvancedTests =
        testList "Result.Extension - map2 / map3 / map4 / mapError / flatten / merge / zip / partition" [

            test "map2 combines two Ok values" {
                let r = Result.map2 (fun a b -> a + b) (Ok 3) (Ok 4)
                match r with
                | Ok v -> Expect.equal v 7 "should be 7"
                | _ -> failtest "Expected Ok"
            }

            test "map2 propagates first Error" {
                let r : Result<int,string> = Result.map2 (fun a b -> a + b) (Error "e1") (Ok 4)
                match r with
                | Error e -> Expect.equal e "e1" "first error"
                | _ -> failtest "Expected Error"
            }

            test "map2 propagates second Error when first is Ok" {
                let r : Result<int,string> = Result.map2 (fun a b -> a + b) (Ok 3) (Error "e2")
                match r with
                | Error e -> Expect.equal e "e2" "second error"
                | _ -> failtest "Expected Error"
            }

            test "map3 combines three Ok values" {
                let r = Result.map3 (fun a b c -> a + b + c) (Ok 1) (Ok 2) (Ok 3)
                match r with
                | Ok v -> Expect.equal v 6 "should be 6"
                | _ -> failtest "Expected Ok"
            }

            test "map3 propagates Error from any argument" {
                let r : Result<int,string> = Result.map3 (fun a b c -> a + b + c) (Ok 1) (Error "e") (Ok 3)
                match r with
                | Error e -> Expect.equal e "e" "error propagated"
                | _ -> failtest "Expected Error"
            }

            test "map4 combines four Ok values" {
                let r = Result.map4 (fun a b c d -> a + b + c + d) (Ok 1) (Ok 2) (Ok 3) (Ok 4)
                match r with
                | Ok v -> Expect.equal v 10 "should be 10"
                | _ -> failtest "Expected Ok"
            }

            test "mapError transforms Error value" {
                let r = Result.mapError (fun e -> sprintf "[E] %s" e) (Error "bad")
                match r with
                | Error e -> Expect.equal e "[E] bad" "error transformed"
                | _ -> failtest "Expected Error"
            }

            test "mapError leaves Ok unchanged" {
                let r = Result.mapError (fun _ -> "never") (Ok 42)
                match r with
                | Ok v -> Expect.equal v 42 "unchanged"
                | _ -> failtest "Expected Ok"
            }

            test "flatten collapses nested Ok" {
                let r = Result.flatten (Ok (Ok 42))
                match r with
                | Ok v -> Expect.equal v 42 "unwrapped"
                | _ -> failtest "Expected Ok"
            }

            test "flatten propagates inner Error" {
                let r = Result.flatten (Ok (Error "inner"))
                match r with
                | Error e -> Expect.equal e "inner" "inner error"
                | _ -> failtest "Expected Error"
            }

            test "flatten propagates outer Error" {
                let r : Result<int,string> = Result.flatten (Error "outer")
                match r with
                | Error e -> Expect.equal e "outer" "outer error"
                | _ -> failtest "Expected Error"
            }

            test "merge combines two Ok values" {
                let r = Result.merge (+) (@) (Ok 3) (Ok 4)
                match r with
                | Ok v -> Expect.equal v 7 "7"
                | _ -> failtest "Expected Ok"
            }

            test "merge propagates first Error when second is Ok" {
                let r : Result<int,string list> = Result.merge (+) (@) (Error ["e1"]) (Ok 4)
                match r with
                | Error es -> Expect.equal es ["e1"] "first error"
                | _ -> failtest "Expected Error"
            }

            test "merge concatenates errors from two Errors" {
                let r = Result.merge (+) (@) (Error ["e1"]) (Error ["e2"])
                match r with
                | Error es -> Expect.equal es ["e1";"e2"] "both errors"
                | _ -> failtest "Expected Error"
            }

            test "zip combines two Ok values into a tuple" {
                let r = Result.zip (Ok 1) (Ok 2)
                match r with
                | Ok (a, b) ->
                    Expect.equal a 1 "first"
                    Expect.equal b 2 "second"
                | _ -> failtest "Expected Ok"
            }

            test "zip propagates first Error" {
                let r : Result<int*int,string> = Result.zip (Error "e") (Ok 2)
                match r with
                | Error e -> Expect.equal e "e" "first error"
                | _ -> failtest "Expected Error"
            }

            test "partition separates Ok and Error" {
                let items : Result<int,string> list = [ Ok 1; Error "e1"; Ok 2; Error "e2" ]
                let (oks, errs) = Result.partition items
                Expect.equal oks [1;2] "ok values"
                Expect.equal errs ["e1";"e2"] "error values"
            }

            test "partition all Oks" {
                let (oks, errs) = Result.partition [ Ok 1; Ok 2 ]
                Expect.equal oks [1;2] "all oks"
                Expect.equal errs [] "no errors"
            }

            test "partition all Errors" {
                let (oks, errs) : int list * string list = Result.partition [ Error "e1"; Error "e2" ]
                Expect.equal oks [] "no oks"
                Expect.equal errs ["e1";"e2"] "all errors"
            }
        ]

    // ============================================================
    // Result.Extension — fold / foldList
    // ============================================================

    let resultExtFoldTests =
        testList "Result.Extension - fold / foldList" [

            test "fold accumulates Ok values" {
                let r = ROP.Result.fold (+) (Ok 0) [ Ok 1; Ok 2; Ok 3 ]
                match r with
                | Ok v -> Expect.equal v 6 "should be 6"
                | _ -> failtest "Expected Ok"
            }

            test "fold stops accumulating on first Error (keeps first Error)" {
                let r = ROP.Result.fold (+) (Ok 0) [ Ok 1; Error "e"; Ok 3 ]
                match r with
                | Error e -> Expect.equal e "e" "first error kept"
                | _ -> failtest "Expected Error"
            }

            test "fold on empty sequence returns initial state" {
                let r = ROP.Result.fold (+) (Ok 99) []
                match r with
                | Ok v -> Expect.equal v 99 "initial state"
                | _ -> failtest "Expected Ok"
            }

            test "foldList accumulates Ok values" {
                let r = Result.foldList (+) (Ok 0) [ Ok 1; Ok 2; Ok 3 ]
                match r with
                | Ok v -> Expect.equal v 6 "should be 6"
                | _ -> failtest "Expected Ok"
            }

            test "foldList accumulates ALL errors across failures" {
                let r = Result.foldList (+) (Ok 0) [ Ok 1; Error ["e1"]; Ok 3; Error ["e2"] ]
                match r with
                | Error es -> Expect.equal es ["e1";"e2"] "both errors collected"
                | _ -> failtest "Expected Error"
            }

            test "foldList on empty sequence returns initial Ok state" {
                let r = Result.foldList (+) (Ok 0) []
                match r with
                | Ok v -> Expect.equal v 0 "initial state"
                | _ -> failtest "Expected Ok"
            }
        ]

    // ============================================================
    // Result.Extension — eitherTee / successTee / failureTee / tee / log
    // ============================================================

    let resultExtTeeLogTests =
        testList "Result.Extension - tee / eitherTee / successTee / failureTee / log" [

            test "tee executes side effect and returns original value" {
                let mutable seen = 0
                let result = Result.tee (fun v -> seen <- v) 42
                Expect.equal result 42 "value returned"
                Expect.equal seen 42 "side effect executed"
            }

            test "eitherTee calls fOk on Ok" {
                let mutable called = false
                let r = Ok 5 |> Result.eitherTee (fun _ -> called <- true) ignore
                Expect.isTrue called "fOk called"
                Expect.equal r (Ok 5) "propagated unchanged"
            }

            test "eitherTee calls fError on Error" {
                let mutable called = false
                let r = Error "e" |> Result.eitherTee ignore (fun _ -> called <- true)
                Expect.isTrue called "fError called"
                Expect.equal r (Error "e") "propagated unchanged"
            }

            test "successTee only executes on Ok" {
                let mutable count = 0
                Ok 1 |> Result.successTee (fun _ -> count <- count + 1) |> ignore
                Error "e" |> Result.successTee (fun _ -> count <- count + 1) |> ignore
                Expect.equal count 1 "called only once"
            }

            test "failureTee only executes on Error" {
                let mutable count = 0
                Ok 1 |> Result.failureTee (fun _ -> count <- count + 1) |> ignore
                Error "e" |> Result.failureTee (fun _ -> count <- count + 1) |> ignore
                Expect.equal count 1 "called only once"
            }

            test "log calls logger on Ok when record is true" {
                let mutable logged = ""
                let r = Ok 42 |> Result.log (fun s -> logged <- s) true "label"
                Expect.stringContains logged "Ok" "mentions Ok"
                Expect.stringContains logged "label" "contains label"
                Expect.equal r (Ok 42) "propagated unchanged"
            }

            test "log calls logger on Error when record is true" {
                let mutable logged = ""
                let r = Error "bad" |> Result.log (fun s -> logged <- s) true "label"
                Expect.stringContains logged "Error" "mentions Error"
                Expect.isTrue (Result.isError r) "propagated unchanged"
            }

            test "log skips logger when record is false" {
                let mutable count = 0
                Ok 1 |> Result.log (fun _ -> count <- count + 1) false "label" |> ignore
                Expect.equal count 0 "not called"
            }
        ]

    // ============================================================
    // Result.Extension — compose / >>= / >=> / protect
    // ============================================================

    let resultExtComposeTests =
        testList "Result.Extension - compose / >>= / >=> / protect" [

            test "compose chains two switch functions" {
                let f1 v = Ok (v + 1)
                let f2 v = Ok (v * 2)
                let r = Result.compose f1 f2 3
                match r with
                | Ok v -> Expect.equal v 8 "(3+1)*2=8"
                | _ -> failtest "Expected Ok"
            }

            test "compose propagates first Error" {
                let f1 _ = Error "step1"
                let f2 v = Ok (v * 2)
                let r = Result.compose f1 f2 5
                match r with
                | Error e -> Expect.equal e "step1" "first error"
                | _ -> failtest "Expected Error"
            }

            test "compose propagates second Error" {
                let f1 v = Ok (v + 1)
                let f2 _ = Error "step2"
                let r = Result.compose f1 f2 5
                match r with
                | Error e -> Expect.equal e "step2" "second error"
                | _ -> failtest "Expected Error"
            }

            test ">>= operator chains Result (via Result.bind)" {
                // Result.Operators.>>= is shadowed by Returns.Operators.>>= so we call bind directly;
                // the operator itself works the same way — this tests the underlying semantics.
                let r = Ok 5 |> Result.bind (fun v -> Ok (v + 1))
                match r with
                | Ok v -> Expect.equal v 6 "should be 6"
                | _ -> failtest "Expected Ok"
            }

            test ">>= propagates Error (via Result.bind)" {
                let r : Result<int,string> = Error "e" |> Result.bind (fun v -> Ok (v + 1))
                match r with
                | Error e -> Expect.equal e "e" "error propagated"
                | _ -> failtest "Expected Error"
            }

            test ">=> operator composes in series (via Result.compose)" {
                let f1 v = Ok (v + 1)
                let f2 v = Ok (v * 2)
                let r = Result.compose f1 f2 3
                match r with
                | Ok v -> Expect.equal v 8 "8"
                | _ -> failtest "Expected Ok"
            }

            test "protect returns Ok when function succeeds" {
                let r = Result.protect (fun x -> x + 1) 5
                match r with
                | Ok v -> Expect.equal v 6 "should be 6"
                | _ -> failtest "Expected Ok"
            }

            test "protect returns Error when function throws" {
                let r = Result.protect (fun _ -> failwith "boom") 0
                match r with
                | Error _ -> ()
                | _ -> failtest "Expected Error"
            }
        ]

    // ============================================================
    // Integration — complex object construction and calculation
    // ============================================================

    // Domain types for integration tests (must be at module level in F#)
    type Widget = { Width: float; Height: float; Depth: float }
    type Part   = { Mass: float; Length: float }
    type Vessel = { Pressure: float; Temperature: float; Volume: float }

    let integrationComplexTests =
        testList "Integration - Complex object construction and calculation pipeline" [

            test "applicative <!>/<*> collects ALL field errors for complex type" {
                let validatePos name v : Returns<float,string> =
                    if v > 0.0 then Returns.ok v else Returns.fail (sprintf "%s must be positive" name)
                let r =
                    fun w h d -> { Width = w; Height = h; Depth = d }
                    <!> validatePos "Width"  -1.0
                    <*> validatePos "Height" -2.0
                    <*> validatePos "Depth"   3.0
                Expect.equal (failureMessages r) ["Width must be positive";"Height must be positive"] "all errors"
            }

            test "CE and! accumulates errors from independent sub-validators" {
                let validateMass v : Returns<float,string> =
                    if v > 0.0 then Returns.ok v else Returns.fail "Mass must be positive"
                let validateLen v : Returns<float,string> =
                    if v > 0.0 then Returns.ok v else Returns.fail "Length must be positive"
                let build m l = returns {
                    let! vm = validateMass m
                    and! vl = validateLen l
                    return { Mass = vm; Length = vl }
                }
                let r = build -1.0 -2.0
                Expect.equal (failureMessages r) ["Mass must be positive";"Length must be positive"] "both errors"
            }

            test "validateAll collects all errors and warnings from a record" {
                let checkP (v:Vessel) = if v.Pressure > 0.0    then Returns.ok () else Returns.fail "Pressure <= 0"
                let checkT (v:Vessel) = if v.Temperature > 0.0  then Returns.ok () else Returns.fail "Temperature <= 0"
                let warnHighP (v:Vessel) = if v.Pressure > 100.0 then Returns.warn "High pressure" () else Returns.ok ()
                let validate = Returns.validateAll [ checkP; checkT; warnHighP ]
                let r = validate { Pressure = -5.0; Temperature = -10.0; Volume = 1.0 }
                Expect.equal (failureMessages r) ["Pressure <= 0";"Temperature <= 0"] "both errors"
            }

            test "traverseList validates a list of sub-components collecting all errors" {
                let validateComponent (v:float) : Returns<float,string> =
                    if v > 0.0 then Returns.ok v else Returns.fail (sprintf "component %.1f invalid" v)
                let components = [1.0; -2.0; 3.0; -4.0]
                let r = Returns.traverseList validateComponent components
                Expect.equal (failureMessages r) ["component -2.0 invalid";"component -4.0 invalid"] "all errors"
            }

            test "warnIf appends warning during calculation without stopping the pipeline" {
                let calcArea diameter length =
                    Returns.ok (System.Math.PI * diameter * length)
                    |> Returns.warnIf (fun a -> a < 1.0) "Area below recommended minimum"
                let r = calcArea 0.01 0.5
                match r with
                | Success (v, msgs) ->
                    Expect.isGreaterThan v 0.0 "positive area"
                    Expect.equal msgs ["Area below recommended minimum"] "warning present"
                | _ -> failtest "Expected Success with warning"
            }

            test "mapWarnings and mapErrors annotate messages independently" {
                let withWarn = Returns.warn "raw warning" 42.0
                let withErr  = Returns.fail<float,string> "raw error"
                let annotated = withWarn |> Returns.mapWarnings (fun m -> "[WARN] " + m)
                let annotatedErr = withErr |> Returns.mapErrors (fun m -> "[ERR] " + m)
                match annotated with
                | Success (_, msgs) -> Expect.equal msgs ["[WARN] raw warning"] "warning annotated"
                | _ -> failtest "Expected Success"
                Expect.equal (failureMessages annotatedErr) ["[ERR] raw error"] "error annotated"
            }

            test "full pipeline: parse -> validate -> compute -> warn" {
                let parse (s:string) : Returns<float,string> =
                    match System.Double.TryParse(s) with
                    | true, v -> Returns.ok v
                    | _ -> Returns.fail (sprintf "Cannot parse '%s'" s)
                let validatePositive v : Returns<float,string> =
                    if v > 0.0 then Returns.ok v
                    else Returns.fail "Value must be positive"
                let compute v = Returns.ok (v * v)
                let pipeline s =
                    parse s
                    >>= validatePositive
                    >>= compute
                    |> Returns.warnIf (fun v -> v > 1000.0) "Large squared value"
                Expect.equal (successValue (pipeline "5")) 25.0 "5^2 = 25"
                Expect.equal (successValue (pipeline "40"))  1600.0 "40^2 = 1600"
                Expect.isTrue (pipeline "40" |> Returns.hasWarnings) "warning for large value"
                Expect.isTrue (pipeline "-3" |> isFailure) "negative fails"
                Expect.isTrue (pipeline "abc" |> isFailure) "non-numeric fails"
            }

            test "sequenceList collects valid and invalid items into single result" {
                let validated = [ Returns.ok 1.0; Returns.fail "e1"; Returns.warn "w" 3.0; Returns.fail "e2" ]
                let r = Returns.sequenceList validated
                Expect.equal (failureMessages r) ["e1";"e2"] "failures accumulated"
            }

            test "CE returnFrom and sequential bind with warnings in each step" {
                let step1 v = Returns.warn "w1" (v + 1)
                let step2 v = Returns.warn "w2" (v * 2)
                let step3 v = Returns.ok   (v - 1)
                let r = returns {
                    let! a = step1 3    // 4, w1
                    let! b = step2 a    // 8, w2 (w1 already held)
                    return! step3 b     // 7
                }
                match r with
                | Success (v, msgs) ->
                    Expect.equal v 7 "final value"
                    Expect.contains msgs "w1" "w1 present"
                    Expect.contains msgs "w2" "w2 present"
                | _ -> failtest "Expected Success"
            }
        ]

// ============================================================
// Performance rewrites: equivalence with the previous implementations
// ============================================================

/// Verbatim copies of the implementations before the performance rewrite, used as oracles: every rewritten
/// combinator must return exactly what its previous version returned, on every combination of inputs.
module Reference =

    // bind / jointMessages as they were before commit 019d2f0 (through `either` with local lambdas).
    let jointMessages (messages: 'm list) (returns: Returns<'s,'m>) =
        let fSuccess (x, msgs) = Success (x, msgs @ messages)
        let fFailure errs = Failure (errs @ messages)
        Returns.either fSuccess fFailure returns

    let bind (f: 'a -> Returns<'b,'m>) (returns: Returns<'a,'m>) =
        let fSuccess (s, msgs) = f s |> jointMessages msgs
        let fFailure msgs = Failure msgs
        Returns.either fSuccess fFailure returns

    let map2 f r1 r2 = Returns.ok f |> Returns.apply <| r1 |> Returns.apply <| r2
    let map3 f r1 r2 r3 = Returns.ok f |> Returns.apply <| r1 |> Returns.apply <| r2 |> Returns.apply <| r3
    let map4 f r1 r2 r3 r4 = Returns.ok f |> Returns.apply <| r1 |> Returns.apply <| r2 |> Returns.apply <| r3 |> Returns.apply <| r4

    let andAnd f1 f2 = Returns.plus (fun s1 _ -> s1) (fun e1 e2 -> e1 @ e2) f1 f2

    let fold folder (state: Returns<'S,'M>) (returns: Returns<'T,'M> seq) =
        let revAppend xs ys = List.fold (fun acc x -> x :: acc) ys xs
        let mutable acc =
            match state with
            | Success (s, msgs) -> Choice1Of2 (s, List.rev msgs)
            | Failure msgs       -> Choice2Of2 (List.rev msgs)
        for r in returns do
            acc <-
                match acc, r with
                | Choice1Of2 (s, msgsRev), Success (v, msgs) -> Choice1Of2 (folder s v, revAppend msgs msgsRev)
                | Choice2Of2 errsRev,      Success _         -> Choice2Of2 errsRev
                | Choice1Of2 _,            Failure errs      -> Choice2Of2 (List.rev errs)
                | Choice2Of2 errsRev,      Failure errs      -> Choice2Of2 (revAppend errs errsRev)
        match acc with
        | Choice1Of2 (s, msgsRev) -> Success (s, List.rev msgsRev)
        | Choice2Of2 errsRev      -> Failure (List.rev errsRev)

    let traverseList (f: 'a -> Returns<'b,'m>) (inputs: 'a list) : Returns<'b list,'m> =
        let revAppend xs ys = List.fold (fun acc x -> x :: acc) ys xs
        let folder state current =
            match state, f current with
            | Success (acc, msgsRev), Success (v, msgs) -> Success (v :: acc, revAppend msgs msgsRev)
            | Failure errsRev,        Success _         -> Failure errsRev
            | Success _,              Failure errs      -> Failure (List.rev errs)
            | Failure errsRev,        Failure errs      -> Failure (revAppend errs errsRev)
        match List.fold folder (Success ([], [])) inputs with
        | Success (acc, msgsRev) -> Success (List.rev acc, List.rev msgsRev)
        | Failure errsRev        -> Failure (List.rev errsRev)

    let traverseListFailFast (f: 'a -> Returns<'b,'m>) (inputs: 'a list) : Returns<'b list,'m> =
        let revAppend xs ys = List.fold (fun acc x -> x :: acc) ys xs
        let rec loop valuesRev msgsRev remaining =
            match remaining with
            | [] -> Success (List.rev valuesRev, List.rev msgsRev)
            | x :: rest ->
                match f x with
                | Success (v, msgs) -> loop (v :: valuesRev) (revAppend msgs msgsRev) rest
                | Failure errs      -> Failure (List.rev msgsRev @ errs)
        loop [] [] inputs

    let validateAll (validators: ('a -> Returns<unit,'m>) list) (value: 'a) : Returns<'a,'m> =
        let results = validators |> List.map (fun v -> v value)
        let errors  = results |> List.collect (fun r -> match r with | Failure msgs -> msgs | _ -> [])
        let warns   = results |> List.collect (fun r -> match r with | Success (_, msgs) -> msgs | _ -> [])
        match errors with
        | [] -> Success (value, warns)
        | _  -> Failure errors

/// Every shape a single Returns can take that matters to the combinators: clean Success, Success with one or two
/// warnings, Failure with one or two errors, and the degenerate Failure [] (not produced by the library's own
/// constructors, but constructible, so the rewrites must treat it exactly as before).
let shapes (tag: string) (value: int) : Returns<int,string> list =
    [ Success (value, [])
      Success (value, [ tag + "w1" ])
      Success (value, [ tag + "w1"; tag + "w2" ])
      Failure [ tag + "e1" ]
      Failure [ tag + "e1"; tag + "e2" ]
      Failure [] ]

/// All lists of length 0..maxLen whose elements are drawn from `shapes`, each element tagged by its position.
let rec shapeLists maxLen : Returns<int,string> list list =
    if maxLen = 0 then [ [] ]
    else
        let shorter = shapeLists (maxLen - 1)
        [ yield! shorter
          for tail in shorter |> List.filter (fun l -> l.Length = maxLen - 1) do
              for head in shapes $"[{maxLen}]" maxLen do
                  yield head :: tail ]

let performanceEquivalenceTests =
    testList "Performance rewrites - equivalence with previous implementations" [

        test "map2 / map3 / map4 match the apply chain on every combination" {
            for a in shapes "a" 1 do
                for b in shapes "b" 2 do
                    Expect.equal (Returns.map2 (+) a b) (Reference.map2 (+) a b) $"map2 {a} {b}"
                    for c in shapes "c" 3 do
                        Expect.equal (Returns.map3 (fun x y z -> x + 10*y + 100*z) a b c)
                                     (Reference.map3 (fun x y z -> x + 10*y + 100*z) a b c) $"map3 {a} {b} {c}"
                        for d in shapes "d" 4 do
                            Expect.equal (Returns.map4 (fun x y z w -> x + y + z + w) a b c d)
                                         (Reference.map4 (fun x y z w -> x + y + z + w) a b c d) $"map4 {a} {b} {c} {d}"
        }

        test "jointMessages matches the previous implementation on every shape and message list" {
            for a in shapes "a" 1 do
                for extra in [ []; [ "x1" ]; [ "x1"; "x2" ] ] do
                    Expect.equal (Returns.jointMessages extra a) (Reference.jointMessages extra a) $"jointMessages {extra} {a}"
        }

        test "bind / >>= / compose / let! match the previous bind on every combination of two steps" {
            for a in shapes "a" 1 do
                for b in shapes "b" 2 do
                    for c in shapes "c" 3 do
                        let f (x: int) = b |> Returns.map ((+) x)
                        let g (x: int) = c |> Returns.map ((*) x)
                        let expected = a |> Reference.bind f |> Reference.bind g
                        Expect.equal (a |> Returns.bind f |> Returns.bind g) expected $"bind {a} {b} {c}"
                        Expect.equal (a >>= f >>= g) expected $">>= {a} {b} {c}"
                        Expect.equal (a |> Returns.bind (f >=> g)) (a |> Reference.bind (fun x -> f x |> Reference.bind g)) $">=> {a} {b} {c}"
                        let ce = returns { let! x = a
                                           let! y = f x
                                           let! z = g y
                                           return z }
                        Expect.equal ce expected $"let! {a} {b} {c}"
        }

        test "map matches apply (ok f) on every shape" {
            for a in shapes "a" 1 do
                Expect.equal (Returns.map ((*) 2) a) (Returns.apply (Returns.ok ((*) 2)) a) $"map {a}"
        }

        test "&&& matches plus-based composition on every combination" {
            for a in shapes "a" 1 do
                for b in shapes "b" 2 do
                    Expect.equal ((fun _ -> a) &&& (fun _ -> b) <| ()) (Reference.andAnd (fun _ -> a) (fun _ -> b) ()) $"&&& {a} {b}"
        }

        test "and! (Bind2Return / Bind3Return) match MergeSources + BindReturn semantics" {
            for a in shapes "a" 1 do
                for b in shapes "b" 2 do
                    let r2 = returns { let! x = a
                                       and! y = b
                                       return x + 10*y }
                    Expect.equal r2 (Reference.map2 (fun x y -> x + 10*y) a b) $"and! x2 {a} {b}"
                    for c in shapes "c" 3 do
                        let r3 = returns { let! x = a
                                           and! y = b
                                           and! z = c
                                           return x + 10*y + 100*z }
                        Expect.equal r3 (Reference.map3 (fun x y z -> x + 10*y + 100*z) a b c) $"and! x3 {a} {b} {c}"
        }

        test "fold matches the previous implementation on every list up to length 3, for every seed" {
            for seed in shapes "s" 0 do
                for l in shapeLists 3 do
                    Expect.equal (Returns.fold (+) seed l) (Reference.fold (+) seed l) $"fold {seed} {l}"
        }

        test "traverseList / sequenceList / traverseListFailFast match the previous implementations up to length 3" {
            for l in shapeLists 3 do
                Expect.equal (Returns.traverseList id l) (Reference.traverseList id l) $"traverseList {l}"
                Expect.equal (Returns.sequenceList l) (Reference.traverseList id l) $"sequenceList {l}"
                Expect.equal (Returns.traverseListFailFast id l) (Reference.traverseListFailFast id l) $"traverseListFailFast {l}"
        }

        test "traverseArray / traverseArrayFailFast match their list counterparts, including Failure []" {
            for l in shapeLists 3 do
                let arr = Array.ofList l
                Expect.equal (Returns.traverseArray id arr |> Returns.map List.ofArray) (Returns.traverseList id l) $"traverseArray {l}"
                Expect.equal (Returns.traverseArrayFailFast id arr |> Returns.map List.ofArray) (Returns.traverseListFailFast id l) $"traverseArrayFailFast {l}"
        }

        test "validateAll matches the previous implementation on every list of up to 3 validators" {
            for l in shapeLists 3 do
                let validators = l |> List.map (fun r -> fun (_: int) -> r |> Returns.map ignore)
                Expect.equal (Returns.validateAll validators 42) (Reference.validateAll validators 42) $"validateAll {l}"
        }

        test "mapWarnings / failOnWarnings / dedupeWarnings / summariseWarnings are unchanged on every shape" {
            for a in shapes "a" 1 do
                let expectedMapWarnings = match a with Success (v, m) -> Success (v, List.map String.length m |> List.map string) | f -> f
                Expect.equal (Returns.mapWarnings (String.length >> string) a) expectedMapWarnings $"mapWarnings {a}"
                let expectedFailOn = match a with Success (_, (_ :: _ as m)) -> Failure m | r -> r
                Expect.equal (Returns.failOnWarnings a) expectedFailOn $"failOnWarnings {a}"
                let expectedDedupe = match a with Success (v, m) -> Success (v, List.distinct m) | f -> f
                Expect.equal (Returns.dedupeWarnings a) expectedDedupe $"dedupeWarnings {a}"
                let expectedSummary =
                    match a with
                    | Success (v, m) -> Success (v, m |> List.groupBy id |> List.map (fun (_, g) -> List.head g, g.Length))
                    | Failure e -> Failure (e |> List.map (fun x -> x, 1))
                Expect.equal (Returns.summariseWarnings id a) expectedSummary $"summariseWarnings {a}"
        }

        test "Validation: string length/emptiness validators read string.Length and agree with Seq on other sequences" {
            let run v (x: 'a) = v "p" x
            for s in [ ""; " "; "abc"; String.replicate 50 "x"; String.replicate 51 "x" ] do
                let asSeq = s |> Seq.toList   // same characters, through the generic Seq path
                Expect.equal (run (hasMaxLengthOf 50) s) (run (hasMaxLengthOf 50) asSeq) $"hasMaxLengthOf {s.Length}"
                Expect.equal (run (hasMinLengthOf 3) s)  (run (hasMinLengthOf 3) asSeq)  $"hasMinLengthOf {s.Length}"
                Expect.equal (run (hasLengthOf 3) s)     (run (hasLengthOf 3) asSeq)     $"hasLengthOf {s.Length}"
                Expect.equal (run isNotEmpty s)          (run isNotEmpty asSeq)          $"isNotEmpty {s.Length}"
                Expect.equal (run isEmpty s)             (run isEmpty asSeq)             $"isEmpty {s.Length}"
            Expect.equal (run isNotEmpty (null: string)) (Errors [ { message = "Must not be null"; property = "p"; errorCode = "isNotEmpty" } ]) "null string"
        }

        test "Validation: inline comparison validators behave as before on ints, floats and strings" {
            Expect.equal ((isGreaterThan 0.0) "h" 1.8) Ok "float >"
            Expect.equal ((isGreaterThan 0.0) "h" 0.0) (Errors [ { message = "Must be greater than 0"; property = "h"; errorCode = "isGreaterThan" } ]) "float > fails"
            Expect.equal ((isLessThan 150) "a" 149) Ok "int <"
            Expect.equal ((isLessThanOrEqualTo 3) "a" 4) (Errors [ { message = "Must have a maximum value of 3"; property = "a"; errorCode = "isLessThanOrEqualTo" } ]) "int <= fails"
            Expect.equal ((isEqualTo "x") "s" "x") Ok "string ="
            Expect.equal ((isNotEqualTo "x") "s" "x") (Errors [ { message = "Must not be equal to x"; property = "s"; errorCode = "isNotEqualTo" } ]) "string <> fails"
        }

        test "Validation: a validator returning Errors [] still counts as valid (unchanged edge case)" {
            let v = createValidatorFor<int>() {
                validate (fun x -> x) [ (fun _ _ -> Errors []) ]
            }
            Expect.equal (v 1) Ok "empty error list is Ok"
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
            returnsWarnIfTests
            returnsMapWarningsErrorsTests
            returnsValidateAllTests
            returnsAndBangTests
            returnsBuilderForLoopTests
            returnsWarnIfLazyTests
            returnsPlainResultTests
            returnsAggregationTests
            returnsWithContextTests
            returnsTraverseVariantsTests
            testingModuleTests
            performanceEquivalenceTests
            returnsFilterTests
            returnsRecoverTests
            Ported.returnsTraverseListTests
            Ported.returnsLogTests
            Ported.resultExtMapAdvancedTests
            Ported.resultExtFoldTests
            Ported.resultExtTeeLogTests
            Ported.resultExtComposeTests
            Ported.integrationComplexTests
            PerformanceTests.performanceTests
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
