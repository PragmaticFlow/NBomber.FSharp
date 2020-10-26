namespace NBomber.FSharp

open System
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open NBomber.Contracts

type ScenarioNoSteps  = ScenarioNoSteps of Scenario
type ScenarioHasSteps = ScenarioHasSteps of Scenario

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
    let inline appendSteps steps scenario =
        { scenario with Steps = scenario.Steps |> List.append steps }

/// scenario builder
type ScenarioBuilder(name : string) =
    let empty = Scenario.create name [] |> ScenarioNoSteps

    /// add external list of steps
    [<CustomOperation "steps">]
    member inline _.Steps(ScenarioNoSteps scenario, steps : IStep list) =
        scenario |> appendSteps steps |> ScenarioHasSteps

    // [<CustomOperation "pause">]
    // member inline _.Pause(scenario: Scenario, ms : int) =
    //     Step.createPause ms |> addTo scenario
    // member inline _.Pause(scenario: Scenario, timeSpan : TimeSpan) =
    //     Step.createPause timeSpan |> addTo scenario
    // member inline _.Pause(scenario: Scenario, ms : unit -> int) =
    //     Step.createPause ms |> addTo scenario
    // member inline _.Pause(scenario: Scenario, timeSpan : unit -> TimeSpan) =
    //     Step.createPause timeSpan |> addTo scenario

    /// set warmup duration
    [<CustomOperation "warmUp">]
    member inline _.WarmUp(ScenarioNoSteps scenario, time) =
        Scenario.withWarmUpDuration time scenario |> ScenarioNoSteps
    member inline _.WarmUp(ScenarioHasSteps scenario, time) =
        Scenario.withWarmUpDuration time scenario |> ScenarioHasSteps

    /// run without warmup
    [<CustomOperation "noWarmUp">]
    member inline _.NoWarmUp(ScenarioNoSteps scenario) =
        Scenario.withoutWarmUp scenario |> ScenarioNoSteps

    member inline _.NoWarmUp(ScenarioHasSteps scenario) =
        Scenario.withoutWarmUp scenario |> ScenarioHasSteps

    /// setup load simulation
    [<CustomOperation "load">]
    member inline _.Load(ScenarioNoSteps scenario, simulations) =
        Scenario.withLoadSimulations simulations scenario |> ScenarioNoSteps
    member inline _.Load(ScenarioNoSteps scenario, simulation) =
        Scenario.withLoadSimulations [ simulation ] scenario |> ScenarioNoSteps

    member inline _.Load(ScenarioHasSteps scenario, simulations) =
        Scenario.withLoadSimulations simulations scenario |> ScenarioHasSteps
    member inline _.Load(ScenarioHasSteps scenario, simulation) =
        Scenario.withLoadSimulations [ simulation ] scenario |> ScenarioHasSteps

    /// run an action before test
    [<CustomOperation "init">]
    member inline _.Init(ScenarioNoSteps scenario, init) =
        Scenario.withInit init scenario |> ScenarioNoSteps
    member inline _.Init(ScenarioNoSteps scenario, init) =
        Scenario.withInit (unitTask init) scenario |> ScenarioNoSteps
    member inline _.Init(ScenarioNoSteps scenario, init) =
        Scenario.withInit (asUnitTask init) scenario |> ScenarioNoSteps

    member inline _.Init(ScenarioHasSteps scenario, init) =
        Scenario.withInit init scenario |> ScenarioHasSteps
    member inline _.Init(ScenarioHasSteps scenario, init) =
        Scenario.withInit (unitTask init) scenario |> ScenarioHasSteps
    member inline _.Init(ScenarioHasSteps scenario, init) =
        Scenario.withInit (asUnitTask init) scenario |> ScenarioHasSteps

    /// run an action after test
    [<CustomOperation "clean">]
    member inline _.Clean(ScenarioNoSteps scenario, clean) =
        Scenario.withClean clean scenario |> ScenarioNoSteps
    member inline _.Clean(ScenarioNoSteps scenario, clean) =
        Scenario.withClean (unitTask clean) scenario |> ScenarioNoSteps
    member inline _.Clean(ScenarioNoSteps scenario, clean) =
        Scenario.withClean (asUnitTask clean) scenario |> ScenarioNoSteps

    member inline _.Clean(ScenarioHasSteps scenario, clean) =
        Scenario.withClean clean scenario |> ScenarioHasSteps
    member inline _.Clean(ScenarioHasSteps scenario, clean) =
        Scenario.withClean (unitTask clean) scenario |> ScenarioHasSteps
    member inline _.Clean(ScenarioHasSteps scenario, clean) =
        Scenario.withClean (asUnitTask clean) scenario |> ScenarioHasSteps

    member inline __.Merge(scenario, otherScenario) =
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
    member inline _.Combine(ScenarioNoSteps scenario, step: IStep) =
        scenario |> appendSteps [step] |> ScenarioHasSteps
    member inline __.Combine(ScenarioHasSteps scenario, step: IStep) =
        scenario |> appendSteps [step] |> ScenarioHasSteps
    member inline __.Combine(ScenarioHasSteps scenario, ScenarioHasSteps otherScenario) =
        __.Merge(scenario, otherScenario) |> ScenarioHasSteps
    member inline __.Combine(ScenarioHasSteps scenario, ScenarioNoSteps otherScenario) =
        __.Merge(scenario, otherScenario) |> ScenarioHasSteps
    member inline _.Run(ScenarioHasSteps scenario) = scenario
