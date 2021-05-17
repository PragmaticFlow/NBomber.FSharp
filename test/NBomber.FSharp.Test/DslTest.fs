module NBomber.FSharp.DslTest

open System.Net.WebSockets

open System
open System.Threading.Tasks
open FSharp.Control.Tasks.NonAffine
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
type ID = ID of int

type DummyConnection =
    { Nr: int
      IsOpen: bool
    }
    member __.Open =
        { __ with IsOpen = true }
    member __.OpenTask (ctx: IBaseContext) = task {
        return { __ with IsOpen = true }
    }
    member __.OpenAsync (ctx: IBaseContext) = async {
        return { __ with IsOpen = true }
    }
    member __.Close (ctx: IBaseContext) = task {
        return { __ with IsOpen = false }
    }

let ``connection pool connect changes type of connection``(): IClientFactory<DummyConnection> =
    clients """operation "connect" changes the type of connection pool""" {

        connect (fun _ -> 42 |> Task.FromResult)
        disconnect (printfn "disconnect %i")

        connect (fun _ -> 42.0 |> Task.FromResult)
        disconnect (printfn "disconnect %f")

        connect (fun _ _ -> ID 42 |> Task.FromResult)
        disconnect (fun (ID i) -> printfn "disconnect %i" i)

        connect (fun _ -> async { return 42 })
        connect (fun _ -> async { return ID 42 })
        connect (fun nr ctx -> async {
            let c = { Nr = nr; IsOpen = false }
            let! isOpen = c.OpenAsync ctx
            return isOpen
        })
        disconnect (fun c ctx -> task {
            let! _closed = c.Close ctx
            return()
        })
    }

let clientsTest(): IClientFactory<ID> =
    clients "connection pool overloads" {
        count 42
        count 24

        connect (fun _ -> 42 |> Task.FromResult)
        connect (fun _ -> async { return 42 })
        connect (fun _ -> job { return 42 })

        connect (fun _ _ -> 42 |> Task.FromResult)
        connect (fun _ _ -> async { return 42 })
        connect (fun _ _ -> job { return 42 })

        connect (fun _ _ -> ID 42 |> Task.FromResult)
        connect (fun _ _ -> ID 42 |> Task.FromResult)
        connect (fun _ -> async { return 42 })
        connect (fun _ -> async { return ID 42 })


        disconnect ignore
        disconnect (fun _ -> Task.CompletedTask)
        disconnect (fun _ -> task  { return () })
        disconnect (fun _ -> async { return () })
        disconnect (fun _ -> job   { return () })

        disconnect (fun _ _ -> ())
        disconnect (fun _ _ -> Task.CompletedTask)
        disconnect (fun _ _ -> task  { return () })
        disconnect (fun _ _ -> async { return () })
        disconnect (fun _ _ -> job   { return () })

    }

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
        clients "websockets" {
            count 100

            connect (fun _nr ctx -> task {
              let ws = new ClientWebSocket()
              do! ws.ConnectAsync(Uri "web.socket.url", ctx.CancellationToken)
              return ws
            })

            disconnect (fun ws ctx -> task {
              do! ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ctx.CancellationToken)
            })
        }

    let steps =
        [
          step "response task step" {
              execute (fun _ -> Response.ok() |> Task.FromResult)
          }
          step "response async step" {
              execute (fun _ -> async { return Response.ok() })
          }
          step "response job step" {
              execute (fun _ -> job { return Response.ok() })
          }
          step "response step" {
            execute (fun _ -> Response.ok())
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
            execute ignore
          }

          step "wait 100" {
              execute (fun ctx -> delay (ms 100) ctx.Logger)
          }
          step "wait 10" {
              dataFeed data
              conns
              execute (fun ctx -> delay (ms 10) ctx.Logger)
              doNotTrack
              timeout (seconds 1)
          }

          step "wait pause 100" {
              pause (seconds 100)
          }

          step "right types from feed and connections" {
              dataFeed data
              conns
              execute (fun ctx ->
                  let takeBoth (_: Guid) (_: ClientWebSocket) = ""
                  ctx.Logger.Information("Can take feed and connection {Ret}", takeBoth ctx.FeedItem ctx.Client) )
          }

          httpStep "yield request message, can't use dataFeed and clients" {
              new System.Net.Http.HttpRequestMessage(Method = System.Net.Http.HttpMethod.Get)
              doNotTrack
          }

          httpStep "create request message with context, with dataFeed and connnectionPool" {
              dataFeed data
              conns
              execute (fun ctx -> new System.Net.Http.HttpRequestMessage(Method = System.Net.Http.HttpMethod.Get))
              doNotTrack
          }

          httpStep "all http features" {
              dataFeed data
              //   clients conns
              // NOTE this does not compile if there is dataFeed or clients
              //   httpMsg {
              //     GET "https://nbomber.com/user/"
              //   }
              execute(fun ctx ->
                httpMsg {
                  GET ("https://people.com/" + string ctx.FeedItem) // $"https://people.com %A{ctx.FeedItem}"
                  Header "x-requestid" "like above but not random"
                  Header "other-header" "other value"
                  AuthorizationBearer """{"access_token":"bla","expires_in":3600,"token_type":"weird"}"""
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
              execute (new Net.Http.HttpRequestMessage() |> Task.FromResult)
          }
          httpStep "create a message from context" {
              execute (fun _ctx -> new Net.Http.HttpRequestMessage())
          }
          httpStep "create a message task from context" {
              execute (fun _ctx ->
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
              execute (fun _ -> Response.ok() |> Task.FromResult)
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

        // init overloads without steps
        init ignore
        init (fun _ -> Task.CompletedTask)
        init (fun _ -> Task.FromResult())
        // clean overloads without steps
        clean ignore
        clean (fun _ -> Task.CompletedTask)
        clean (fun _ -> Task.FromResult())

        load []
        load Seq.empty
        load [||]

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

        // now the same but with non empty scenario (with steps)

        warmUp (seconds 5)
        noWarmUp

        // init overload with steps
        init ignore
        init (fun _ -> Task.CompletedTask)
        init (fun _ -> Task.FromResult())

        // clean overloads with steps
        clean ignore
        clean (fun _ -> Task.CompletedTask)
        clean (fun _ -> Task.FromResult())

        load []
        load Seq.empty
        load [||]
    }
]

let runnerBuilderTest reportingSink =
    testSuite "Suite name" {
        report {
            csv
            text
            markdown
            html
            sink reportingSink
            interval (seconds 10)
            folderName "reportsFolder"
            fileName "reportFile"
        }
        noReports

        testName "Test name"
        scenarios scenarioBuilderTest

        config "loadTestConfig.json"
        infraConfig "infrastructureConfig.json"

        plugins []

        runConsole
    }

let yieldScenario =
    testSuite "suite 1" {
        testName "testName"
        scenario "scenario 1" {
            step "step 1" { execute ignore }
            step "step 2" { execute ignore }
        }
        plugins []
        report { html }
        runProcess
    }

let yieldStep: Scenario =
    scenario "bla" {
        noWarmUp
        step "dummy step 1" {
            execute ignore
        }
        noWarmUp
        step "dummy step 2" {
            execute ignore
        }
    }

let main' (_argv: string[]): int =
    testSuite "" {
        runWithExitCode
    }

let main'' (argv: string[]): int =
    testSuite "" {
        runWithArgs argv
    }
