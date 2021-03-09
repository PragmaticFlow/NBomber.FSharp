namespace NBomber.FSharp

open System.Threading.Tasks
open NBomber.Contracts


type StepEmpty<'c, 'f> =
    { Feed: IFeed<'f>
      Pool: IConnectionPoolArgs<'c> }

type StepEmptyBuilder() =
    let zero =
        let ignoreTask _ = Task.FromResult()
        { Feed = Feed.createConstant "empty" [()]
          Pool = ConnectionPoolArgs.create("empty", ignoreTask, ignoreTask)
        }

    member inline __.Combine(state: StepEmpty<'c, 'f>, state2: StepEmpty<'c, 'f>) =
        let zero = __.Zero()
        { Feed = if box state2.Feed = box zero.Feed then state.Feed else state2.Feed
          Pool = if box state2.Pool = box zero.Pool then state.Pool else state2.Pool
        }
    member inline __.Combine(state: StepEmpty<'c, 'f>, state2: StepEmpty<unit, 'f>) =
        let zero = __.Zero()
        { Feed = if box state2.Feed = box zero.Feed then state.Feed else state2.Feed
          Pool = state.Pool
        }
    member inline __.Combine(state: StepEmpty<'c, 'f>, state2: StepEmpty<'c, unit>) =
        let zero = __.Zero()
        { Feed = state.Feed
          Pool = if box state2.Pool = box zero.Pool then state.Pool else state2.Pool
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

    member __.Zero() = zero
    member inline __.Yield (()) = __.Zero()
    member inline __.Yield(pool : IConnectionPoolArgs<'c>) =
      { Feed = __.Zero().Feed
        Pool = pool
      }
    member inline _.Delay f = f()
