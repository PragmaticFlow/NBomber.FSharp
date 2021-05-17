# NBomber.FSharp

F# Computation Expressions for [NBomber](https://nbomber.com) API

| Package              | Description | Status |
|----------------------|-------------|--------|
| NBomber.FSharp       | CE for test runners, scenarios and steps | [![NBomber.FSharp](https://buildstats.info/nuget/NBomber.FSharp?includePreReleases=true)](https://www.nuget.org/packages/NBomber.FSharp/) |
| NBomber.FSharp.Http  | CE for http calls steps, similar to NBomber.Plugins.Http | [![NBomber.FSharp.Http](https://buildstats.info/nuget/NBomber.FSharp.Http?includePreReleases=true)](https://www.nuget.org/packages/NBomber.FSharp.Http/) |
| NBomber.FSharp.Hopac | [Hopac](https://hopac.github.io) support for the above, depends on both | [![NBomber.FSharp.Hopac](https://buildstats.info/nuget/NBomber.FSharp.Hopac?includePreReleases=true)](https://www.nuget.org/packages/NBomber.FSharp.Hopac/) |

[![Build history](https://buildstats.info/github/chart/PragmaticFlow/NBomber.FSharp)](https://github.com/PragmaticFlow/NBomber.FSharp/actions)

Look at [Demo.fs](test/NBomber.FSharp.Test/Demo.fs) or [programming documentation](docs/Programming.md) for usage

### Scenario

<table> <tbody>
<thead><tr><th>Fluent API</th><th>Computation Expression</th></tr></thead>
<tr><td>

```fsharp
Scenario.create "test" [step]
|> Scenario.withWarmUp (seconds 10)
|> Scenario.withLoadSimulations [
    KeepConstant(copies = copiesCount, during = seconds 2)
]
```

</td><td>

```fsharp
scenario "test" {
    warmUp (seconds 10)
    load [
        KeepConstant(copies = copiesCount, during = seconds 2)
    ]
    init (fun _ -> Task.FromResult())
    clean (fun _ -> Task.FromResult())
    steps [myStep]
    // or if just one step
    myStep
}
```

</td></tr>
</tbody></table>

### Step

The overloads of `step`'s custom operation `execute` accepts a function taking a step context and returning either `Response` or `unit` directly or wrapped in a `task`, `async` or even in a Hopac `job`
<table> <tbody>
<thead><tr><th>Fluent API</th><th>Computation Expression</th></tr></thead>
<tr><td>

```fsharp
Step.create("step name",
    myWebsocketPool,
    myGuidFeed,
    (fun ctx -> task {
        let doSomethingWithSocket (_: Guid) (_: ClientWebSocket) =
            ""
        ctx.Logger.Information("Can take feed and client {Ret}",
            takeBoth ctx.FeedItem ctx.Client)
        return Response.Ok()
    }), true)

```

</td><td>

```fsharp
step "step name" {
    dataFeed myGuidFeed
    myWebsocketPool
    execute (fun ctx ->
        let doSomethingWithSocket (_: Guid) (_: ClientWebSocket) =
            ""
        ctx.Logger.Information("Can take feed and client {Ret}",
            takeBoth ctx.FeedItem ctx.Client) )
    doNotTrack
}
```

</td></tr>
</tbody></table>

### Client factory

Simplified construction of clients. Notable differences are:

- `connect` and `disconnect` are more tolerant for missing type signatures and arguments
- no need for `cancellationToken` argument if it is not used in `connect` or `disconnect` functions
- `disconnect` can be omitted completely, if the type of created connection implements `IDisposable` or can be just disposed.
- `count` of connections can be omitted too, it defaults to `50`
- even less verbose compared to the C# API

<table> <tbody>
<thead><tr><th>Fluent API</th><th>Computation Expression</th></tr></thead>
<tr><td>

```fsharp

let clientFactory =
    ClientFactory.create(
        name = "db connections",
        initClient = (fun i ctx -> task {
            let c = new NpgsqlConnection(connectionString)
            do! c.OpenAsync(ctx.CancellationToken)
            return c
        }),
        disposeClient = (fun (c:NpgsqlConnection, ctx) -> c.CloseAsync(ctx.CancellationToken)),
        clientCount = 100)

```

</td><td>

```fsharp
let clientFactory =
    clients "db connections" {
        count 100
        connect(fun i ctx -> task {
            let c = new NpgsqlConnection(connectionString)
            do! c.OpenAsync(ctx.CancellationToken)
            return c
        })
        disconnect(fun c ctx -> c.CloseAsync(ctx.CancellationToken))
    }
```

</td></tr>
</tbody></table>


### Reporter

Imagine you need just a html report. Then it is as short as

```fsharp
report { html }
```

<table> <tbody>
<thead><tr><th>Fluent API</th><th>Computation Expression</th></tr></thead>
<tr><td>

```fsharp
    NBomberRunner.withReportFormats [
        ReportFormat.Csv
        ReportFormat.Txt
        ReportFormat.Md
        ReportFormat.Html
    ]
    |> NBomberRunner.withReportingSinks
        [ influxdbSink
          customFormatSink ]
        (seconds 10)
    |> NBomberRunner.withReportFolder "reportsFolder"
    |> NBomberRunner.withReportFileName "reportFile"
    // or if none
    |> NBomberRunner.withoutReports
    ...

```

</td><td>

```fsharp
    testSuite "Suite name" {
        report {
            csv
            text
            markdown
            html
            sink influxdbSink
            sink customFormatSink
            interval (seconds 10)
            folderName "reportsFolder"
            fileName "reportFile"
        }
        // or if none
        noReports

        ...
    }

```

</td></tr>
</tbody></table>

### Other runner configuration

<table> <tbody>
<thead><tr><th>Fluent API</th><th>Computation Expression</th></tr></thead>
<tr><td>

```fsharp
NBomberRunner.registerScenarios []
|> NBomberRunner.withTestName "Test name"
|> NBomberRunner.withTestSuite "Suite name"
|> NBomberRunner.withoutReports
|> NBomberRunner.loadConfig "loadTestConfig.json"
|> NBomberRunner.loadInfraConfig "infrastructureConfig.json"
|> NBomberRunner.withWorkerPlugins []
|> NBomberRunner.withApplicationType ApplicationType.Console
// |> NBomberRunner.runWithArgs args
|> NBomberRunner.run
|> evaluateStatsFunction
```

</td><td>

```fsharp
testSuite "Suite name" {
    testName "Test name"
    noReports
    scenarios scenarioBuilderTest
    config "loadTestConfig.json"
    infraConfig "infrastructureConfig.json"
    plugins [ (* plugins list*) ]
    runConsole
    // runAsProcess
    // runWithArgs args
    // runWithExitCode
}
```

</td></tr>
</tbody></table>
