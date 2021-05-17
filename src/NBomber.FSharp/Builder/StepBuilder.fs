namespace NBomber.FSharp

open System
open System.Threading.Tasks
open FSharp.Control.Tasks.NonAffine
open NBomber.Contracts


type FullStep<'c, 'f> =
    { Feed: IFeed<'f>
      Pool: IClientFactory<'c>
      Timeout: TimeSpan
      DoNotTrack: bool
      Execute: (IStepContext<'c, 'f> -> Response Task) }

module Defaults =
  let timeout = TimeSpan.FromSeconds 1.0

type StepBuilder(name: string) =
    inherit StepEmptyBuilder()

    [<CustomOperation "execute">]
    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Response Task) =
        { Feed = state.Feed
          Pool = state.Pool
          Timeout = Defaults.timeout
          Execute = exe
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Response) =
        { Feed = state.Feed
          Pool = state.Pool
          Timeout = Defaults.timeout
          Execute = fun ctx -> task { return exe ctx }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Response Async) =
        { Feed = state.Feed
          Pool = state.Pool
          Timeout = Defaults.timeout
          Execute = fun ctx -> task { return! exe ctx }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Task) =
        { Feed = state.Feed
          Pool = state.Pool
          Timeout = Defaults.timeout
          Execute = fun ctx -> task {
            do! exe ctx
            return Response.ok()
          }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> unit) =
        { Feed = state.Feed
          Pool = state.Pool
          Timeout = Defaults.timeout
          Execute = fun ctx -> task {
            do exe ctx
            return Response.ok()
          }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> unit Task) =
        { Feed = state.Feed
          Pool = state.Pool
          Timeout = Defaults.timeout
          Execute = fun ctx -> task {
            do! exe ctx
            return Response.ok()
          }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> unit Async) =
        { Feed = state.Feed
          Pool = state.Pool
          Timeout = Defaults.timeout
          Execute = fun ctx -> task {
            do! exe ctx
            return Response.ok()
          }
          DoNotTrack = false
        }

    [<CustomOperation "pause">]
    member inline __.Pause(state : StepEmpty<'c,'f>, timeSpan: TimeSpan) =
       { __.Execute(state, fun _ -> Task.Delay timeSpan) with
             DoNotTrack = true }
    member inline __.Pause(state : StepEmpty<'c,'f>, millis: int) =
      { __.Execute(state, fun _ -> Task.Delay millis) with
            DoNotTrack = true }

    // lower case to differ from hopac's timeOut function
    [<CustomOperation "timeout">]
    member inline __.TimeOut(state : FullStep<'c,'f>, timeout: TimeSpan) =
       { state with Timeout = timeout }
    member inline __.TimeOut(state : FullStep<'c,'f>, millis: int) =
      { state with Timeout = TimeSpan.FromMilliseconds(float millis) }

    [<CustomOperation "doNotTrack">]
    member inline _.DoNotTrack(state : FullStep<'c,'f>) =
        { state with DoNotTrack = true }

    member _.Run(state : FullStep<'c,'f>) =
        Step.create(name, state.Execute, state.Pool, state.Feed, state.Timeout, state.DoNotTrack)
