namespace NBomber.FSharp

open System
open System.Threading.Tasks
open FSharp.Control.Tasks.NonAffine
open NBomber.Contracts


type FullStep<'c, 'f> =
    { Feed: IFeed<'f>
      Pool: IConnectionPoolArgs<'c>
      DoNotTrack: bool
      Execute: (IStepContext<'c, 'f> -> Response Task) }

type StepBuilder(name: string) =
    inherit StepEmptyBuilder()

    [<CustomOperation "execute">]
    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Response Task) =
        { Feed = state.Feed
          Pool = state.Pool
          Execute = exe
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Response) =
        { Feed = state.Feed
          Pool = state.Pool
          Execute = fun ctx -> task { return exe ctx }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Response Async) =
        { Feed = state.Feed
          Pool = state.Pool
          Execute = fun ctx -> task { return! exe ctx }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> Task) =
        { Feed = state.Feed
          Pool = state.Pool
          Execute = fun ctx -> task {
            do! exe ctx
            return Response.ok()
          }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> unit) =
        { Feed = state.Feed
          Pool = state.Pool
          Execute = fun ctx -> task {
            do exe ctx
            return Response.ok()
          }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> unit Task) =
        { Feed = state.Feed
          Pool = state.Pool
          Execute = fun ctx -> task {
            do! exe ctx
            return Response.ok()
          }
          DoNotTrack = false
        }

    member inline _.Execute (state : StepEmpty<'c,'f>, exe : IStepContext<'c,'f> -> unit Async) =
        { Feed = state.Feed
          Pool = state.Pool
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

    [<CustomOperation "doNotTrack">]
    member inline _.DoNotTrack(state : FullStep<'c,'f>) =
        { state with DoNotTrack = true }

    member _.Run(state : FullStep<'c,'f>) =
        Step.create(name, state.Execute, state.Pool, state.Feed, state.DoNotTrack)
