module Demo

open NBomber.FSharp
open NBomber.Plugins.Http
open FsHttp.DslCE

let runnerDemo reportingSink =
    testSuite "demo suite" {
        scenario "empty scenario" { noWarmUp }

        scenario "demo scenario" {
            step "usual step" {
                execute ignore
                doNotTrack
            }

            httpStep "http step demo" {
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

                check (fun resp -> int resp.StatusCode = 204) "wrong response code"
            }

            noWarmUp
        }

        testName "demo test"
        reportHtml
    }
