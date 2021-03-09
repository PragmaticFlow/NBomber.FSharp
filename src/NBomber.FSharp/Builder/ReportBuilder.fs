namespace NBomber.FSharp

open System
open NBomber.Configuration
open NBomber.Contracts


type ReportBuilder() =
    member inline __.AddFormat format ctx =
        if ctx.Formats = __.Zero().Formats then
            { ctx with Formats = [format] }
        elif List.contains format ctx.Formats then
            ctx
        else
            { ctx with Formats = format::ctx.Formats |> List.distinct }

    [<CustomOperation "fileName">]
    member inline _.FileName (ctx, fileName ) =
        { ctx with FileName = Some fileName }
    [<CustomOperation "folderName">]
    member inline _.Folder (ctx, folderName ) =
        { ctx with FolderName = Some folderName }

    [<CustomOperation "html">]
    member inline __.Html ctx =
        __.AddFormat ReportFormat.Html ctx
    [<CustomOperation "csv">]
    member inline __.Csv ctx =
        __.AddFormat  ReportFormat.Csv ctx
    [<CustomOperation "markdown">]
    member inline __.Md ctx =
        __.AddFormat  ReportFormat.Md ctx
    [<CustomOperation "text">]
    member inline __.Text ctx =
        __.AddFormat ReportFormat.Txt ctx

    [<CustomOperation "sink">]
    member inline _.Sink(ctx, reporter) =
        { ctx with Sinks = reporter::ctx.Sinks }

    [<CustomOperation "interval">]
    member inline _.Report(ctx: ReportingContext, interval: TimeSpan) =
        { ctx with SendStatsInterval = interval }

    member _.Zero() = zeroContext.Reporting
    member inline __.Yield(()) = __.Zero()
    member inline _.Delay f = f()
    member inline _.Run (ctx: ReportingContext) = ctx
