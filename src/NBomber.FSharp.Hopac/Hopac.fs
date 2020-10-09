module NBomber.FSharp.Hopac

open Hopac
open NBomber.Contracts
open NBomber.FSharp.Builders

type StepBuilder(name : string) =
    inherit NBomber.FSharp.Builders.StepBuilder(name) with
        member __.Execute (state : IncompleteStep<'c,'f>, exe : IStepContext<'c,'f> -> #Job<Response>) =
            { Name = state.Name
              Feed = state.Feed
              Pool = state.Pool
              Execute = exe >> startAsTask
              DoNotTrack = state.DoNotTrack
            }
type ScenarioBuilder(name: string) =
    inherit NBomber.FSharp.Builders.ScenarioBuilder(name) with
        member _.Init(scenario: Scenario, init) =
            Scenario.withInit (init >> startAsTask) scenario
        member _.Clean(scenario: Scenario, clean) =
            Scenario.withClean (clean >> startAsTask) scenario

let step = StepBuilder
let scenario = ScenarioBuilder
