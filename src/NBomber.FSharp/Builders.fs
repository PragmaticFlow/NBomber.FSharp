[<AutoOpen>]
module NBomber.FSharp.Builder

/// creates a clients pool
let clients = ClientBuilder

/// creates a step builder
let step = StepBuilder

/// creates a scenario builder
let scenario = ScenarioBuilder

/// report configuration
let report = ReportBuilder()

/// creates a performance test builder
let testSuite = RunnerBuilder
