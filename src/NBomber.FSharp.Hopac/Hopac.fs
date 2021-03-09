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
            __.Execute(state, exe >> Job.map(fun _ -> Response.ok()))

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
    member inline _.Connect(ctx, f: unit -> Job<'T>) =
        { Count = ctx.Count
          Connect = (fun _ _ -> f() |> startAsTask)
          Disconnect = (fun _ _ -> Task.FromResult())
        }
    member inline _.Connect(ctx, f: int -> IBaseContext -> Job<'T>) =
        { Count = ctx.Count
          Connect = (fun nr b -> f nr b |> startAsTask)
          Disconnect = (fun _ _ -> Task.FromResult())
        }
    member inline _.Disconnect(ctx, f: 'T -> Job<unit>) =
        { ctx with Disconnect = fun c _ -> f c |> startAsTask }
    member inline _.Disconnect(ctx, f: 'T -> IBaseContext -> Job<unit>) =
        { ctx with Disconnect = fun c b -> f c b |> startAsTask }

let connectionPool = ConnectionPoolBuilder
let step = StepBuilder
let scenario = ScenarioBuilder
