module Demo

open Hopac
open NBomber.FSharp
open NBomber.FSharp.Http
open NBomber.FSharp.Hopac
open FsHttp.DslCE
open FSharp.Json
open FSharp.Control.Tasks.NonAffine
open System
open System.Net.WebSockets

type Token =
    { access_token : string
      expires_in : int
      token_type : string
    }
// just for demo. actual entry point is Main.main
//[<EntryPoint>]
let main' (argv: string[]) : int =
    testSuite "demo suite" {
        report {
            html
        }

        scenario "demo scenario" {

            step "regular action step" {
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
                execute (fun ctx -> ctx.Logger.Information "start regular action")
                doNotTrack
            }

            step "hopac job step" {
                execute(fun ctx -> job {
                    ctx.Logger.Information "Start hopac job"
                })
            }

            httpStep "user authorization" {
                httpMsg {
                    POST "https://reqres.in/api/token"

                    body
                    formUrlEncoded [
                        "grant_type", "password"
                        "username", "eve.holt@reqres.in"
                        "password", "pistol"
                        "scope", "openid"
                        "client_id", "client42"
                        "client_secret", "client_secret.XYZ!§$%&"
                    ]
                }
                handle (fun ctx _resp -> task {
                    let! content = _resp.Content.ReadAsStringAsync()
                    let token = content |> Json.deserialize<Token>
                    ctx.Data.["access_token"] <- token.access_token
                })
            }

            httpStep "http step demo" {
                execute(fun ctx ->
                    httpMsg {
                        POST "https://reqres.in/api/users"
                        CacheControl "no-cache"
                        AuthorizationBearer (string ctx.Data.["access_token"])

                        body
                        json """
                        {
                            "name": "morpheus",
                            "job": "leader"
                        }
                        """
                })

                check "response code should be 204 Content"
                    (fun resp -> int resp.StatusCode = 204)
                check "response ContentLength should be set to 0"
                    (fun resp -> resp.Content.Headers.ContentLength.HasValue &&
                                 resp.Content.Headers.ContentLength.Value = 0L)
            }

            noWarmUp
        }

        testName "demo test"
        runWithArgs argv
    }
