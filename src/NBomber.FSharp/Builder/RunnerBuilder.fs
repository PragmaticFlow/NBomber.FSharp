namespace NBomber.FSharp

open NBomber.Contracts


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
        { ctx with Reporting = { ctx.Reporting with Formats = [] }}

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
    member _.Plugins(ctx, plugins) =
        { ctx with WorkerPlugins = plugins }

    [<CustomOperation "noHintsAnalyzer">]
    member _.NoHintAnalyzer(ctx: NBomberContext) =
        { ctx with UseHintsAnalyzer = false }

    [<CustomOperation "runProcess">]
    member _.ApplicationTypeProcess(ctx) =
        { ctx with ApplicationType = Some ApplicationType.Process }
        |> NBomberRunner.run

    [<CustomOperation "runConsole">]
    member _.ApplicationTypeConsole(ctx) =
        { ctx with ApplicationType = Some ApplicationType.Console }
        |> NBomberRunner.run

    /// run with the specified cli arguments, return exit code
    [<CustomOperation "runWithArgs">]
    member _.WithArgs(ctx : NBomberContext, args) =
        ctx
        |> NBomberRunner.runWithArgs args
        |> getExitCode ctx

    /// run and return exit code, instead of result
    [<CustomOperation "runWithExitCode">]
    member _.WithExitCode(ctx : NBomberContext) =
        ctx
        |> NBomberRunner.run
        |> getExitCode ctx

    member _.Zero() = { zeroContext with TestSuite = name }
    member inline __.Yield (()) = __.Zero()
    member inline __.Yield(report : ReportingContext) =
        { __.Zero() with Reporting = report }
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

    member inline __.Combine(state: ReportingContext, otherState: ReportingContext) =
        let zero = __.Zero().Reporting
        {
            FileName = state.FileName |> Option.orElse otherState.FileName
            FolderName = state.FolderName |> Option.orElse otherState.FolderName
            Formats = state.Formats |> orIfDefault zero.Formats otherState.Formats
            Sinks = state.Sinks |> orIfDefault zero.Sinks otherState.Sinks
            SendStatsInterval = state.SendStatsInterval |> orIfDefault zero.SendStatsInterval otherState.SendStatsInterval
        }
    member inline __.Combine(state: NBomberContext, otherState: NBomberContext) =
        let zero = __.Zero()
        {   TestSuite = state.TestSuite
            TestName = state.TestName |> orIfDefault zero.TestName otherState.TestName
            ApplicationType = state.ApplicationType |> Option.orElse otherState.ApplicationType
            CreateLoggerConfig = state.CreateLoggerConfig |> Option.orElse otherState.CreateLoggerConfig
            InfraConfig = state.InfraConfig |> Option.orElse otherState.InfraConfig
            NBomberConfig = state.NBomberConfig |> Option.orElse otherState.NBomberConfig
            RegisteredScenarios = state.RegisteredScenarios |> List.append otherState.RegisteredScenarios
            Reporting = __.Combine(state.Reporting, otherState.Reporting)
            UseHintsAnalyzer = true
            WorkerPlugins = state.WorkerPlugins |> List.append otherState.WorkerPlugins
        }

    member inline __.Combine(state, scenario: Scenario) =
        { state with RegisteredScenarios = scenario::state.RegisteredScenarios }
    member inline __.Combine(state, report: ReportingContext) =
        { state with Reporting = report }

    member inline __.For (state: NBomberContext, f: unit -> NBomberContext) =
        __.Combine(state, f())
    member inline __.For (xs: seq<'T>, f: 'T -> NBomberContext) =
        xs
        |> Seq.map f
        |> Seq.reduce (fun a b -> __.Combine(a, b))

    member inline _.Run f = f
