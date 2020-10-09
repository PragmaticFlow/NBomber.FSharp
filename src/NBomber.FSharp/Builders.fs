[<AutoOpen>]
module NBomber.FSharp.Builder

open NBomber.FSharp.Builders

/// creates a step builder
let step = StepBuilder

/// creates a scenario builder
let scenario name = ScenarioBuilder name

// type ReportBuilder() =
//     member _.Yield _ = ReporterConfig.Default

//     [<CustomOperation "fileName">]
//     member _.ReportFileName(ctx, fileName) =
//         { ctx with FileName = Some fileName }

//     [<CustomOperation "interval">]
//     member _.Interval(ctx : ReporterConfig, interval) =
//         { ctx with SendStatsInterval = interval }

//     [<CustomOperation "formats">]
//     member _.Formats(ctx, formats) =
//         { ctx with Formats = formats }

//     [<CustomOperation "sink">]
//     member _.Report(ctx, reporter) =
//         { ctx with Sinks = reporter::ctx.Sinks }

// let report = ReportBuilder()


/// creates a performance test builder
let testSuite name = RunnerBuilder name
