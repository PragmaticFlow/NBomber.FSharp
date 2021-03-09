[<AutoOpen>]
module NBomber.FSharp.Internals

open NBomber.Contracts
open System.Threading.Tasks

type ConnectionPoolContext<'T> =
    { Connect: int -> IBaseContext -> Task<'T>
      Disconnect: 'T -> IBaseContext -> Task<unit>
      Count : int option
    }
    static member Empty =
        { Connect = fun _ _ -> Task.FromResult()
          Disconnect = fun _ _ -> Task.FromResult()
          Count = None
        }

let inline connect (ctx: ConnectionPoolContext<'A>)
                   (f: int -> IBaseContext -> Task<'B>) =
    { Connect = f
      Disconnect = fun _ _ -> Task.FromResult()
      Count = ctx.Count
    }

let zeroContext = NBomberRunner.registerScenarios []


let inline checkFailureRate failureRate nodeStats =
    nodeStats.RequestCount <> 0
    && (float nodeStats.FailCount / float nodeStats.RequestCount) <= failureRate

let inline getExitCode ctx (runResult: Result<NodeStats, string>) =
    match runResult with
    | Error e ->
        eprintf """Error in "%s"/"%s":\n%A""" ctx.TestSuite ctx.TestName e
        1
    | Ok stats ->
        if checkFailureRate 0.05 stats then 0 else 1

let inline orIfDefault defaultValue otherValue value =
    if value = defaultValue then otherValue else value
