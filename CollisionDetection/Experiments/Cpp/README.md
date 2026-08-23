# Exact outline/query-perimeter collision in C++

This folder contains four exact implementations of the requested predicate:

- `LinearAlwaysExact`: simple scan and fixed-width integer orientations; the
  brute-force exact oracle/baseline.
- `LinearAdaptiveExact`: simple scan with a certified binary64 filter and exact
  integer fallback.
- `BvhAdaptiveExact`: a flattened median AABB BVH; the recommended default.
- `UniformGridAdaptiveExact`: a capped uniform-grid broad phase optimized for
  spatially uniform edges and small local queries.

All variants test the four supplied query edges directly. They do not
reconstruct a rectangle, treat it as filled, or use a geometric tolerance.
See [REPORT.md](REPORT.md) for the numerical policy, complexity analysis,
correctness coverage, measured results, trade-offs, and recommendation.

## Build and run on Windows

From this directory:

```powershell
.\build.ps1 -Configuration Release
.\build\collision_tests.exe
.\build\collision_benchmark.exe .\results\benchmark.csv
```

`build.ps1` locates the newest Visual Studio x64 C++ toolchain with
`vswhere.exe`. A conventional `CMakeLists.txt` is also provided.

## API sketch

```cpp
#include "collision/algorithms.hpp"

collision::Outline outer = /* cyclic float32 vertices */;
collision::Outline inner = /* cyclic float32 vertices */;
collision::QueryPerimeter query = /* four cyclic float32 vertices */;

auto index = collision::make_bvh_index(outer, inner);
const bool touches_an_outline = index->intersects(query);
```

Factories copy the outline edges into an immutable index. They assume the input
contract in the task (finite coordinates, valid simple loops, nesting, and a
valid strictly convex query); they do not spend time revalidating it.

Queries on one immutable index may run concurrently. The grid uses per-thread
scratch. If counters are wanted, pass a different `QueryStats` object to each
thread; sharing a mutable counter object would be a data race.

## Layout

- `include/collision`: public geometry, predicate, and index APIs.
- `src`: the exact predicate and three broad-phase implementations.
- `tests/test_collision.cpp`: deterministic correctness and concurrency suite.
- `bench/benchmark.cpp`: deterministic benchmark and CSV writer.
- `results/benchmark.csv`: raw measured rows used in the report.
