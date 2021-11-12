namespace NBomber.FSharp

open System
open System.Threading.Tasks
open FSharp.Control.Tasks.NonAffine
open NBomber.Contracts


type FullStep<'c, 'f> =
    { Generic: StepEmpty<'c, 'f>
      Timeout: TimeSpan
      DoNotTrack: bool
      Execute: (IStepContext<'c, 'f> -> Response Task) }

module Defaults =
  let timeout = TimeSpan.FromSeconds 1.0

type StepBuilder(name: string) =
    inherit StepEmptyBuilder()

    [<CustomOperation "execute">]
    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Response Task) =
        { Generic = state
          Timeout = Defaults.timeout
          Execute = exe
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Response) =
        { Generic = state
          Timeout = Defaults.timeout
          Execute = fun ctx -> task { return exe ctx }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Response Async) =
        { Generic = state
          Timeout = Defaults.timeout
          Execute = fun ctx -> task { return! exe ctx }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Task) =
        { Generic = state
          Timeout = Defaults.timeout
          Execute = fun ctx -> task {
            do! exe ctx
            return Response.ok()
          }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> unit) =
        { Generic = state
          Timeout = Defaults.timeout
          Execute = fun ctx -> task {
            do exe ctx
            return Response.ok()
          }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> unit Task) =
        { Generic = state
          Timeout = Defaults.timeout
          Execute = fun ctx -> task {
            do! exe ctx
            return Response.ok()
          }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> unit Async) =
        { Generic = state
          Timeout = Defaults.timeout
          Execute = fun ctx -> task {
            do! exe ctx
            return Response.ok()
          }
          DoNotTrack = false
        }

    member inline _.Combine (state : StepEmpty<_,'f>, pool: IClientFactory<'c>) =
      { Feed = state.Feed
        Pool = Some pool
      }

    member inline _.Combine (state : StepEmpty<'c,_>, feed: IFeed<'f>) =
      { Feed = Some feed
        Pool = state.Pool
      }

    member inline _.Combine (pool: IClientFactory<'c>, state : FullStep<_,'f>) =
      { Generic = { Feed = state.Generic.Feed
                    Pool = Some pool
                  }
        Timeout = state.Timeout
        Execute = state.Execute
        DoNotTrack = state.DoNotTrack
      }

    member inline _.Combine (state : FullStep<_,'f>, feed: IFeed<'f>) =
      { Generic = { Feed = Some feed
                    Pool = state.Generic.Pool
                  }
        Timeout = state.Timeout
        Execute = state.Execute
        DoNotTrack = state.DoNotTrack
      }

    [<CustomOperation "pause">]
    member inline __.Pause(state : StepEmpty<'c,'f>, timeSpan: TimeSpan) =
       { __.Execute(state, fun _ -> Task.Delay timeSpan) with
             DoNotTrack = true }
    member inline __.Pause(state : StepEmpty<'c,'f>, millis: int) =
      { __.Execute(state, fun _ -> Task.Delay millis) with
            DoNotTrack = true }

    /// Overwrite default 1s step execution timeout time with the specified value
    [<CustomOperation "timeout">]
    member inline __.TimeOut(state : FullStep<'c,'f>, timeout: TimeSpan) =
       { state with Timeout = timeout }
    /// Overwrite default 1s step execution timeout time with the specified value
    member inline __.TimeOut(state : FullStep<'c,'f>, millis: int) =
      { state with Timeout = TimeSpan.FromMilliseconds(float millis) }

    [<CustomOperation "doNotTrack">]
    member inline _.DoNotTrack(state : FullStep<'c,'f>) =
        { state with DoNotTrack = true }

    member _.Run(state : FullStep<'c,'f>) =
      match state.Generic with
      | { Pool = None; Feed = None } ->
        Step.create(name, state.Execute, timeout = state.Timeout, doNotTrack = state.DoNotTrack)
      | { Pool = None; Feed = Some feed } ->
        Step.create(name, state.Execute, feed = feed, timeout = state.Timeout, doNotTrack = state.DoNotTrack)
      | { Pool = Some pool; Feed = None } ->
          Step.create(
            name,
            state.Execute,
            clientFactory = pool,
            timeout = state.Timeout,
            doNotTrack = state.DoNotTrack)
      | { Pool = Some pool; Feed = Some feed } ->
          Step.create(
            name,
            state.Execute,
            clientFactory = pool,
            feed = feed,
            timeout = state.Timeout,
            doNotTrack = state.DoNotTrack)
