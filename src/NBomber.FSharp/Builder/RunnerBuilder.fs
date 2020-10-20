namespace NBomber.FSharp

open NBomber.Contracts
open NBomber.Configuration

[<AutoOpen>]
module private Internals =
    let inline empty name =
        { NBomberRunner.registerScenarios [] with TestSuite = name }
    let inline addReportFormat (ctx: NBomberContext) format =
        { ctx with ReportFormats = format::ctx.ReportFormats
                                   |> List.distinct }
/// performance test builder
type RunnerBuilder(name: string) =

    /// define a list of test scenarios
    [<CustomOperation "scenarios">]
    member _.Scenarios(ctx, scenarios) =
        { ctx with RegisteredScenarios = scenarios }

    /// statistics reporter
    [<CustomOperation "reporter">]
    member _.Report(ctx, reporter) =
        { ctx with ReportingSinks = [reporter] }

    /// statistics reporter
    [<CustomOperation "reportInterval">]
    member _.ReportInterval(ctx: NBomberContext, interval) =
        { ctx with SendStatsInterval = interval }

    /// add html to selected report formats
    [<CustomOperation "reportHtml">]
    member _.ReportHtml(ctx : NBomberContext) =
        addReportFormat ctx ReportFormat.Html

    /// add markdown to selected report formats
    [<CustomOperation "reportMd">]
    member _.ReportMd(ctx : NBomberContext) =
        addReportFormat ctx ReportFormat.Md

    /// add markdown to selected report formats
    [<CustomOperation "reportCsv">]
    member _.ReportCsv(ctx : NBomberContext) =
        addReportFormat ctx ReportFormat.Csv

    /// add markdown to selected report formats
    [<CustomOperation "reportTxt">]
    member _.ReportTxt(ctx : NBomberContext) =
        addReportFormat ctx ReportFormat.Txt

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

    // [<CustomOperation "applicationType">]
    // member _.ApplicationType(ctx, application) =
    //     { ctx with ApplicationType = Some application }
    member _.Zero() = empty name
    member inline __.Yield (()) = __.Zero()
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
    member inline _.Run f = f
    member inline __.Combine(state, state2) =
        { state with RegisteredScenarios = state.RegisteredScenarios |> List.append state2.RegisteredScenarios }
    member inline __.Combine(state, scenario: Scenario) =
        { state with RegisteredScenarios = scenario::state.RegisteredScenarios }

