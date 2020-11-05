namespace NBomber.FSharp

open NBomber.Contracts

[<AutoOpen>]
module private RunnerInternals =
    let empty = NBomberRunner.registerScenarios []
    let inline addReportFormat (ctx: NBomberContext) format =
        { ctx with ReportFormats = format::ctx.ReportFormats
                                   |> List.distinct }
    let inline applyReport report ctx =
        { ctx with
            ReportFormats = report.Formats
            ReportingSinks = report.Sinks
            ReportFileName = report.FileName
            ReportFolder = report.FolderName
            SendStatsInterval = report.Interval
        }

    let inline checkFailureRate failureRate nodeStats =
        nodeStats.RequestCount <> 0
        && (float nodeStats.FailCount / float nodeStats.RequestCount) <= failureRate

    let inline getExitCode ctx (runResult: Result<NodeStats, string>) =
        match runResult with
        | Error e ->
            eprintf """Error in "%s"/"%s":\n%A""" ctx.TestSuite ctx.TestName e
            1
        | Ok stats ->
            if checkFailureRate 0.05 stats then 0 else 1
    let inline orIfDefault defaultValue otherValue value =
        if value = defaultValue then otherValue else value

/// performance test builder
type RunnerBuilder(name: string) =

    /// define a list of test scenarios
    [<CustomOperation "scenarios">]
    member _.Scenarios(ctx, scenarios) =
        { ctx with RegisteredScenarios = scenarios }
    member _.Scenarios(ctx, scenarios) =
        { ctx with RegisteredScenarios = List.ofSeq scenarios }

    /// deletes default reporters from test runner
    [<CustomOperation "noReports">]
    member _.NoReports(ctx : NBomberContext) =
        { ctx with ReportFormats = [] }

    [<CustomOperation "testName">]
    member _.TestName(ctx : NBomberContext, name) =
        { ctx with TestName = name }

    /// load test configuration from file path
    [<CustomOperation "config">]
    member _.Config(ctx, path) =
        NBomberRunner.loadConfig path ctx

    /// load infrastructure configuration from path
    [<CustomOperation "infraConfig">]
    member _.ConfigInfrastructure(ctx, path) =
        NBomberRunner.loadInfraConfig path ctx

    [<CustomOperation "loggerConfig">]
    member _.LoggerConfig(ctx, loggerConfig) =
        NBomberRunner.withLoggerConfig loggerConfig ctx
    member _.LoggerConfig(ctx, loggerConfig) =
        NBomberRunner.withLoggerConfig (fun () -> loggerConfig) ctx

    /// set list of test runner plugins
    [<CustomOperation "plugins">]
    member _.Reports(ctx, plugins) =
        { ctx with WorkerPlugins = plugins }

    [<CustomOperation "runProcess">]
    member _.ApplicationTypeProcess(ctx) =
        { ctx with ApplicationType = Some ApplicationType.Process }

    [<CustomOperation "runConsole">]
    member _.ApplicationTypeConsole(ctx) =
        { ctx with ApplicationType = Some ApplicationType.Console }

    /// run with the specified cli arguments, return exit code
    [<CustomOperation "withArgs">]
    member _.WithArgs(ctx : NBomberContext, args) =
        ctx
        |> NBomberRunner.runWithArgs args
        |> getExitCode ctx

    /// run and return exit code, instead of result
    [<CustomOperation "withExitCode">]
    member _.WithExitCode(ctx : NBomberContext) =
        ctx
        |> NBomberRunner.run
        |> getExitCode ctx

    member _.Zero() = { empty with TestSuite = name }
    member inline __.Yield (()) = __.Zero()
    member inline __.Yield(report : ReportContext) =
        __.Zero() |> applyReport report
    member inline __.Yield(scenario : Scenario) =
        let ctx = __.Zero()
        { ctx with RegisteredScenarios = scenario::ctx.RegisteredScenarios }
    member inline __.Yield(step : IStep) =
        let scn =
            ScenarioBuilder step.StepName {
                step
            }
        __.Yield(scn)
    member inline _.Delay f = f()

    member inline __.Combine(state: NBomberContext, otherState: NBomberContext) =
        let zero = __.Zero()
        {   TestSuite = state.TestSuite
            TestName = state.TestName |> orIfDefault zero.TestName otherState.TestName
            ApplicationType = state.ApplicationType |> Option.orElse otherState.ApplicationType
            CreateLoggerConfig = state.CreateLoggerConfig |> Option.orElse otherState.CreateLoggerConfig
            InfraConfig = state.InfraConfig |> Option.orElse otherState.InfraConfig
            NBomberConfig = state.NBomberConfig |> Option.orElse otherState.NBomberConfig
            RegisteredScenarios = state.RegisteredScenarios |> List.append otherState.RegisteredScenarios
            ReportFileName = state.ReportFileName |> Option.orElse otherState.ReportFileName
            ReportFolder = state.ReportFolder |> Option.orElse otherState.ReportFolder
            ReportFormats = state.ReportFormats |> List.append otherState.ReportFormats
            ReportingSinks = state.ReportingSinks |> List.append otherState.ReportingSinks
            WorkerPlugins = state.WorkerPlugins |> List.append otherState.WorkerPlugins
            SendStatsInterval = state.SendStatsInterval |> orIfDefault zero.SendStatsInterval otherState.SendStatsInterval
        }

    member inline __.Combine(state, scenario: Scenario) =
        { state with RegisteredScenarios = scenario::state.RegisteredScenarios }
    member inline __.Combine(state, report: ReportContext) =
        state |> applyReport report

    member inline __.For (state: NBomberContext, f: unit -> NBomberContext) =
        __.Combine(state, f())
    member inline __.For (xs: seq<'T>, f: 'T -> NBomberContext) =
        xs
        |> Seq.map f
        |> Seq.reduce (fun a b -> __.Combine(a, b))

    member inline _.Run(ctx: NBomberContext) =
        NBomberRunner.run ctx
    member inline _.Run f = f
