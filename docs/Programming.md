# NBomber.FSharp


- NBomber.FSharp is a set of computation expressions (CE) for NBomber to simplify test configuration
- NBomber.FSharp.Http extends above package with CEs for use with http test steps
- NBomber.FSharp.Hopac In both above packages for all operations like test execution or connection creation and cleanup you can use synchron functions, or wrapping result in `Async<'T>` or `Task<'T>`. This package also allows to use Hopac's `Job<'T>`

## Test runner

The NBomber test suite can be defined like this:

```fsharp
testSuite "suite name" {
    testName "test name"

    scenarios [
        scenario "scenario name" {
            steps [
                step "step name" {
                    execute(fun ctx -> task {
                        ctx.Logger.Information "hello from test"
                        return Response.Ok()
                    })
                }
            ]
        }
    ]
}
```

That is a lot of code for just one test. Fortunately almost all configuration options are optional. We'll cut some corners.

- `testName` is optional
- `scenarios` and steps are optional too. Write just `scenario "name"` or even `step "name"` if you use just one of them.
- return `unit` if you don't need Response or Tasks at all.

```fsharp
testSuite "just one step" {
    step "the only step" {
        execute(fun _ -> Task.CompletedTask)
    }
}
```

## Scenario configuration

Scenario is only valid if it has at least one `step` defined. Here are some options you can define for it:

```fsharp
scenario "scenario name" {
    warmUp(seconds 20) // default to 20 seconds if you omit this line
    noWarmUp // no warm up at all

    // functions to call at scenario creating and after the tests are finished
    init (fun ctx -> Task.CompletedTask)
    clean (fun ctx -> Task.CompletedTask)

    load [
        InjectPerSec(rate = 10, during = seconds 10)
        InjectPerSec(rate = 20, during = seconds 20)
    ]

    // scenario CE doesn't compile without a single `step` or list of `steps`
    steps [
        // define steps here
    ]
}
```

## Reporter

NBomber can create reports in different formats or you can define own using custom reporter plugins. Define your sink either defining a type, which implements `IReportingSink` or do it inline, using object expression:

```fsharp
/// implement your own reporter sink
let reportingSink =
    { new IReportingSink with
        member _.SinkName = "My custom reporting sink"
        member _.Init(context, infraConfig) = Task.CompletedTask
        member _.Start(testInfo) = Task.CompletedTask
        member _.Stop() = Task.CompletedTask
        member _.SaveRealtimeStats(scenarioStats) = Task.CompletedTask
        member _.SaveFinalStats(nodeStats) = Task.CompletedTask
        member _.Dispose() = ()
    }
```

Following example shows how you can define built-in reporter formats sinks and reporting interval time:

```fsharp
report {
    // specify built-in reporter formats
    csv
    text
    markdown
    html

    // optionally specify where reports are to save
    folderName "reportsFolder"
    fileName "reportFile"

    // use custom reporter sink(s)
    sink reportingSink
    interval (seconds 10)
}
```

use the report computation expression right in the root of testSuite

```fsharp
// use it in your runner configuration
testSuite "test and report" {
    report {
        html
        // other reporter options ...
    }

    // or specify to not produce reports at all
    noReports
}
```

## Connection Pool

There is an `IClientFactory<'T>` interface for creating client pools. It is a little verbose for the cases where you just define a number of connections and function to create one connection. For such cases there is a clients CE.

```fsharp
clients "pool of 42s" {
    count 42
    connect (fun i ctx -> async { return 42 })
    disconnect (fun conn ctx -> async { return () })
}
```

The connect and disconnect functions are accept functions, returning the connection type `'T` itself, or wrapped in `Async<'T>` or `Task<'T>`, or even in Hopac `Job<'T>` if you reference [NBomber.FSharp.Hopac](https://www.nuget.org/packages/NBomber.FSharp.Hopac/) NuGet. As always, anything here is optional (expect connect of course). Here is a shortest way you define a pool of 100 int numbers:

```fsharp
clients "pool of integer numbers" {
    connect (fun i _ -> i)
}
```
