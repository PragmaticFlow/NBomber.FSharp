namespace NBomber.FSharp

open System
open NBomber.Configuration
open NBomber.Contracts

type ReportContext =
    { FileName: string option
      FolderName: string option
      Formats:  ReportFormat list
      Sinks: IReportingSink list
      Interval: TimeSpan
    }
    static member Empty =
        { FolderName = None
          FileName = None
          Formats = []
          Sinks = []
          Interval = TimeSpan.FromSeconds(10.0)
        }

[<AutoOpen>]
module private ReportInternals =
    let inline addFormat format ctx =
        { ctx with Formats = format::ctx.Formats }

type ReportBuilder() =

    [<CustomOperation "fileName">]
    member inline _.FileName (ctx, fileName ) =
        { ctx with FileName = Some fileName }
    [<CustomOperation "folderName">]
    member inline _.Folder (ctx, folderName ) =
        { ctx with FolderName = Some folderName }

    [<CustomOperation "html">]
    member inline _.Html ctx =
        ctx |> addFormat ReportFormat.Html
    [<CustomOperation "csv">]
    member inline _.Csv ctx =
        ctx |> addFormat ReportFormat.Csv
    [<CustomOperation "markdown">]
    member inline _.Md ctx =
        ctx |> addFormat ReportFormat.Md
    [<CustomOperation "text">]
    member inline _.Text ctx =
        ctx |> addFormat ReportFormat.Txt
    // [<CustomOperation "formats">]
    // member _.Formats(ctx, formats) =
    //     { ctx with Formats = formats }

    [<CustomOperation "sink">]
    member inline _.Sink(ctx, reporter) =
        { ctx with Sinks = reporter::ctx.Sinks }
    [<CustomOperation "interval">]
    member inline _.Report(ctx, interval: TimeSpan) =
        { ctx with Interval = interval }

    member inline _.Zero() = ReportContext.Empty
    member inline __.Yield(()) = __.Zero()
    member inline _.Delay f = f()
    member inline _.Run (ctx: ReportContext) = ctx
