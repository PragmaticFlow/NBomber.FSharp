namespace NBomber.FSharp.Http

open System
open System.Diagnostics
open System.Threading.Tasks
open System.Net.Http
open FSharp.Control.Tasks.NonAffine
open NBomber
open NBomber.Contracts
open NBomber.FSharp
open Microsoft.Extensions.DependencyInjection
open Serilog

type HttpStepRequest<'c, 'f> =
    { Generic: StepEmpty<'c, 'f>
      DoNotTrack: bool
      Execute: IStepContext<'c, 'f> -> Task<HttpRequestMessage>
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
            Response.ok(sizeBytes = headersSize + bodySize, latencyMs = latencyMs)
            |> Task.FromResult
        else
            Response.fail(response.ReasonPhrase)
            |> Task.FromResult

    let httpClientFactory =
        ServiceCollection().AddHttpClient().BuildServiceProvider().GetService<IHttpClientFactory>()
    let inline defaultHttpClientFactory name =
        httpClientFactory.CreateClient name

type HttpStepBuilder(name: string) =
    inherit StepEmptyBuilder()

    [<CustomOperation "doNotTrack">]
    member inline _.DoNotTrack(state : HttpStepRequest<'a,'b>) =
        { state with DoNotTrack = true }

    [<CustomOperation "dataFeed">]
    member inline _.WithFeed(state : StepEmpty<'c,_>, feed) : StepEmpty<'c,'f> =
        { Feed = feed
          Pool = state.Pool
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

    /// provide a function to create request message
    [<CustomOperation "execute">]
    member _.Execute(incomplete: StepEmpty<'c, 'f>, execute: IStepContext<'c,'f> -> Task<HttpRequestMessage>): HttpStepRequest<'c,'f> =
        { Generic = incomplete
          Execute = execute
          Version = Version "2.0"
          CompletionOption = HttpCompletionOption.ResponseHeadersRead
          HttpClientFactory = defaultHttpClientFactory
          Checks = []
          WithRequest = []
          WithResponse = []
          DoNotTrack = false
        }

    member inline __.Execute(state: StepEmpty<'c, 'f>, httpMsg) =
        __.Execute(state, httpMsg >> Task.FromResult)
    member inline __.Execute(state, httpMsg: Task<HttpRequestMessage>) =
        __.Execute(state, fun _ -> httpMsg)
    member inline __.Execute(state, httpMsg: HttpRequestMessage) =
        __.Execute(state, fun _ -> Task.FromResult httpMsg)

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

    // member _.Zero() = empty
    member inline __.Yield(()) = __.Zero()
    member inline __.Yield(pool: IClientFactory<'c>) =
        { Feed = None; Pool = Some pool }
    member inline __.Yield(feed: IFeed<_>) =
        { Feed = Some feed; Pool = None }
    member inline _.Delay f = f()
    member inline __.Yield(httpMsg: HttpRequestMessage): HttpStepRequest<unit,unit> =
        __.Execute(__.Zero(), httpMsg)

    member _.Run(state: HttpStepRequest<'c,'f>) =
        let action (ctx: IStepContext<'c,'f>) =
            task {
                let! request = state.Execute ctx
                request.Version <- state.Version

                for withRequest in state.WithRequest do
                    do! withRequest ctx request

                let sw = Stopwatch.StartNew()

                try
                    let! response =
                        state.HttpClientFactory(string ctx.ScenarioInfo.ScenarioName)
                            .SendAsync(request, state.CompletionOption, ctx.CancellationToken)

                    sw.Stop()
                    let latencyMs = float sw.ElapsedMilliseconds

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
                        return Response.fail checkErrors
                with
                    | :? TaskCanceledException when ctx.CancellationToken.IsCancellationRequested ->
                        return Response.ok()
                    | ex -> return Response.fail(ex)
            }

        match state.Generic with
        | { Pool = None; Feed = None } ->
          Step.create(name, execute = action, doNotTrack = state.DoNotTrack)
        | { Pool = None; Feed = Some feed } ->
          Step.create(name, execute = action, feed = feed, doNotTrack = state.DoNotTrack)
        | { Pool = Some pool; Feed = None } ->
            Step.create(
              name,
              execute = action,
              clientFactory = pool,
              doNotTrack = state.DoNotTrack)
        | { Pool = Some pool; Feed = Some feed } ->
            Step.create(
              name,
              execute = action,
              clientFactory = pool,
              feed = feed,
              doNotTrack = state.DoNotTrack)
        // Step.create (
        //     name,
        //     execute = action,
        //     feed = state.Feed,
        //     clientFactory = state.Pool,
        //     doNotTrack = state.DoNotTrack)

[<AutoOpen>]
module Builders =

    /// creates a step builder for http requests
    let httpStep = HttpStepBuilder
