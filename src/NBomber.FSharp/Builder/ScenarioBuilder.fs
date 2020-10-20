namespace NBomber.FSharp

open System
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open NBomber.Contracts

// TODO disallow scenario without steps
[<AutoOpen>]
module ScenarioInternals =
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
/// scenario builder
type ScenarioBuilder(name : string) =
    let empty = Scenario.create name []

    /// add external list of steps
    [<CustomOperation "steps">]
    member inline _.Steps(scenario : Scenario, steps : IStep list) =
        { scenario with Steps = scenario.Steps |> List.append steps }

    /// create not tracked pause step
    [<CustomOperation "pause">]
    member inline _.Pause(scenario: Scenario, ms : int) =
        Step.createPause ms |> addTo scenario
    member inline _.Pause(scenario: Scenario, timeSpan : TimeSpan) =
        Step.createPause timeSpan |> addTo scenario
    member inline _.Pause(scenario: Scenario, ms : unit -> int) =
        Step.createPause ms |> addTo scenario
    member inline _.Pause(scenario: Scenario, timeSpan : unit -> TimeSpan) =
        Step.createPause timeSpan |> addTo scenario

    /// set warmup duration
    [<CustomOperation "warmUp">]
    member inline _.WarmUp(scenario: Scenario, time) =
        Scenario.withWarmUpDuration time scenario

    /// run without warmup
    [<CustomOperation "noWarmUp">]
    member inline _.NoWarmUp(scenario: Scenario) =
        Scenario.withoutWarmUp scenario

    /// setup load simulation
    [<CustomOperation "load">]
    member inline _.Load(scenario: Scenario, simulations) =
        Scenario.withLoadSimulations simulations scenario
    member inline _.Load(scenario: Scenario, simulation) =
        Scenario.withLoadSimulations [ simulation ] scenario

    /// run an action before test
    [<CustomOperation "init">]
    member inline _.Init(scenario: Scenario, init) =
        Scenario.withInit init scenario
    member inline _.Init(scenario: Scenario, init) =
        Scenario.withInit (unitTask init) scenario
    member inline _.Init(scenario: Scenario, init) =
        Scenario.withInit (asUnitTask init) scenario

    /// run an action after test
    [<CustomOperation "clean">]
    member inline _.Clean(scenario: Scenario, clean) =
        Scenario.withClean clean scenario
    member inline _.Clean(scenario: Scenario, clean) =
        Scenario.withClean (unitTask clean) scenario
    member inline _.Clean(scenario: Scenario, clean) =
        Scenario.withClean (asUnitTask clean) scenario


    member inline __.Combine(scenario, otherScenario) =
        let defaultWarmUp = TimeSpan.FromSeconds 30.0
        let defaultLoad = [ InjectPerSec(rate = 50, during = TimeSpan.FromMinutes 1.0) ]
        { Scenario.ScenarioName = scenario.ScenarioName
          Init = otherScenario.Init |> Option.orElse scenario.Init
          Clean = otherScenario.Clean |> Option.orElse scenario.Clean
          Steps = List.append scenario.Steps otherScenario.Steps
          WarmUpDuration =
            if scenario.WarmUpDuration = defaultWarmUp then
                otherScenario.WarmUpDuration
            else
                scenario.WarmUpDuration
          LoadSimulations =
            if scenario.LoadSimulations = defaultLoad then
                otherScenario.LoadSimulations
            else
                scenario.LoadSimulations
        }

    member _.Zero() = empty
    member inline __.Yield(()) = __.Zero()
    member inline __.Yield(step : IStep) = __.Steps(__.Zero(), [step])
    member inline _.Delay f = f()
    member inline __.Combine(scenario, step: IStep) = __.Steps(scenario, [step])
    member inline _.Run f = f
