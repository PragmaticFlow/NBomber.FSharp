module Demo

open Hopac
open NBomber.FSharp
open NBomber.FSharp.Http
open NBomber.FSharp.Hopac
open FsHttp.DslCE
open FSharp.Json
open FSharp.Control.Tasks.V2.ContextInsensitive

type Token =
    { access_token : string
      expires_in : int
      token_type : string
    }


let runnerDemo reportingSink =
    testSuite "demo suite" {
        // NOTE scenario without steps doesn't compile
        // scenario "empty scenario" { noWarmUp }

        scenario "demo scenario" {
            step "regular action step" {
                execute ignore
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
                create(fun ctx ->
                    httpMsg {
                        POST "https://reqres.in/api/users"
                        CacheControl "no-cache"
                        BearerAuth (string ctx.Data.["access_token"])

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
        reportHtml
        reporter reportingSink
    }
