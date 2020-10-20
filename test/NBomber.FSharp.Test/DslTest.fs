module NBomber.FSharp.DslTest

open System.Net.WebSockets

open System
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open Hopac
open FSharp.Json
open FsHttp.DslCE
open NBomber
open NBomber.Contracts
open NBomber.FSharp
open NBomber.FSharp.Hopac
open NBomber.Plugins.Http

/// Dummy user record
type User = { Id: Guid; UserName: string }

let ms = milliseconds

let delay (time: TimeSpan) (logger: Serilog.ILogger) =
    task {
        logger.Information("start wait {time}", time)
        do! Task.Delay time
        logger.Information("end wait {time}", time)
    }

let stepBuilderTest () =
    let data =
        FeedData.fromJson<int> "jsonPath"
        |> Feed.createCircular "none"

    let conns =
        let url = ""
        ConnectionPoolArgs.create(
            name = "websockets pool",
            openConnection = (fun (_nr, cancel) -> task {
                let ws = new ClientWebSocket()
                do! ws.ConnectAsync(Uri url, cancel)
                return ws
            }),
            closeConnection = (fun (ws: ClientWebSocket, cancel) -> task {
                do! ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancel)
            }))

    let steps =
        [ step "task step" {
              feed data
              connectionPool conns
              execute (fun _ -> Response.Ok() |> Task.FromResult)
              doNotTrack
          }
          step "async step" {
              doNotTrack
              execute (fun _ -> async { return Response.Ok() })
          }
          step "job step" {
              execute (fun _ -> job { return Response.Ok() })
          }
          step "wait 100" {
              execute (fun ctx -> delay (ms 100) ctx.Logger)
          }
          step "wait 10" {
              execute (fun ctx -> delay (ms 10) ctx.Logger)
          }
          step "wait 0" {
              execute (fun _ -> Task.CompletedTask)
          }
          step "wait pause 100" {
              pause (seconds 100)
          }

          httpStep "all http features" {
              httpMsg {
                  GET "https://people.com"
                  Header "x-requestid" "like above but not random"
                  Header "other-header" "other value"
                  BearerAuth """{"access_token":"bla","expires_in":3600,"token_type":"weird"}"""
                  transformHttpRequestMessage (fun req -> req)
              }
              version "1.0"
              prepare (fun _ctx _req -> ())
              prepare (fun _ctx _req -> Task.FromResult())
              logRequest
              logResponse
              handle (fun _ctx _req -> ())
              handle (fun _ctx _req -> Task.FromResult())
              handle (fun ctx resp -> task {
                  let! users = resp.Content.ReadAsStringAsync()
                  ctx.Data.["users"] <- users |> Json.deserialize<User list> |> box
              })

              check (fun _resp -> true)  "error message"
              check (fun _resp -> Task.FromResult true)  "error message"
          }

          httpStep "GET homepage" {
            httpMsg {
              GET "https://nbomber.com/user/"
            }
            prepare (fun _ctx _req -> ())
            prepare (fun _ctx _req -> Task.FromResult())
            prepare (fun ctx req ->
              let users = ctx.Data.["users"] :?> User list
              let userId = users.[0].Id
              req.RequestUri <- req.RequestUri.AbsoluteUri + string userId |> Uri)
          }

          httpStep "POST json" {
            httpMsg {
                POST "http://nbomber.com"
                body
                json """{"data": 42}"""
            }
          }

          httpStep "create a message" {
              new Net.Http.HttpRequestMessage()
          }
          httpStep "create a message task" {
              create (new Net.Http.HttpRequestMessage() |> Task.FromResult)
          }
          httpStep "create a message from context" {
              create (fun _ctx -> new Net.Http.HttpRequestMessage())
          }
          httpStep "create a message task from context" {
              create (fun _ctx ->

                    new Net.Http.HttpRequestMessage() |> Task.FromResult)
          }

          httpStep "FsHttp" {
              httpMsg {
                POST "https://reqres.in/api/users"
                CacheControl "no-cache"
                body
                json """
                {
                    "name": "morpheus",
                    "job": "leader"
                }
                """
              }
          }
        //   httpStep "yield FsHttp" {
        //       httpMsg { GET "https://google.com" }
        //   }
        ]

    steps

let scenarioBuilderTest = [
    scenario "implicit yield steps" {
        step "dummy step 1" {
            execute (fun ctx -> delay (ms 100) ctx.Logger)
        }
        step "dummy step 2" {
            execute (fun ctx -> delay (ms 100) ctx.Logger)
        }
    }
    scenario "test delays" {
        warmUp (seconds 5)
        noWarmUp


        init(fun ctx -> task {
            ctx.Logger.Information "init scenario task"
        })
        clean(fun ctx -> ctx.Logger.Information "cleanup after scenario action")

        load [ KeepConstant(10, seconds 100)
               RampConstant(100, seconds 100)
             ]

        // implicit yield step
        // step "wait 100" {
        //     execute (fun ctx -> delay (ms 100) ctx.Logger)
        // }
        // custom op steps
        steps [
            step "wait 100" {
                execute (fun ctx -> delay (ms 100) ctx.Logger)
            }
            step "pause 100 s" {
                pause (seconds 100) // TODO ScenarioBuilder.Pause duplicates StepBuilder.Pause
            }

            step "wait 10" {
                execute (fun ctx -> delay (ms 10) ctx.Logger)
            }
            step "wait 0" {
                execute (fun _ -> Task.CompletedTask)
            }
        ]
    }
]

let runnerBuilderTest reportingSink =
    testSuite "Suite name" {
        testName "Test name"
        scenarios scenarioBuilderTest

        noReports
        reporter reportingSink
        reportHtml
        reportTxt
        reportCsv
        reportMd
        reportMd // used twice is useless - has no changes
        noReports // but this removes them all again
        reportInterval (seconds 5)

        config "loadTestConfig.json"
        infraConfig "infrastructureConfig.json"

        plugins []
        plugins []

        runProcess
        runConsole
    }

let simplifiedRunner =
    testSuite "simplified" {
        step "dummy step" {
            execute ignore
        }
    }
