module NBomber.FSharp.Expecto

open System
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open Expecto
open Expecto.Flip
open NBomber.Configuration
open NBomber.Contracts

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

let sim = [ InjectPerSec(20, seconds 40 )
            KeepConstant(50, seconds 100) ]

[<Tests>]
let scenarioTests =
    testList "scenario" [
        test "defaults" {
            let scn = scenario "default" {
                step1
            }

            scn.Steps
            |> List.map (fun s -> s.StepName)
            |> Expect.sequenceEqual "wrong step names"
                ["step 1"]

            scn.WarmUpDuration
            |> Expect.equal "wrong warmup duration "
                (seconds 30)

            scn.LoadSimulations
            |> Expect.equal "wrong load simulations" [ InjectPerSec(50, minutes 1) ]
            scn.Init
            |> Expect.isNone "scenario init action is not set"
            scn.Clean
            |> Expect.isNone "scenario clean action is not set"
        }

        test "steps list" {
            let scn = scenario "scenario 1" {
                load sim
                init ignore
                clean ignore
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

            scn.LoadSimulations
            |> Expect.equal "wrong load simulations" sim
            scn.Init
            |> Expect.isSome "scenario init action is not set"
            scn.Clean
            |> Expect.isSome "scenario clean action is not set"
        }

        test "steps yields" {
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

[<Tests>]
let runnerTests =
    testList "runner" [
        test "reportcontext" {
            let dummySink =
                { new IReportingSink with
                    member _.SinkName = "empty"
                    member _.SaveFinalStats _ = Task.CompletedTask
                    member _.SaveRealtimeStats _ = Task.CompletedTask
                    member _.Init(_,_) = ()
                    member _.Start _ = Task.CompletedTask
                    member _.Stop() = Task.CompletedTask
                    member _.Dispose() = () }
            let ctx = report {
                folderName "folder name"
                html
                interval (seconds 22)
                csv
                sink dummySink
                markdown

            }
            ctx.Formats
            |> Expect.wantSome "formats not set"
            |> Expect.equal "wrong formats list" [ ReportFormat.Md
                                                   ReportFormat.Csv
                                                   ReportFormat.Html ]
            ctx.Sinks
            |> Expect.equal "wrong reporter sinks" [ dummySink ]
            ctx.Interval
            |> Expect.equal "wrong report interval" (seconds 22)
            ctx.FolderName
            |> Expect.wantSome "folder name not set"
            |> Expect.equal "wrong folder name" "folder name"
            ctx.FileName
            |> Expect.isNone "file name should be not set"
        }

        test "reports" {
            let ctx =
                testSuite "test suite" {
                    report { html }
                    scenarios [
                        scenario "empty scenario" {
                            step "empty step" {
                                execute ignore
                            }
                        }
                    ]
                }
            ctx.ReportFormats
            |> Expect.equal "wrong report format list" [ ReportFormat.Html ]

            ctx.RegisteredScenarios.Length
            |> Expect.equal "wrong scenarios count" 1
            let steps =
                ctx.RegisteredScenarios
                |> List.head
                |> (fun s -> s.Steps)
            steps.Length
            |> Expect.equal "wrong steps count" 1
        }
        test "runner" {
            testSuite "empty suite" {
                report {
                    html
                    folderName "empty"
                    fileName "empty"
                }
                scenarios [
                    scenario "empty scenario" {
                        load [ InjectPerSec(1000, seconds 30) ]
                        step "empty step" {
                            execute ignore
                        }
                    }
                ]
                runWithExitCode
            } |> Expect.equal "error exit code" 0
        }
    ]

[<EntryPoint>]
let main argv =
    Tests.runTestsInAssembly defaultConfig argv
