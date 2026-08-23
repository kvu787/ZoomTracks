# Prompt

If there any issues with this prompt, address them in a reasonable way.

Find, implement, and compare optimal practical algorithms for the task below.

Constraints:

- Use C#.
- Put everything in `C:\Users\k\Repository\Unity\ZoomTracks\CollisionDetection\Canonical`.
- Don't read anything in the repo outside of that folder.
  - Exception 1: You must read the ZoomTracks project info to ensure that you use C# that will work in the ZoomTracks Unity game.
  - Exception 2: You may read the stuff in `C:\Users\k\Repository\Unity\ZoomTracks\CollisionDetection\Experiments`. However, don't use it as a "quick shortcut" to answer the prompt.
- Do not commit anything.

There may not be one algorithm that is optimal for every workload. Include worthwhile approaches with different optimization objectives and recommend a practical default.

For each algorithm:

- Explain how it works and what assumptions it makes.
- Explain its numerical-robustness policy, including intermediate precision and any tolerances. An exact algorithm may use no geometric tolerance. Clearly label any approximate algorithm and describe when it can produce false positives or false negatives.
- Identify the objective or objectives it optimizes.
- Report its preprocessing-time complexity.
- Report its per-query-time complexity.
- Report its storage complexity.
- Explain its trade-offs and preferred workloads.
- Test it for correctness and performance. Include relevant edge cases, describe the benchmark methodology and environment, and report measured results rather than estimated timings.

The intended workload evaluates many different `R` queries against the same immutable `O1` and `O2`. Preprocessing data that does not vary between queries is allowed and should be considered when comparing approaches. Express complexity in terms of the outline sizes, using `N = n1 + n2`, and any other relevant parameters.

Typical input-edge lengths `L`, in application units, satisfy `0.1 <= L <= 1,000`. Treat this as workload guidance rather than a validity requirement.

# Task

All geometry is 2D. Given two outline loops `O1` and `O2`, and a rectangle `R`, return `True` if and only if an edge of `R` intersects an edge of `O1` or `O2`.

## Outlines

`O1` and `O2` may have different vertex counts, denoted `n1` and `n2`. For either outline, let `v[0..n-1]` be its ordered vertex sequence, where `n >= 3`. Its segments are the closed line segments:

```text
segment[i] = (v[i], v[(i+1) mod n])
```

Each outline is a simple, connected, closed polygonal loop: it does not self-intersect, and its segments meet only where adjacent segments share a vertex. All vertex coordinates are finite IEEE 754 binary32 (`float32`) values, and every outline segment has strictly positive length.

`O2` lies entirely within the bounded interior of `O1`, and the two loops do not touch or intersect.

## Rectangle representation

`R` is represented by immutable local-space bounds and a per-query pose.

### Local-space bounds

Represent the immutable local-space bounds of `R` with four `float32` values:

```text
(min_x, min_y, max_x, max_y)
```

All four values are finite, with `min_x < max_x` and `min_y < max_y`. Do not assume the bounds are centered at `(0, 0)`. The representation has no scale component; these bounds define the final local-space size.

The local corners, in cyclic perimeter order, are:

```text
(min_x, min_y)
(max_x, min_y)
(max_x, max_y)
(min_x, max_y)
```

### Per-query pose

Each query supplies three finite `float32` values:

```text
(position_x, position_y, rotation_degrees)
```

Positive rotation is clockwise. For each local corner `(x, y)`, convert `rotation_degrees` to radians, let `c` and `s` be its cosine and sine, and compute:

```text
world_x = position_x + x*c + y*s
world_y = position_y - x*s + y*c
```

Implementations may choose how to evaluate the angle conversion, sine, cosine, and coordinate expressions, including their intermediate precision. Explain the chosen numerical policy. Convert each resulting world-space coordinate to a finite `float32` value; those values define the geometry used for intersection testing. Different implementations are not required to produce bit-identical vertices from the same pose.

The resulting vertices and their cyclic connecting segments define `R`. Because of binary32 rounding, do not assume exact parallelism, perpendicularity, or equality of opposite-edge lengths in world space. Implementations do not have to materialize the four vertices, but must return the same result as testing these segments. The per-query benchmark must include the work needed to place `R` from its local bounds and pose.

Treat `R` as its perimeter only, consisting of four closed edge segments, not as a filled region.

## Intersection and numerical semantics

Interpret every `float32` coordinate as the exact real value represented by that binary32 value. After constructing `R`, the target result is exact intersection of the resulting closed line segments. Thus, "exact" refers to the generated binary32 geometry, not to an ideal real-number rotation. Implementations may use higher-precision or exact intermediate arithmetic after constructing equivalent geometry.

Return `True` for proper crossings, endpoint contact, tangential contact, and collinear overlap. Containment without edge contact does not count.

Numerical robustness is part of the task. Exact solutions must preserve the mathematical predicate above. Tolerance-based solutions may also be explored as practical alternatives, but they must be identified as approximate and must define and justify their tolerance policy and error behavior.

# API Contract

All public types are in the `ZoomTracks.CollisionDetection` namespace.

## `CoordinateXY`

```csharp
public readonly struct CoordinateXY
{
    public CoordinateXY(float x, float y);
    public float X { get; }
    public float Y { get; }
}
```

Represents a two-dimensional point. Both coordinates must be finite; the constructor
throws `ArgumentOutOfRangeException` for `NaN` or positive/negative infinity.

## `ICollisionDetector`

```csharp
public interface ICollisionDetector
{
    bool IsColliding(RectangleLocalBounds localBounds, RectanglePose pose);
}
```

`IsColliding` constructs the four world-space rectangle vertices from `localBounds`
and `pose` according to the rectangle-representation rules above, then returns `true`
if and only if one of the four resulting closed segments intersects an edge of either
outline. The method must not treat the rectangle as filled. In particular, it returns
`false` for containment without edge contact.

Exact algorithms that implement this interface must satisfy the exact intersection
semantics above. An approximate algorithm with possible false positives or false
negatives does not satisfy this interface's contract; expose such an algorithm with a
separately and clearly named public API.

## `RectangleLocalBounds`

```csharp
public readonly struct RectangleLocalBounds
{
    public RectangleLocalBounds(float minX, float minY, float maxX, float maxY);
    public float MinX { get; }
    public float MinY { get; }
    public float MaxX { get; }
    public float MaxY { get; }
}
```

Represents the immutable local-space bounds of a rectangle. All four arguments must be
finite, `minX < maxX`, and `minY < maxY`. The constructor throws
`ArgumentOutOfRangeException` when an argument is not finite and `ArgumentException`
when either ordered pair does not define a positive extent. Because C# permits a
`readonly struct` to be created without running its constructor,
`default(RectangleLocalBounds)` is invalid; `IsColliding` throws `ArgumentException`
when given invalid bounds.

## `RectanglePose`

```csharp
public readonly struct RectanglePose
{
    public RectanglePose(float positionX, float positionY, float rotationDegrees);
    public float PositionX { get; }
    public float PositionY { get; }
    public float RotationDegrees { get; }
}
```

Represents a rectangle pose for one query. All three arguments must be finite; the
constructor throws `ArgumentOutOfRangeException` for `NaN` or positive/negative
infinity. `default(RectanglePose)` is the valid pose `(0, 0, 0)`.

## Collision-detector construction

C# interfaces do not declare constructors. Every public concrete implementation of
`ICollisionDetector` must provide at least this constructor shape, where `DetectorName`
is the implementation type:

```csharp
public DetectorName(
    List<CoordinateXY> outline1,
    List<CoordinateXY> outline2);
```

The lists contain the vertices in perimeter order and must not repeat the first vertex
at the end; the closing segment is implicit. Each list must contain at least three
vertices, and every pair of consecutive vertices, including the last and first, must
be distinct. A null list throws `ArgumentNullException`; too few vertices or a
zero-length segment throws `ArgumentException`. The remaining outline properties in
the task description are caller preconditions and do not have to be revalidated by the
constructor.

Ownership of both lists transfers to the detector when the constructor completes
successfully. The detector must retain and use the supplied lists without copying their
vertices. It may mutate or reorder the lists as part of preprocessing. After ownership
transfers, the caller must not read, mutate, or otherwise reuse either list. If the
constructor throws, ownership does not transfer. Implementations may also provide
overloads with algorithm-specific settings, but the two-list form above must select
documented practical defaults.

The measured per-query time must use `IsColliding` and include construction of the
world-space rectangle geometry from the supplied bounds and pose. Creating the two
small immutable argument values before the timed region is allowed; precomputing or
supplying their transformed world-space corners is not.
