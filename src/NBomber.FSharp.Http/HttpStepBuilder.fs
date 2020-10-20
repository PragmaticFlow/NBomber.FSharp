namespace NBomber.Plugins.Http

open System
open System.Diagnostics
open System.Threading.Tasks
open System.Net.Http
open FSharp.Control.Tasks.V2.ContextInsensitive
open NBomber.Contracts
open NBomber.FSharp
open Microsoft.Extensions.DependencyInjection
open Serilog


type HttpStepIncomplete =
    { HttpClientFactory: unit -> HttpClient
      CompletionOption: HttpCompletionOption
      Checks: (HttpResponseMessage -> Task<Response>) list
    }

type HttpStepRequest<'a, 'b> =
    { CreateRequest: IStepContext<'a, 'b> -> Task<HttpRequestMessage>
      Version: Version
      CompletionOption: HttpCompletionOption
      HttpClientFactory: unit -> HttpClient
      Checks: (HttpResponseMessage -> Task<bool>) list
      WithRequest: (IStepContext<'a, 'b> -> HttpRequestMessage -> unit Task) list
      WithResponse: (IStepContext<'a,'b> -> HttpResponseMessage -> unit Task) list
    }

/// module for private functions, because class is bad with type inferences
[<AutoOpen>]
module private Internals =
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

    let defaultClientFactory =
        ServiceCollection().AddHttpClient().BuildServiceProvider().GetService<IHttpClientFactory>()

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

    let handleResponse (response: HttpResponseMessage) latencyMs =
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

type HttpStepBuilder(name: string) =

    let empty: HttpStepIncomplete =
        { HttpClientFactory = fun () -> defaultClientFactory.CreateClient name
          CompletionOption = HttpCompletionOption.ResponseHeadersRead
          Checks = []
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
    member inline _.CreateRequest(state: HttpStepIncomplete, createRequest: IStepContext<'c,'f> -> Task<HttpRequestMessage>): HttpStepRequest<'c,'f> =
        { CreateRequest = createRequest
          Version = Version "2.0"
          CompletionOption = HttpCompletionOption.ResponseHeadersRead
          HttpClientFactory = state.HttpClientFactory
          Checks = []
          WithRequest = []
          WithResponse = []
        }

    member inline __.CreateRequest(state: HttpStepIncomplete, httpMsg) =
        __.CreateRequest(state, httpMsg >> Task.FromResult)
    member inline __.CreateRequest(state, httpMsg: Task<HttpRequestMessage>) =
        __.CreateRequest(state, fun _ -> httpMsg)
    member inline __.CreateRequest(state, httpMsg: HttpRequestMessage) =
        __.CreateRequest(state, fun _ -> Task.FromResult httpMsg)

    /// Provide a http client instance to use instead of created by default factory
    [<CustomOperation "httpClient">]
    member inline _.HttpClient(state: HttpStepRequest<_,_>, httpClient) =
        { state with HttpClientFactory = fun () -> httpClient }

    /// Provide a http client factory instead of default one
    [<CustomOperation "clientFactory">]
    member inline _.HttpClientFactory(state: HttpStepRequest<_,_>, httpClientFactory) =
        { state with HttpClientFactory = httpClientFactory }
    member inline _.HttpClientFactory(state: HttpStepRequest<_,_>, httpClientFactory: IHttpClientFactory) =
        { state with HttpClientFactory = httpClientFactory.CreateClient }

    /// Provide a response check function
    [<CustomOperation "check">]
    member inline __.WithCheck(state: HttpStepRequest<_,_>, check: HttpResponseMessage -> Task<bool>, errorMessage: string) =
        { state with Checks = check :: state.Checks }
    member inline __.WithCheck(state: HttpStepRequest<_,_>, check: HttpResponseMessage -> bool, errorMessage: string) =
        let checkTask response = task { return check response }
        { state with Checks = checkTask :: state.Checks }
    // TODO involve error message into check

    [<CustomOperation "logRequest">]
    member inline _.LogRequest(state : HttpStepRequest<_,_>) =
        { state with WithRequest = logRequest::state.WithRequest }

    [<CustomOperation "logResponse">]
    member inline _.LogResponse(state : HttpStepRequest<_,_>) =
        { state with WithResponse = logResponse::state.WithResponse }

    member _.Zero() = empty
    member inline __.Yield(()) = __.Zero()
    member inline _.Delay f = f()
    member inline __.Yield(httpMsg: HttpRequestMessage): HttpStepRequest<unit,unit> =
        __.CreateRequest(__.Zero(), httpMsg)
    member inline __.Combine(state: HttpStepRequest<_,_>, state2 : HttpStepIncomplete) =
        printfn "Combine(%O, %O)" state state2
        state // TODO merge them ?
    // member inline __.Combine(state: HttpStepIncomplete, state2 : HttpStepRequest<_,_>) =
    //     printfn "Combine(%O, %O)" state state2
    //     state2 // TODO merge them
    member inline __.Combine(state: HttpStepIncomplete, httpMsg : HttpRequestMessage) =
        printfn "Combine(%O, %O)" state httpMsg
        __.CreateRequest(state, httpMsg)
    member _.Run(state: HttpStepRequest<_, _>) =
        let action (ctx: IStepContext<_, _>) =
            task {
                let! request = state.CreateRequest ctx
                request.Version <- state.Version

                for withRequest in state.WithRequest do
                    do! withRequest ctx request

                let sw = Stopwatch.StartNew()

                let! response =
                    state.HttpClientFactory().SendAsync(request, state.CompletionOption, ctx.CancellationToken)

                sw.Stop()
                let latencyMs = sw.ElapsedMilliseconds

                for withResponse in state.WithResponse do
                    do! withResponse ctx response

                let! checkResults =
                    state.Checks
                    |> List.map (fun check -> check response)
                    |> Task.WhenAll

                if checkResults |> Array.forall id then
                    return! handleResponse response latencyMs
                else
                    return Response.Fail("not satisfied response checks")
            }

        Step.create (name, execute = action)

[<AutoOpen>]
module Builders =

    /// creates a step builder for http requests
    let httpStep = HttpStepBuilder
