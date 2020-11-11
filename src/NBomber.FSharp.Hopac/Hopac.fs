module NBomber.FSharp.Hopac

open Hopac
open NBomber.Contracts
open NBomber.FSharp
open System.Threading
open System.Threading.Tasks

type StepBuilder(name : string) =
    inherit NBomber.FSharp.StepBuilder(name) with
        member inline __.Execute (state: StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Job<Response>) =
            { Feed = state.Feed
              Pool = state.Pool
              Execute = exe >> startAsTask
              DoNotTrack = false
            }
        member inline __.Execute(state: StepEmpty<'c,'f>, exe: IStepContext<'c, 'f> -> Job<unit>) =
            __.Execute(state, exe >> Job.map(fun _ -> Response.Ok()))

type ScenarioBuilder(name: string) =
    inherit NBomber.FSharp.ScenarioBuilder(name) with
        member inline __.Init(scenario: ScenarioNoSteps, init: IScenarioContext -> Job<unit>) =
            __.Init(scenario, init >> startAsTask)
        member inline __.Init(scenario: ScenarioHasSteps, init: IScenarioContext -> Job<unit>) =
            __.Init(scenario, init >> startAsTask)
        member inline __.Clean(scenario: ScenarioNoSteps, clean: IScenarioContext -> Job<unit>) =
            __.Clean(scenario, clean >> startAsTask)
        member inline __.Clean(scenario: ScenarioHasSteps, clean: IScenarioContext -> Job<unit>) =
            __.Clean(scenario, clean >> startAsTask)

type ConnectionPoolBuilder(name: string) =
    inherit NBomber.FSharp.ConnectionPoolBuilder(name) with
    member inline __.Connect(ctx, f: unit -> Job<'T>) =
        { Count = ctx.Count
          Connect = (fun _ _ -> f() |> startAsTask)
          Disconnect = (fun _ _ -> Task.CompletedTask)
        }
    member inline __.Connect(ctx, f: int -> CancellationToken -> Job<'T>) =
        { Count = ctx.Count
          Connect = (fun nr token -> f nr token |> startAsTask)
          Disconnect = (fun _ _ -> Task.CompletedTask)
        }
    member inline __.Disconnect(ctx, f: 'T -> Job<unit>) =
        { ctx with Disconnect = fun c _ -> f c |> startAsTask :> Task}
    member inline __.Disconnect(ctx, f: 'T -> CancellationToken -> Job<unit>) =
        { ctx with Disconnect = fun c token -> f c token |> startAsTask :> Task }

let connectionPool = ConnectionPoolBuilder
let step = StepBuilder
let scenario = ScenarioBuilder
