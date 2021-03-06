namespace NBomber.FSharp

open System.Threading.Tasks
open FSharp.Control.Tasks.NonAffine
open NBomber.Contracts


type ClientBuilder(name: string) =
    /// number of connections in the pool
    [<CustomOperation "count">]
    member inline _.Count(ctx: ClientContext<'a>, count ) =
        { ctx with Count = Some count }

    [<CustomOperation "connect">]
    member inline __.Connect(ctx: ClientContext<'a>, f: int -> IBaseContext -> Task<'b>) : ClientContext<'b> =
        connect ctx f
    member inline __.Connect(ctx, f: unit -> Task<'T>) =
        connect ctx (fun _ _ -> f())

    member inline __.Connect(ctx, f) =
        connect ctx (fun nr ctx ->  f nr ctx |> Async.StartAsTask)
    member inline __.Connect(ctx, f: unit -> Async<'T>) =
        connect ctx (fun _ _ -> f() |> Async.StartAsTask )

    // member inline __.Connect(ctx, f: unit -> 'T) =
    //     connect ctx (fun _ _ -> task { return f()})
    // member inline __.Connect(ctx, f) =
    //     connect ctx (fun nr _ -> task { return f nr })
    // member inline __.Connect(ctx, f) =
    //     connect ctx (fun _ ctx -> task { return f ctx })
    // member inline __.Connect(ctx, f) =
    //     connect ctx (fun nr ctx -> task { return f nr ctx })

    /// destroy connection after usage
    [<CustomOperation "disconnect">]
    member inline __.Disconnect(ctx, f: 'a -> IBaseContext -> Task) =
        { ctx with Disconnect = fun c ctx -> task { do! f c ctx } }
    member inline __.Disconnect(ctx, f: 'a -> IBaseContext -> Task<unit>) =
        { ctx with Disconnect = f }
    member inline __.Disconnect(ctx, f: 'a -> IBaseContext -> Async<unit>) =
        { ctx with Disconnect = fun c ctx -> f c ctx |> Async.StartAsTask }

    member inline __.Disconnect(ctx, f: 'a -> Task) =
        { ctx with Disconnect = fun c _ -> task { do! f c } }
    member inline __.Disconnect(ctx, f: 'a  -> Task<unit>) =
        { ctx with Disconnect = fun c _ -> f c }
    member inline __.Disconnect(ctx, f: 'a -> Async<unit>) =
        { ctx with Disconnect = fun c _ -> f c |> Async.StartAsTask }

    member inline __.Disconnect(ctx, f: 'a -> IBaseContext -> unit) =
        { ctx with Disconnect = fun c ctx -> task { do f c ctx }}
    member inline __.Disconnect(ctx, f: 'a -> unit) =
        { ctx with Disconnect = fun c _ -> task { do f c }}

    member __.Zero() = ClientContext<_>.Empty
    member inline __.Yield(()) = __.Zero()
    member inline _.Delay f = f()
    member __.Run(ctx: ClientContext<'a>) : IClientFactory<'a> =
        match ctx.Count with
        | None ->
            ClientFactory.create(
                name = name,
                initClient = (fun (i, b) -> ctx.Connect i b),
                disposeClient = (fun (c, b) -> ctx.Disconnect c b)
            )
        | Some count ->
            ClientFactory.create(
                name = name,
                initClient = (fun (i, b) -> ctx.Connect i b),
                disposeClient = (fun (c, b) -> ctx.Disconnect c b),
                clientCount = count)

