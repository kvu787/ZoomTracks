# Prompt

Find, implement, and compare practical algorithms for the task below.

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
    bool IsColliding(/* rectangle R */);
}
```

## Constructor for ICollisionDetector

must accept O1 and O2.
