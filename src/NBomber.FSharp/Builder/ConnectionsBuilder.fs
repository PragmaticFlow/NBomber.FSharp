namespace NBomber.FSharp

open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open NBomber.Contracts

type ConnectionPoolContext<'T> =
    { Connect: int -> CancellationToken -> Task<'T>
      Disconnect: 'T -> CancellationToken -> Task
      Count : int option
    }
    static member Empty =
        { Connect = fun _ _ -> Task.FromResult()
          Disconnect = fun _ _ -> Task.CompletedTask
          Count = None
        }

[<AutoOpen>]
module private ConnectionPoolInternals =

    let inline connect (ctx: ConnectionPoolContext<'A>)
                      (f: int -> CancellationToken -> Task<'B>) =
        { Connect = f
          Disconnect = fun _ _ -> Task.CompletedTask
          Count = ctx.Count
        }

type ConnectionPoolBuilder(name: string) =
    /// number of connections in the pool
    [<CustomOperation "count">]
    member inline _.Count(state, count ) =
        { state with Count = Some count }

    [<CustomOperation "connect">]
    member inline __.Connect(ctx: ConnectionPoolContext<'a>, f: int -> CancellationToken -> Task<'b>) : ConnectionPoolContext<'b> =
        connect ctx f
    member inline __.Connect(ctx, f: unit -> Task<'T>) =
        connect ctx (fun _ _ -> f())

    member inline __.Connect(ctx, f) =
        connect ctx (fun nr token ->  f nr token |> Async.StartAsTask)
    member inline __.Connect(ctx, f: unit -> Async<'T>) =
        connect ctx (fun _ _ -> f() |> Async.StartAsTask )

    // member inline __.Connect(ctx, f: unit -> 'T) =
    //     connect ctx (fun _ _ -> task { return f()})
    // member inline __.Connect(ctx, f) =
    //     connect ctx (fun nr _ -> task { return f nr })
    // member inline __.Connect(ctx, f) =
    //     connect ctx (fun _ token -> task { return f token })
    // member inline __.Connect(ctx, f) =
    //     connect ctx (fun nr token -> task { return f nr token })

    /// destroy connection after usage
    [<CustomOperation "disconnect">]
    member inline __.Disconnect(ctx, f: 'a -> CancellationToken -> Task) =
        { ctx with Disconnect = f }
    member inline __.Disconnect(ctx, f: 'a -> CancellationToken -> Task<unit>) =
        { ctx with Disconnect = fun c token -> f c token :> Task  }
    member inline __.Disconnect(ctx, f: 'a -> CancellationToken -> Async<unit>) =
        { ctx with Disconnect = fun c token -> f c token |> Async.StartAsTask :> Task }

    member inline __.Disconnect(ctx, f: 'a -> Task) =
        { ctx with Disconnect = fun c _ -> f c }
    member inline __.Disconnect(ctx, f: 'a -> Task<unit>) =
        { ctx with Disconnect = fun c _ -> f c :> Task }
    member inline __.Disconnect(ctx, f: 'a -> Async<unit>) =
        { ctx with Disconnect = fun c _ -> f c |> Async.StartAsTask :> Task }

    member inline __.Disconnect(ctx, f: 'a -> CancellationToken -> unit) =
        { ctx with Disconnect = fun c token -> task { do f c token } :> Task}
    member inline __.Disconnect(ctx, f: 'a -> unit) =
        { ctx with Disconnect = fun c _ -> task { do f c } :> Task}

    member __.Zero() = ConnectionPoolContext<_>.Empty
    member inline __.Yield(()) = __.Zero()
    member inline _.Delay f = f()
    member __.Run(ctx: ConnectionPoolContext<'a>) : IConnectionPoolArgs<'a> =
        match ctx.Count with
        | None ->
            ConnectionPoolArgs.create(
                    name = name,
                    openConnection = (fun (nr, token) -> ctx.Connect nr token),
                    closeConnection = (fun (c, token) -> ctx.Disconnect c token)
            )
        | Some count ->
            ConnectionPoolArgs.create(
                    name = name,
                    openConnection = (fun (nr, token) -> ctx.Connect nr token),
                    closeConnection = (fun (c, token) -> ctx.Disconnect c token),
                    connectionCount = count)

