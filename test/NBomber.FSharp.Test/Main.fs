module NBomber.FSharp.Expecto

open System
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open Expecto
open Expecto.Flip

let delay (time: TimeSpan) (logger: Serilog.ILogger) =
    task {
        logger.Information("start wait {time}", time)
        do! Task.Delay time
        logger.Information("end wait {time}", time)
    }

let ms = milliseconds

let step1 =
    step "step 1" {
        execute (fun ctx -> delay (ms 100) ctx.Logger)
    }
let step2 =
    step "step 2" {
        execute (fun ctx -> delay (ms 100) ctx.Logger)
    }
let step3 =
    step "step 3" {
        execute (fun ctx -> delay (ms 100) ctx.Logger)
    }

[<Tests>]
let tests =
  testList "samples" [
    test "scenario with steps list" {
        let scn = scenario "scenario 1" {
            warmUp (seconds 42)
            steps [ step1; step2; step3 ]
        }

        scn.Steps
        |> List.map (fun s -> s.StepName)
        |> Expect.sequenceEqual "wrong step names"
            ["step 1"; "step 2"; "step 3"]

        scn.WarmUpDuration
        |> Expect.equal "wrong warmup duration "
            (seconds 42)
    }

    test "scenario with steps yields" {
        let scn = scenario "scenario 1" {
            step1
            step2
            step3
            warmUp (seconds 42)
        }

        scn.Steps
        |> List.map (fun s -> s.StepName)
        |> Expect.sequenceEqual "wrong step names"
            ["step 1"; "step 2"; "step 3"]

        scn.WarmUpDuration
        |> Expect.equal "wrong warmup duration"
            (seconds 42)
    }
  ]

[<EntryPoint>]
let main argv =
    Tests.runTestsInAssembly defaultConfig argv
