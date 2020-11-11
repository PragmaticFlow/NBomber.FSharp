namespace NBomber.FSharp

open NBomber
open NBomber.Contracts


type StepEmpty<'c, 'f> =
    { Feed: IFeed<'f>
      Pool: IConnectionPoolArgs<'c> }

type StepEmptyBuilder() =

    let empty =
        { Feed = Feed.empty
          Pool = ConnectionPoolArgs.empty
        }

    member inline _.Combine(state: StepEmpty<'c, 'f>, state2: StepEmpty<'c, 'f>) =
        { Feed = if box state2.Feed = box Feed.empty then state.Feed else state2.Feed
          Pool = if box state2.Pool = box ConnectionPoolArgs.empty then state.Pool else state2.Pool
        }
    member inline _.Combine(state: StepEmpty<'c, 'f>, state2: StepEmpty<unit, 'f>) =
        { Feed = if box state2.Feed = box Feed.empty then state.Feed else state2.Feed
          Pool = state.Pool
        }
    member inline _.Combine(state: StepEmpty<'c, 'f>, state2: StepEmpty<'c, unit>) =
        { Feed = state.Feed
          Pool = if box state2.Pool = box ConnectionPoolArgs.empty then state.Pool else state2.Pool
        }

    member inline __.For (state: StepEmpty<unit,'f>, f: unit -> StepEmpty<'c,unit>) =
        { Feed = state.Feed; Pool = f().Pool }
    member inline __.For (state: StepEmpty<'c,unit>, f: unit -> StepEmpty<unit,'f>) =
        { Feed = f().Feed; Pool = state.Pool }
    member inline __.For (state: StepEmpty<'c,'f>, f: unit -> StepEmpty<'c,'f>) =
        __.Combine(state, f())

    [<CustomOperation "dataFeed">]
    member inline _.WithFeed(state : StepEmpty<'c,_>, feed) : StepEmpty<'c,'f> =
        { Feed = feed
          Pool = state.Pool
        }

    member _.Zero() = empty
    member inline __.Yield (()) = __.Zero()
    member inline __.Yield(pool : IConnectionPoolArgs<'c>) =
      { Feed = Feed.empty
        Pool = pool }
    member inline _.Delay f = f()
