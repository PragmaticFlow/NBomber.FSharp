// #r "paket:
// nuget Fake.DotNet.Cli
// nuget Fake.DotNet.Testing.Expecto
// nuget Fake.IO.FileSystem
// nuget Fake.Core.Target //"
// #load ".fake/build.fsx/intellisense.fsx"

#r "nuget: Fake.DotNet.Cli"
#r "nuget: Fake.DotNet.Testing.Expecto"
#r "nuget: Fake.IO.FileSystem"
#r "nuget: Fake.Core.Target"
#r "nuget: Fake.Core.ReleaseNotes"
#r "nuget: System.Reactive"


System.Environment.GetCommandLineArgs()
|> Array.skip 2 // skip fsi.exe; build.fsx
|> Array.toList
|> Fake.Core.Context.FakeExecutionContext.Create false __SOURCE_FILE__
|> Fake.Core.Context.RuntimeContext.Fake
|> Fake.Core.Context.setExecutionContext


open Fake.Core
open Fake.DotNet
open Fake.DotNet.Testing
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

let release = ReleaseNotes.load "RELEASE_NOTES.md"
// let changelog = Changelog.load "CHANGELOG.md"

module NuGet =
  let private nuget = "nuget"
  let private apiKey = "oy2h6g7bcbpbbt3wvhhq36h72lm3bbaelbrntk254ikp7i"
  let src = "https://api.nuget.org/v3/index.json"
  let push nupkg =
    Trace.trace $"Publishing nuget package: %s{nupkg}"
    let pushArgs = $"push %s{nupkg} -s https://api.nuget.org/v3/index.json -k %s{apiKey}"
    DotNet.exec id "nuget" pushArgs

let (|Release|CI|) input =
  if SemVer.isValid input then
      let semVer = SemVer.parse input
      Release semVer
  else
      CI

// Target.initEnvironment ()

Target.create "Clean" (fun _ ->
  !! "src/**/bin"
  ++ "src/**/obj"
  |> Shell.cleanDirs
)

Target.create "Build" (fun _ ->
  !! "src/**/*.*proj"
  |> Seq.iter (DotNet.build id)
)

Target.create "Pack" (fun _ ->
  Shell.cleanDir "nuget"
  !! "src/**/*.*proj"
  |> Seq.iter (DotNet.pack (fun p ->
    let customParams =
      [ $"""/p:PackageReleaseNotes="%s{release.Notes |> String.concat "\n"}" """
        $"""/p:PackageVersion="{release.NugetVersion}" """
      ]
      |> String.concat " "
    { p with
        Configuration = DotNet.BuildConfiguration.Release
        Common = DotNet.Options.withCustomParams (Some customParams) p.Common
        OutputPath = Some "nuget"
    }
  ))
)

Target.create "Push" (fun _ ->
  let result =
    !!"nuget/*.nupkg"
    |> Seq.map (fun nupkg ->
        (nupkg, NuGet.push nupkg))
    |> Seq.filter (fun (_, p) -> p.ExitCode <> 0)
    |> List.ofSeq
  match result with
  | [] -> ()
  | failedAssemblies ->
      failedAssemblies
      |> List.map (fun (nuget, proc) ->
          $"Failed to push NuGet package '%s{nuget}'. Process finished with exit code %d{proc.ExitCode}.")
      |> String.concat System.Environment.NewLine
      |> exn
      |> raise
)

Target.create "Test" (fun _ ->
  let args proj = $"-c Release -p %s{proj} -- --summary"
  !! "test/**/*.*proj"
  |> Seq.map(fun proj -> DotNet.exec id "run" (args proj))
  |> Seq.iter(fun result ->
    if result.OK then
      if result.Messages |> List.isEmpty |> not then
        Trace.tracefn "%A" result.Messages
    else
      if result.Errors |> List.isEmpty |> not then
        Trace.traceErrorfn "%A" result.Errors
      else
        failwith "Test failed"
  )
)

Target.create "All" ignore

"Clean"
  ==> "Build"
  ==> "Test"
  ==> "All"

"Test"
  ==> "Pack"
  ==> "Push"
Target.runOrDefault "All"
