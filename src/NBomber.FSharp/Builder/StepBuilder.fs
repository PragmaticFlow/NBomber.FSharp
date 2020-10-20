namespace NBomber.FSharp

open System
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open NBomber
open NBomber.Contracts


type IncompleteStep<'c, 'f> =
    { Name: string
      Feed: IFeed<'f>
      Pool: IConnectionPoolArgs<'c>
      DoNotTrack: bool }

type FullStep<'c, 'f> =
    { Name: string
      Feed: IFeed<'f>
      Pool: IConnectionPoolArgs<'c>
      DoNotTrack: bool
      Execute: (IStepContext<'c, 'f> -> Response Task) }

type StepBuilder(name: string) =
    let empty =
        { Name = name
          Feed = Feed.empty
          Pool = ConnectionPoolArgs.empty
          DoNotTrack = false
        }

    [<CustomOperation "execute">]
    member inline _.Execute (state : IncompleteStep<'c,'f>, exe : IStepContext<'c,'f> -> Response Task) =
        { Name = state.Name
          Feed = state.Feed
          Pool = state.Pool
          Execute = exe
          DoNotTrack = state.DoNotTrack
        }

    member inline _.Execute (state : IncompleteStep<'c,'f>, exe : IStepContext<'c,'f> -> Response Async) =
        { Name = state.Name
          Feed = state.Feed
          Pool = state.Pool
          Execute = fun ctx -> exe ctx |> Async.StartAsTask
          DoNotTrack = state.DoNotTrack
        }

    member inline _.Execute (state : IncompleteStep<'c,'f>, exe : IStepContext<'c,'f> -> Task) =
        { Name = state.Name
          Feed = state.Feed
          Pool = state.Pool
          Execute = fun ctx -> task {
            do! exe ctx
            return Response.Ok()
          }
          DoNotTrack = state.DoNotTrack
        }

    member inline _.Execute (state : IncompleteStep<'c,'f>, exe : IStepContext<'c,'f> -> unit) =
        { Name = state.Name
          Feed = state.Feed
          Pool = state.Pool
          Execute = fun ctx -> task {
            exe ctx
            return Response.Ok()
          }
          DoNotTrack = state.DoNotTrack
        }

    member inline _.Execute (state : IncompleteStep<'c,'f>, exe : IStepContext<'c,'f> -> 'a Task) =
        { Name = state.Name
          Feed = state.Feed
          Pool = state.Pool
          Execute = fun ctx -> task {
            let! _a = exe ctx
            return Response.Ok()
          }
          DoNotTrack = state.DoNotTrack
        }

    [<CustomOperation "pause">]
    member inline __.Pause(state : IncompleteStep<'c,'f>, timeSpan: TimeSpan) =
       { __.Execute(state, fun _ -> Task.Delay timeSpan) with
             DoNotTrack = true }
    member inline __.Pause(state : IncompleteStep<'c,'f>, millis: int) =
      { __.Execute(state, fun _ -> Task.Delay millis) with
            DoNotTrack = true }

    [<CustomOperation "doNotTrack">]
    member inline _.DoNotTrack(state : IncompleteStep<'c,'f>) =
        { state with DoNotTrack = true }
    member inline _.DoNotTrack(state : FullStep<'c,'f>) =
        { state with DoNotTrack = true }

    [<CustomOperation "feed">]
    member inline _.WithFeed(state : IncompleteStep<'c,_>, feed) : IncompleteStep<'c,'f> =
        { Name = state.Name
          Feed = feed
          Pool = state.Pool
          DoNotTrack = state.DoNotTrack
        }

    [<CustomOperation "connectionPool">]
    member inline _.WithPool(state : IncompleteStep<_,'f>, pool : IConnectionPoolArgs<'c>) : IncompleteStep<'c,'f> =
        { Name = state.Name
          Feed = state.Feed
          Pool = pool
          DoNotTrack = state.DoNotTrack
        }

    member _.Zero() = empty
    member inline __.Yield (()) = __.Zero()
    member inline _.Delay f = f()
    member inline _.Run(state : FullStep<'c,'f>) =
        Step.create(state.Name, state.Pool, state.Feed, state.Execute, state.DoNotTrack)
