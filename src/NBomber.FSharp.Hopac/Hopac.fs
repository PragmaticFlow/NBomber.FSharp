module NBomber.FSharp.Hopac

open Hopac
open NBomber.Contracts
open NBomber.FSharp

type StepBuilder(name : string) =
    inherit NBomber.FSharp.StepBuilder(name) with
        member inline _.Execute (state : IncompleteStep<'c,'f>, exe : IStepContext<'c,'f> -> #Job<Response>) =
            { Name = state.Name
              Feed = state.Feed
              Pool = state.Pool
              Execute = exe >> startAsTask
              DoNotTrack = state.DoNotTrack
            }
type ScenarioBuilder(name: string) =
    inherit NBomber.FSharp.ScenarioBuilder(name) with
        member inline _.Init(scenario: Scenario, init) =
            Scenario.withInit (init >> startAsTask) scenario
        member inline _.Clean(scenario: Scenario, clean) =
            Scenario.withClean (clean >> startAsTask) scenario

let step = StepBuilder
let scenario = ScenarioBuilder
