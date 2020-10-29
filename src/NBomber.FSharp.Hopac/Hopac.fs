module NBomber.FSharp.Hopac

open Hopac
open NBomber.Contracts
open NBomber.FSharp

type StepBuilder(name : string) =
    inherit NBomber.FSharp.StepBuilder(name) with
        member inline __.Execute (state: IncompleteStep<'c,'f>, exe : IStepContext<'c,'f> -> Job<Response>) =
            { Name = state.Name
              Feed = state.Feed
              Pool = state.Pool
              Execute = exe >> startAsTask
              DoNotTrack = false
            }
        member inline __.Execute(state: IncompleteStep<'c,'f>, exe: IStepContext<'c, 'f> -> Job<unit>) =
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

let step = StepBuilder
let scenario = ScenarioBuilder
