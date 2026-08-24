# Canonical rectangle-perimeter collision detectors

This directory contains three exact C# implementations of the API in
[`Prompt.md`](Prompt.md), an independent exact oracle, conformance/fuzz tests,
repeatable benchmarks, and the measured comparison.

The practical default for the stated many-query, immutable-track workload is
`UniformGridCollisionDetector`. Its default grid uses approximately one cell per
outline edge, caps ordinary edge replication at 64 cells, sends larger edges to
an overflow list, and switches broad queries to a single linear pass. Use
`LinearScanCollisionDetector` for very few lifetime queries or highly predictable
early hits. `BvhCollisionDetector` is the grid-resolution-free alternative when
edge sizes or spatial density vary enough to make a uniform grid unattractive.

All three detectors are exact for the binary32 geometry produced by the shared
rectangle transform. They use no geometric tolerance and have no approximate
false positives or false negatives.

## Layout

- `ZoomTracks.CollisionDetection/`: Unity-compatible `netstandard2.1`, C# 9
  implementation and public API.
- `CollisionDetection.Harness/`: independent `BigInteger` oracle, correctness
  suite, workload generator, and benchmark runner.
- [`REPORT.md`](REPORT.md): algorithms, numerical policy, complexity,
  methodology, measured results, trade-offs, and recommendation.
- `TestArtifacts/benchmark-results.csv`: raw results from the reported run
  (generated and intentionally gitignored).
- `TestArtifacts/benchmark-environment.txt`: run timestamps, environment, and
  SHA-256 manifest of every executable source file in that run.

## Reproduce

From this directory with a recent .NET SDK:

```powershell
dotnet build "CollisionDetection.Harness\CollisionDetection.Harness.csproj" -c Release
dotnet run --project "CollisionDetection.Harness\CollisionDetection.Harness.csproj" -c Release --no-build -- --test
$env:DOTNET_TieredCompilation = "0"
$env:DOTNET_TieredPGO = "0"
dotnet run --project "CollisionDetection.Harness\CollisionDetection.Harness.csproj" -c Release --no-build -- --benchmark --output "TestArtifacts"
```

The production project itself targets `netstandard2.1` and uses only C# 9
features supported by ZoomTracks' Unity 6000.3.22f1 configuration. The harness
targets .NET 10 only because it is an out-of-game validation and measurement
tool.
