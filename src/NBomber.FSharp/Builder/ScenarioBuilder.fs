namespace NBomber.FSharp

open System
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open NBomber.Contracts

/// scenario builder
type ScenarioBuilder(name : string) =
    let empty = Scenario.create name []
    let addTo scenario step =
        { scenario with Steps = [ step ] |> List.append scenario.Steps }
    let unitTask (init : IScenarioContext -> Task) ctx =
        task {
            do! init ctx
            return ()
        }
    let asUnitTask (init : IScenarioContext -> unit) ctx =
        task {
            do init ctx
            return ()
        }

    member _.Zero() = empty
    member _.Yield _ = empty
    member inline _.Run f = f

    [<CustomOperation "steps">]
    member _.Steps(scenario : Scenario, steps : IStep list) =
        { scenario with Steps = steps @ scenario.Steps }

    /// create not tracked pause step
    [<CustomOperation "pause">]
    member _.Pause(scenario: Scenario, timeSpan : TimeSpan) =
        Step.createPause timeSpan |> addTo scenario

    /// set warmup duration
    [<CustomOperation "warmUp">]
    member _.WarmUp(scenario: Scenario, time) =
        Scenario.withWarmUpDuration time scenario

    /// run without warmup
    [<CustomOperation "noWarmUp">]
    member _.NoWarmUp(scenario: Scenario) =
        Scenario.withWarmUpDuration TimeSpan.Zero scenario

    /// setup load simulation
    [<CustomOperation "load">]
    member _.Load(scenario: Scenario, simulations) =
        Scenario.withLoadSimulations simulations scenario

    /// run an action before test
    [<CustomOperation "init">]
    member _.Init(scenario: Scenario, init) =
        Scenario.withInit init scenario
    member _.Init(scenario: Scenario, init) =
        Scenario.withInit (unitTask init) scenario
    member _.Init(scenario: Scenario, init) =
        Scenario.withInit (asUnitTask init) scenario

    /// run an action after test
    [<CustomOperation "clean">]
    member _.Clean(scenario: Scenario, clean) =
        Scenario.withClean clean scenario
    member _.Clean(scenario: Scenario, clean) =
        Scenario.withClean (unitTask clean) scenario
    member _.Clean(scenario: Scenario, clean) =
        Scenario.withClean (asUnitTask clean) scenario
