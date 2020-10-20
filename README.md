# NBomber.FSharp

NBomber Extensions for F#

- NBomber.FSharp - set of computation expressions for test runners, scenarios and steps
- NBomber.FSharp.Http - the same but for NBomber.Http
- NBomber.FSharp.Hopac - adds Hopac supports for the above

Checkout [Demo.fs](test/NBomber.FSharp.Test/Demo.fs)

### Disclaimer

The API is currently a subject for massive changes. Therefore no NuGets published and no recommendation from me to put it into heavy usage.
But feedback is pretty appreciated. You can still try it out, referencing the files with `paket` right from the github:

In `paket.dependencies` in your solution root:

```
github PragmaticFlow/NBomber.FSharp:dev src/NBomber.FSharp/Builder/RunnerBuilder.fs
github PragmaticFlow/NBomber.FSharp:dev src/NBomber.FSharp/Builder/ScenarioBuilder.fs
github PragmaticFlow/NBomber.FSharp:dev src/NBomber.FSharp/Builder/StepBuilder.fs
github PragmaticFlow/NBomber.FSharp:dev src/NBomber.FSharp/Builders.fs
github PragmaticFlow/NBomber.FSharp:dev src/NBomber.FSharp.Hopac/Hopac.fs
github PragmaticFlow/NBomber.FSharp:dev src/NBomber.FSharp.Http/HttpStepBuilder.fs
```

In `paket.references` in your test project:

```
File: StepBuilder.fs
File: ScenarioBuilder.fs
File: RunnerBuilder.fs
File: Builders.fs
File: HttpStepBuilder.fs
File: Hopac.fs
```
