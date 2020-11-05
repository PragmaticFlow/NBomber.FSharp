namespace NBomber.FSharp.Http

open System
open System.Diagnostics
open System.Threading.Tasks
open System.Net.Http
open FSharp.Control.Tasks.V2.ContextInsensitive
open NBomber
open NBomber.Contracts
open NBomber.FSharp
open Microsoft.Extensions.DependencyInjection
open Serilog

type HttpStepRequest<'c, 'f> =
    { Name: string
      Feed: IFeed<'f>
      Pool: IConnectionPoolArgs<'c>
      DoNotTrack: bool
      CreateRequest: IStepContext<'c, 'f> -> Task<HttpRequestMessage>
      Version: Version
      CompletionOption: HttpCompletionOption
      HttpClientFactory: string -> HttpClient
      Checks: (HttpResponseMessage -> Task<string option>) list
      WithRequest: (IStepContext<'c, 'f> -> HttpRequestMessage -> unit Task) list
      WithResponse: (IStepContext<'c, 'f> -> HttpResponseMessage -> unit Task) list
    }

/// module for private functions, because class is bad with type inferences
[<AutoOpen>]
module private HttpStepInternals =
    open System.Collections.Generic

    let inline addPairs headers stateHeaders =
        let mutable ret = stateHeaders
        for k,v in headers do
            ret <- stateHeaders |> Map.add k v
        ret

    let inline createFormData formData =
        formData
        |> Seq.map(fun (k,v) -> KeyValuePair(k,v))
        |> Seq.toArray
        |> fun d -> new FormUrlEncodedContent(d)
        :> HttpContent

    let inline logRequest (ctx: IStepContext<_,_>) (req: HttpRequestMessage) =
        if ctx.Logger.IsEnabled Events.LogEventLevel.Verbose then
            let body = if isNull req.Content then "" else req.Content.ReadAsStringAsync().Result
            ctx.Logger.Verbose("\n [REQUEST]: \n {0} \n [REQ_BODY] \n {1} \n", req.ToString(), body)
        Task.FromResult()

    let inline logResponse (ctx: IStepContext<_,_>) (res: HttpResponseMessage) =
        if ctx.Logger.IsEnabled Events.LogEventLevel.Verbose then
            let body = if isNull res.Content then "" else res.Content.ReadAsStringAsync().Result
            ctx.Logger.Verbose("\n [RESPONSE]: \n {0} \n [RES_BODY] \n {1} \n", res.ToString(), body)
        Task.FromResult()

    let inline handleResponse (response: HttpResponseMessage) latencyMs =
        if response.IsSuccessStatusCode then
            let headersSize = response.Headers.ToString().Length
            let bodySize =
                if response.Content.Headers.ContentLength.HasValue
                then int response.Content.Headers.ContentLength.Value
                else 0
            Response.Ok(sizeBytes = headersSize + bodySize, latencyMs = int latencyMs)
            |> Task.FromResult
        else
            Response.Fail(response.ReasonPhrase)
            |> Task.FromResult

    let httpClientFactory =
        ServiceCollection().AddHttpClient().BuildServiceProvider().GetService<IHttpClientFactory>()
    let inline defaultHttpClientFactory name =
        httpClientFactory.CreateClient name

type HttpStepBuilder(name: string) =


    let empty =
        { Name = name
          Feed = Feed.empty
          Pool = ConnectionPoolArgs.empty
        }
    // let empty: HttpStepIncomplete =
    //     { HttpClientFactory = fun () -> defaultClientFactory.CreateClient name
    //       CompletionOption = HttpCompletionOption.ResponseHeadersRead
    //       Checks = []
    //     }

    [<CustomOperation "doNotTrack">]
    member inline _.DoNotTrack(state : HttpStepRequest<'a,'b>) =
        { state with DoNotTrack = true }

    [<CustomOperation "dataFeed">]
    member inline _.WithFeed(state : IncompleteStep<'c,_>, feed) : IncompleteStep<'c,'f> =
        { Name = state.Name
          Feed = feed
          Pool = state.Pool
        }

    [<CustomOperation "connectionPool">]
    member inline _.WithPool(state : IncompleteStep<_,'f>, pool : IConnectionPoolArgs<'c>) : IncompleteStep<'c,'f> =
        { Name = state.Name
          Feed = state.Feed
          Pool = pool
        }

    /// Add a routine to call on request before it is fired
    [<CustomOperation "prepare">]
    member inline _.WithRequest(state : HttpStepRequest<'a,'b>, requestTask) =
        { state with WithRequest = requestTask::state.WithRequest }
    member inline __.WithRequest(state : HttpStepRequest<_,_>, requestAction) =
        let requestTask ctx request = task { return requestAction ctx request }
        { state with WithRequest = requestTask::state.WithRequest }


    /// Add a routine to call on response
    [<CustomOperation "handle">]
    member inline _.WithResponse(state : HttpStepRequest<'a,'b>, responseTask) =
        { state with WithResponse= responseTask::state.WithResponse }
    member inline __.WithResponse(state : HttpStepRequest<_,_>, action) =
        let responseTask ctx request = task { return action ctx request }
        { state with WithResponse = responseTask::state.WithResponse }

    /// Set the http version, default is 2.0
    [<CustomOperation "version">]
    member inline _.Version(state: HttpStepRequest<'c,'f>, version: string) =
        { state with Version = Version version }

    /// provide a function to create request message instead of specifying each parameter
    [<CustomOperation "create">]
    member _.CreateRequest(incomplete: IncompleteStep<'c, 'f>, createRequest: IStepContext<'c,'f> -> Task<HttpRequestMessage>): HttpStepRequest<'c,'f> =
        { Name = incomplete.Name
          Feed = incomplete.Feed
          Pool = incomplete.Pool
          CreateRequest = createRequest
          Version = Version "2.0"
          CompletionOption = HttpCompletionOption.ResponseHeadersRead
          HttpClientFactory = defaultHttpClientFactory
          Checks = []
          WithRequest = []
          WithResponse = []
          DoNotTrack = false
        }

    member inline __.CreateRequest(state: IncompleteStep<'c, 'f>, httpMsg) =
        __.CreateRequest(state, httpMsg >> Task.FromResult)
    member inline __.CreateRequest(state, httpMsg: Task<HttpRequestMessage>) =
        __.CreateRequest(state, fun _ -> httpMsg)
    member inline __.CreateRequest(state, httpMsg: HttpRequestMessage) =
        __.CreateRequest(state, fun _ -> Task.FromResult httpMsg)

    /// Provide a http client instance to use instead of created by default factory
    [<CustomOperation "httpClient">]
    member inline _.HttpClient(state: HttpStepRequest<'c,'f>, httpClient) =
        { state with HttpClientFactory = fun _ -> httpClient }

    /// Provide a http client factory instead of default one
    [<CustomOperation "clientFactory">]
    member inline _.HttpClientFactory(state: HttpStepRequest<'c,'f>, httpClientFactory) =
        { state with HttpClientFactory = httpClientFactory }
    member inline _.HttpClientFactory(state: HttpStepRequest<'c,'f>, httpClientFactory: IHttpClientFactory) =
        { state with HttpClientFactory = httpClientFactory.CreateClient }

    /// Provide a response check function
    [<CustomOperation "check">]
    member inline __.WithCheck(state: HttpStepRequest<'c,'f>, errorMessage: string, check: HttpResponseMessage -> Task<bool>) =
        let checkTask response =
            task {
                let! isOk = check response
                if isOk then return None
                else return Some errorMessage
            }
        { state with Checks = checkTask :: state.Checks }
    member inline __.WithCheck(state: HttpStepRequest<'c,'f>, errorMessage: string, check: HttpResponseMessage -> bool) =
        let checkTask response =
            task {
                let isOk = check response
                if isOk then return None
                else return Some errorMessage
            }
        { state with Checks = checkTask :: state.Checks }

    [<CustomOperation "logRequest">]
    member inline _.LogRequest(state : HttpStepRequest<'c,'f>) =
        { state with WithRequest = logRequest::state.WithRequest }

    [<CustomOperation "logResponse">]
    member inline _.LogResponse(state : HttpStepRequest<'c,'f>) =
        { state with WithResponse = logResponse::state.WithResponse }

    member _.Zero() = empty
    member inline __.Yield(()) = __.Zero()
    member inline _.Delay f = f()
    member inline __.Yield(httpMsg: HttpRequestMessage): HttpStepRequest<unit,unit> =
        __.CreateRequest(__.Zero(), httpMsg)
    member inline _.Combine(state: IncompleteStep<'c, 'f>, state2: IncompleteStep<'c, 'f>) =
        {   Name = state.Name
            Feed = if box state2.Feed = box Feed.empty then state.Feed else state2.Feed
            Pool = if box state2.Pool = box ConnectionPoolArgs.empty then state.Pool else state2.Pool
        }
    member __.Combine(state: HttpStepRequest<'c,'f>, state2 : HttpStepRequest<'c, 'f>) =
        { Name = state.Name
          Feed = if box state2.Feed = box Feed.empty then state.Feed else state2.Feed
          Pool = if box state2.Pool = box ConnectionPoolArgs.empty then state.Pool else state2.Pool
          CreateRequest = state.CreateRequest
          Version = Version "2.0"
          CompletionOption = HttpCompletionOption.ResponseHeadersRead
          HttpClientFactory = defaultHttpClientFactory
          Checks = state.Checks |> List.append state2.Checks
          WithRequest = state.WithRequest |> List.append state2.WithRequest
          WithResponse = state.WithResponse |> List.append state2.WithResponse
          DoNotTrack = state.DoNotTrack || state2.DoNotTrack
        }

    member inline __.Combine(state: HttpStepRequest<'c,'f>, state2 : IncompleteStep<'c, 'f>) =
         { state with
            Feed = if box state2.Feed = box Feed.empty then state.Feed else state2.Feed
            Pool = if box state2.Pool = box ConnectionPoolArgs.empty then state.Pool else state2.Pool
         }
    member inline __.Combine(state: IncompleteStep<'c,'f>, state2 : HttpStepRequest<'c, 'f>) =
         { state with
            Feed = if box state2.Feed = box Feed.empty then state.Feed else state2.Feed
            Pool = if box state2.Pool = box ConnectionPoolArgs.empty then state.Pool else state2.Pool
         }

    member inline __.For (state: IncompleteStep<'c,'f>, f: unit -> HttpStepRequest<'c,'f>) =
        __.Combine(state, f())
    member inline __.For (state: HttpStepRequest<'c,'f>, f: unit -> IncompleteStep<'c,'f>) =
        __.Combine(state, f())
    member inline __.For (state: HttpStepRequest<'c,'f>, f: unit -> HttpStepRequest<'c,'f>) =
        __.Combine(state, f())
    member inline __.For (xs: seq<'T>, f: 'T -> HttpStepRequest<'c,'f>) =
        xs
        |> Seq.map f
        |> Seq.reduce (fun a b -> __.Combine(a,b))

    member _.Run(state: HttpStepRequest<'c,'f>) =
        let action (ctx: IStepContext<'c,'f>) =
            task {
                let! request = state.CreateRequest ctx
                request.Version <- state.Version

                for withRequest in state.WithRequest do
                    do! withRequest ctx request

                let sw = Stopwatch.StartNew()

                let! response =
                    state.HttpClientFactory(sprintf "%A" ctx.CorrelationId)
                        .SendAsync(request, state.CompletionOption, ctx.CancellationToken)

                sw.Stop()
                let latencyMs = sw.ElapsedMilliseconds

                for withResponse in state.WithResponse do
                    do! withResponse ctx response

                let! checkResults =
                    state.Checks
                    |> List.map (fun check -> check response)
                    |> Task.WhenAll
                let checkErrors = checkResults |> Array.choose id |> String.concat "; "

                if String.IsNullOrEmpty checkErrors then
                    return! handleResponse response latencyMs
                else
                    return Response.Fail checkErrors
            }

        Step.create (
            name,
            feed = state.Feed,
            connectionPoolArgs = state.Pool,
            execute = action,
            doNotTrack = state.DoNotTrack)

[<AutoOpen>]
module Builders =

    /// creates a step builder for http requests
    let httpStep = HttpStepBuilder
