module NBomber.FSharp.Demo

open System.Net.WebSockets
open NBomber
open NBomber.Contracts
open NBomber.FSharp
open NBomber.FSharp.Hopac
open NBomber.Plugins.Http
open System
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open Hopac
open Serilog
open FSharp.Json

/// Dummy user record
type User = { Id: Guid; UserName: string }

let ms = milliseconds

let delay (time: TimeSpan) (logger: ILogger) =
    task {
        logger.Information("start wait {time}", time)
        do! Task.Delay time
        logger.Information("end wait {time}", time)
    }

let stepBuilderDemo () =
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
              pause 100
          }

          httpStep "all http features" {
              request "GET" "https://people.com"
              version "1.0"
              xRequestId
              header "x-requestid" "like above but not random"

              headers [ "name1", "value1"
                        "name2", "value2" ]

              bearerToken """{"access_token":"bla","expires_in":3600,"token_type":"weird"}"""
              withRequest (fun _ctx _req -> ())
              withRequest (fun _ctx _req -> Task.FromResult())
              logRequest
              logResponse
              withResponse (fun _ctx _req -> ())
              withResponse (fun _ctx _req -> Task.FromResult())
              withResponse (fun ctx resp -> task {
                  let! users = resp.Content.ReadAsStringAsync()
                  ctx.Data.["users"] <- users |> Json.deserialize<User list> |> box
              })

              check (fun resp -> int resp.StatusCode < 300)
              check (fun _resp -> Task.FromResult true)
          }

          httpStep "GET homepage" {
              GET "https://nbomber.com/user/"
              withRequest (fun _ctx _req -> ())
              withRequest (fun _ctx _req -> Task.FromResult())
              withRequest (fun ctx req ->
                  let users = ctx.Data.["users"] :?> User list
                  let userId = users.[0].Id
                  req.RequestUri <- req.RequestUri.AbsoluteUri + string userId |> Uri)
          }

          httpStep "POST json" { POST "http://nbomber.com" """{"data": 42}""" } ]

    steps

let scenarioBuilderDemo =
    scenario "test delays" {
        warmUp (seconds 5)
        noWarmUp

        pause (seconds 100) // TODO duplicates StepBuilder.pause

        init(fun ctx -> task {
            ctx.Logger.Information "init scenario task"
        })
        clean(fun ctx -> ctx.Logger.Information "cleanup after scenario action")

        load [ KeepConstant(10, seconds 100)
               RampConstant(100, seconds 100)
             ]

        steps [
            step "wait 100" {
                execute (fun ctx -> delay (ms 100) ctx.Logger)
            }
            step "wait 10" {
                execute (fun ctx -> delay (ms 10) ctx.Logger)
            }
            step "wait 0" {
                execute (fun _ -> Task.CompletedTask)
            }
        ]
    }

let runnerBuilderDemo reportingSink =
    testSuite "Suite name" {
        testName "Test name"
        scenarios [ scenarioBuilderDemo ]

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