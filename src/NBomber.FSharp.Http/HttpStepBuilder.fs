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

type HttpStepCreateRequest<'a, 'b> =
    { CreateRequest: IStepContext<'a, 'b> -> Task<HttpRequestMessage>
      CompletionOption: HttpCompletionOption
      HttpClientFactory: unit -> HttpClient
      Checks: (HttpResponseMessage -> Task<bool>) list
      WithRequest: (IStepContext<'a, 'b> -> HttpRequestMessage -> unit Task) list
      WithResponse: (IStepContext<'a,'b> -> HttpResponseMessage -> unit Task) list
    }

type HttpStepRequest<'a, 'b> =
    { Url: Uri
      Version: Version
      Method: HttpMethod
      Headers: Map<string, string>
      WithContent: IStepContext<'a, 'b> -> HttpContent
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

    let empty =
        { HttpClientFactory = fun () -> defaultClientFactory.CreateClient name
          CompletionOption = HttpCompletionOption.ResponseHeadersRead
          Checks = []
        }

    static member DefaultClientFactory = defaultClientFactory

    member _.Zero() = empty
    member _.Yield _ = empty

    /// Create a request with specified method and url
    [<CustomOperation "request">]
    member inline _.Request(state: HttpStepIncomplete, method: string, url: string) =
        { Url = Uri url
          Version = Version(1, 1)
          Method = HttpMethod method
          Headers = Map.empty
          WithContent = (fun _ -> null)
          CompletionOption = HttpCompletionOption.ResponseHeadersRead
          HttpClientFactory = state.HttpClientFactory
          WithRequest = []
          Checks = []
          WithResponse =[]
        }

    /// Create a GET request
    [<CustomOperation "GET">]
    member inline __.GetRequest(state: HttpStepIncomplete, url: string) =
        __.Request(state, "GET", url)

    /// Create a POST request
    [<CustomOperation "POST">]
    member inline __.PostRequest(state: HttpStepIncomplete, url: string, content: string) =
        let req = __.Request(state, "Post", url)
        { req with WithContent = fun _ -> new StringContent(content)  :> HttpContent}
    member inline __.PostRequest(state: HttpStepIncomplete, url: string, content: #HttpContent) =
        let req = __.Request(state, "Post", url)
        { req with WithContent = fun _ -> content :> HttpContent }

    /// Add a routine to call on request before it is fired
    [<CustomOperation "withRequest">]
    member inline _.WithRequest(state : HttpStepRequest<'a,'b>, requestTask) =
        { state with WithRequest = requestTask::state.WithRequest }
    member inline __.WithRequest(state : HttpStepRequest<_,_>, requestAction) =
        let requestTask ctx request = task { return requestAction ctx request }
        { state with WithRequest = requestTask::state.WithRequest }
    member inline _.WithRequest(state : HttpStepCreateRequest<_,_>, requestTask) =
        { state with WithRequest = requestTask::state.WithRequest }
    member inline _.WithRequest(state : HttpStepCreateRequest<_,_>, action) =
        let requestTask ctx request = task { return action ctx request }
        { state with WithRequest = requestTask::state.WithRequest }


    /// Add a routine to call on response
    [<CustomOperation "withResponse">]
    member inline _.WithResponse(state : HttpStepRequest<'a,'b>, responseTask) =
        { state with WithResponse= responseTask::state.WithResponse }
    member inline __.WithResponse(state : HttpStepRequest<_,_>, action) =
        let responseTask ctx request = task { return action ctx request }
        { state with WithResponse = responseTask::state.WithResponse }
    member inline _.WithResponse(state : HttpStepCreateRequest<_,_>, responseTask) =
        { state with WithResponse = responseTask::state.WithResponse }
    member inline _.WithResponse(state : HttpStepCreateRequest<_,_>, action) =
        let responseTask ctx request = task { return action ctx request }
        { state with WithResponse = responseTask::state.WithResponse }

    /// Set the http version, default is 2.0
    [<CustomOperation "version">]
    member inline _.Version(state: HttpStepRequest<_,_>, version: string) =
        { state with Version = Version version }

    /// Add a collection of header values
    [<CustomOperation "headers">]
    member inline _.Headers(state: HttpStepRequest<_,_>, headers) =
        { state with Headers = headers }
    member inline _.Headers(state: HttpStepRequest<_,_>, headers) =
        { state with Headers = state.Headers |> addPairs headers }
    member inline __.Headers(state: HttpStepRequest<_,_>, getHeaders : IStepContext<_,_> -> seq<string*string>) =
        let prepare ctx (request: HttpRequestMessage) =
            getHeaders ctx
            |> Seq.iter (fun (name, value) -> request.Headers.TryAddWithoutValidation(name, value) |> ignore)
            Task.FromResult()
        __.WithRequest(state, prepare)

    /// Add a single header name-value pair
    [<CustomOperation "header">]
    member inline __.Header(state: HttpStepRequest<_,_>, name: string, value: string) =
        __.Headers(state, [name, value])

    /// Add a "x-requestid" header with a guid value
    [<CustomOperation "xRequestId">]
    member inline __.XRequestId(state: HttpStepRequest<_,_>) =
        __.Headers(state, [ "x-requestid", (Guid.NewGuid() |> string) ])

    /// Adds a header: "Authorization: Bearer [token]"
    [<CustomOperation "bearerToken">]
    member inline _.BearerToken(state : HttpStepRequest<_,_>, token) =
        { state with Headers = state.Headers |> addPairs [ "Authorization", "Bearer " + token ] }

    /// Add http content
    [<CustomOperation "content">]
    member inline _.Content(state: HttpStepRequest<_,_>, withContent) =
        { state with WithContent = withContent }
    member inline _.Content(state: HttpStepRequest<_,_>, content) =
        { state with WithContent = fun _ -> content }

    /// Add url-encoded-form-data content
    [<CustomOperation "formData">]
    member _.FormData(state: HttpStepRequest<_,_>, formData) =
        { state with WithContent = fun _ -> createFormData formData }

    /// provide a function to create request message instead of specifying each parameter
    [<CustomOperation "createRequest">]
    member inline _.CreateRequest(state: HttpStepIncomplete, createRequest) =
        { CreateRequest = createRequest
          CompletionOption = HttpCompletionOption.ResponseHeadersRead
          HttpClientFactory = state.HttpClientFactory
          Checks = []
          WithRequest = []
          WithResponse = []
        }

    /// Provide a http client instance to use instead of created by default factory
    [<CustomOperation "httpClient">]
    member inline _.HttpClient(state: HttpStepRequest<_,_>, httpClient) =
        { state with HttpClientFactory = fun () -> httpClient }

    /// Provide a http client factory instead of default one
    [<CustomOperation "clientFactory">]
    member inline _.HttpClientFactory(state: HttpStepRequest<_,_>, httpClientFactory) =
        { state with HttpClientFactory = httpClientFactory }
    member inline _.HttpClientFactory(state: HttpStepCreateRequest<_, _>, httpClientFactory) =
        { state with HttpClientFactory = httpClientFactory }
    member inline _.HttpClientFactory(state: HttpStepRequest<_,_>, httpClientFactory: IHttpClientFactory) =
        { state with HttpClientFactory = httpClientFactory.CreateClient }
    member inline _.HttpClientFactory(state: HttpStepCreateRequest<_, _>, httpClientFactory: IHttpClientFactory) =
        { state with HttpClientFactory = httpClientFactory.CreateClient }

    /// Provide a response check function
    [<CustomOperation "check">]
    member inline __.WithCheck(state: HttpStepRequest<_,_>, check: HttpResponseMessage -> Task<bool>) =
        { state with Checks = check :: state.Checks }
    member inline __.WithCheck(state: HttpStepRequest<_,_>, check: HttpResponseMessage -> bool) =
        let checkTask response = task { return check response }
        { state with Checks = checkTask :: state.Checks }
    member inline _.WithCheck(state: HttpStepCreateRequest<_, _>, check) =
        { state with Checks = check :: state.Checks }
    member inline _.WithCheck(state: HttpStepCreateRequest<_, _>, check) =
        let checkTask response = task { return check response }
        { state with Checks = checkTask :: state.Checks }

    [<CustomOperation "logRequest">]
    member inline _.LogRequest(state : HttpStepRequest<_,_>) =
        { state with WithRequest = logRequest::state.WithRequest }
    member inline _.LogRequest(state : HttpStepCreateRequest<_,_>) =
        { state with WithRequest = logRequest::state.WithRequest }

    [<CustomOperation "logResponse">]
    member inline _.LogResponse(state : HttpStepRequest<_,_>) =
        { state with WithResponse = logResponse::state.WithResponse }
    member inline _.LogResponse(state : HttpStepCreateRequest<_,_>) =
        { state with WithResponse = logResponse::state.WithResponse }

    member _.Run(state: HttpStepRequest<_,_>) =
        let action (ctx: IStepContext<_, _>) =
            task {
                let request = new HttpRequestMessage()
                request.Method <- state.Method
                request.RequestUri <- state.Url
                request.Version <- state.Version
                let content = state.WithContent ctx
                if not (isNull content) then
                    request.Content <- content

                state.Headers
                |> Map.iter (fun name value ->
                    request.Headers.TryAddWithoutValidation(name, value)
                    |> ignore)

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

    member _.Run(state: HttpStepCreateRequest<_, _>) =
        let action (ctx: IStepContext<_, _>) =
            task {
                let! request = state.CreateRequest ctx

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