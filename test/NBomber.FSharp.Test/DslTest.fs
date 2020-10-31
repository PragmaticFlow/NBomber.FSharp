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
open NBomber.FSharp.Http

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
        FeedData.fromJson<Guid> "jsonPath"
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
        [
          step "response task step" {
              execute (fun _ -> Response.Ok() |> Task.FromResult)
          }
          step "response async step" {
              execute (fun _ -> async { return Response.Ok() })
          }
          step "response job step" {
              execute (fun _ -> job { return Response.Ok() })
          }
          step "response step" {
            execute (fun _ -> Response.Ok())
          }

          step "task step" {
              execute (fun _ -> Task.CompletedTask)
          }
          step "unit task step" {
              execute (fun _ -> Task.FromResult())
          }
          step "unit async step" {
              execute (fun _ -> async { return () })
          }
          step "unit job step" {
              execute (fun _ -> job { return () })
          }
          step "unit step" {
            execute (fun _ -> ())
          }

          step "wait 100" {
              execute (fun ctx -> delay (ms 100) ctx.Logger)
          }
          step "wait 10" {
              dataFeed data
              connectionPool conns
              execute (fun ctx -> delay (ms 10) ctx.Logger)
              doNotTrack
          }

          step "wait pause 100" {
              pause (seconds 100)
          }

          step "right types from feed and connections" {
              dataFeed data
              connectionPool conns
              execute (fun ctx ->
                  let takeBoth (_: Guid) (_: ClientWebSocket) = ""
                  ctx.Logger.Information("Can take feed and connection {Ret}", takeBoth ctx.FeedItem ctx.Connection) )
          }

          httpStep "all http features" {
              dataFeed data
              //   connectionPool conns
              // NOTE this does not compile if there is dataFeed or connectionPool
              //   httpMsg {
              //     GET "https://nbomber.com/user/"
              //   }
              create(fun ctx ->
                httpMsg {
                  GET (sprintf "https://people.com %A" ctx.FeedItem)
                  Header "x-requestid" "like above but not random"
                  Header "other-header" "other value"
                  BearerAuth """{"access_token":"bla","expires_in":3600,"token_type":"weird"}"""
                  transformHttpRequestMessage id
              })

              version "1.0"
              prepare (fun _ctx _req -> ())
              prepare (fun _ctx _req -> Task.FromResult())
              logRequest
              logResponse
              handle (fun _ctx _resp -> ())
              handle (fun _ctx _resp -> Task.FromResult())
              handle (fun ctx resp -> task {
                  let! users = resp.Content.ReadAsStringAsync()
                  ctx.Data.["users"] <- users |> Json.deserialize<User list> |> box
              })

              check "error message 1" (fun _resp -> true)
              check "error message 2" (fun _resp -> Task.FromResult true)
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
          step "allow mixing regular and http steps and pauses" {
              execute (fun _ -> Response.Ok() |> Task.FromResult)
          }
          step "pause 100 s" {
            pause (seconds 100)
          }
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
                pause (seconds 100)
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
        report {
            csv
            text
            markdown
            html
            // TODO
            // formats [ NBomber.Configuration.ReportFormat.Html ]
            sink reportingSink
            interval (seconds 10)
            folderName "reports"
            fileName "reportFile"
        }
        noReports

        testName "Test name"
        scenarios scenarioBuilderTest

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
