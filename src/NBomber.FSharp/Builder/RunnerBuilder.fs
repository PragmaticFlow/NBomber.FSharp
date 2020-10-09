namespace NBomber.FSharp.Builders

open NBomber.Contracts
open NBomber.Configuration
open NBomber.FSharp

/// performance test builder
type RunnerBuilder(name: string) =
    let empty =
        { NBomberRunner.registerScenarios [] with TestSuite = name }

    member _.Zero() = empty
    member _.Yield _ = empty
    member _.Run f = f

    /// define a list of test scenarios
    [<CustomOperation "scenarios">]
    member _.Scenarios(ctx, scenarios) =
        { ctx with RegisteredScenarios = scenarios }

    /// statisctics reporter
    [<CustomOperation "reporter">]
    member _.Report(ctx, reporter, interval) =
        { ctx with ReportingSinks = [reporter]
                   SendStatsInterval = interval }

    [<CustomOperation "reportHtml">]
    member _.ReportHtml(ctx : NBomberContext) =
        { ctx with ReportFormats = ReportFormat.Html::ctx.ReportFormats |> List.distinct }

    [<CustomOperation "noReports">]
    member _.NoReports(ctx : NBomberContext) =
        { ctx with ReportFormats = [] }

    /// test name
    [<CustomOperation "name">]
    member _.Name(ctx : NBomberContext, name) =
        { ctx with TestName = name }

    /// load test configuration from file path
    [<CustomOperation "config">]
    member _.Config(ctx, path) =
        NBomberRunner.loadConfig path ctx

    /// load infrastructure configuration from path
    [<CustomOperation "infraConfig">]
    member _.ConfigInfrastructure(ctx, path) =
        NBomberRunner.loadInfraConfig path ctx

    /// set list of test runner plugins
    [<CustomOperation "plugins">]
    member _.Reports(ctx, plugins) =
        { ctx with WorkerPlugins = plugins }

    // [<CustomOperation "runProcess">]
    // member _.ApplicationTypeProcess(ctx) =
    //     { ctx with ApplicationType = Some ApplicationType.Process }

    // [<CustomOperation "runConsole">]
    // member _.ApplicationTypeConsole(ctx) =
    //     { ctx with ApplicationType = Some ApplicationType.Console }

    // [<CustomOperation "applicationType">]
    // member _.ApplicationType(ctx, application) =
    //     { ctx with ApplicationType = Some application }
