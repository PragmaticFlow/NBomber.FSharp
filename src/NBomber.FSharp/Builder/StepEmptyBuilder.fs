namespace NBomber.FSharp

open System.Threading.Tasks
open NBomber.Contracts


type StepEmpty<'c, 'f> =
    { Feed: IFeed<'f> option
      Pool: IClientFactory<'c> option }

type StepEmptyBuilder() =

    member inline __.Combine(state: StepEmpty<'c, 'f>, state2: StepEmpty<'c, 'f>) =
      { Pool = state2.Pool |> Option.orElse state.Pool
        Feed = state2.Feed |> Option.orElse state.Feed
      }

    member inline __.Combine(state: StepEmpty<'c, 'f1>, state2: StepEmpty<'c, 'f2>) =
      { Pool = state2.Pool |> Option.orElse state.Pool
        Feed = state2.Feed
      }

    member inline __.Combine(state: StepEmpty<'c1, 'f>, state2: StepEmpty<'c2, 'f>) =
      { Pool = state2.Pool
        Feed = state2.Feed |> Option.orElse state.Feed
      }

    member inline __.Combine(state: StepEmpty<'c1, 'f1>, state2: StepEmpty<'c2, 'f2>) =
      { Pool = state2.Pool
        Feed = state2.Feed
      }

    member inline _.For (state: StepEmpty<unit,'f>, f: unit -> StepEmpty<'c,unit>) =
        { Feed = state.Feed
          Pool = f().Pool }
    member inline _.For (state: StepEmpty<'c,unit>, f: unit -> StepEmpty<unit,'f>) =
        { Feed = f().Feed
          Pool = state.Pool }

    [<CustomOperation "dataFeed">]
    member inline _.WithFeed(state : StepEmpty<'c,_>, feed) : StepEmpty<'c,'f> =
        { Feed = Some feed
          Pool = state.Pool
        }

    [<CustomOperation "connections">]
    member inline _.WithConnection(state: StepEmpty<'c,'f>, c) =
      { Feed = state.Feed
        Pool = Some c
      }

    member _.Zero() =
      { Feed = None
        Pool = None
      }
    member inline __.Yield (()) = __.Zero()
    member inline __.Yield(pool : IClientFactory<'c>) =
      { Feed = None
        Pool = Some pool
      }
    member inline __.Yield(feed : IFeed<'f>) =
      { Feed = Some feed
        Pool = None
      }
    member inline _.Delay f = f()
