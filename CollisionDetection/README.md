# Prompt

Find, implement, and compare practical algorithms for the task below. Use `<insert programming language here>`.

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

The intended workload evaluates many different `R` queries against the same immutable `O1` and `O2`. Preprocessing the outlines once is allowed and should be considered when comparing approaches. Express complexity in terms of the outline sizes, using `N = n1 + n2`, and any other relevant parameters.

Typical input-edge lengths `L`, in application units, satisfy `0.1 <= L <= 1,000`. Treat this as workload guidance rather than a validity requirement.

# Task

Given two outline loops `O1` and `O2` in the 2D XY plane and a possibly rotated rectangle-derived query perimeter `R`, return `True` if and only if an edge of `R` intersects an edge of `O1` or `O2`.

## Outlines

`O1` and `O2` may have different vertex counts, denoted `n1` and `n2`. For either outline, let `v[0..n-1]` be its ordered vertex sequence, where `n >= 3`. Its segments are the closed line segments:

```text
segment[i] = (v[i], v[(i+1) mod n])
```

Each outline is a simple, connected, closed polygonal loop: it does not self-intersect, and its segments meet only where adjacent segments share a vertex. All vertex coordinates are finite IEEE 754 binary32 (`float32`) values, and every outline segment has strictly positive length.

`O2` lies entirely within the bounded interior of `O1`, and the two loops do not touch or intersect.

## Query perimeter

Each `R` originates from a rectangle that may have any rotation in the XY plane. Its valid input and collision geometry are defined as follows:

- It is represented by four finite `float32` XY vertices supplied in cyclic perimeter order, either clockwise or counterclockwise.
- The four segments connecting the vertices in cyclic order form a simple, strictly convex, closed loop, and every segment has strictly positive length.
- The supplied vertices and their connecting segments authoritatively define the collision geometry. Test those segments directly; do not reconstruct or regularize an ideal mathematical rectangle.
- Do not assume exact parallelism, perpendicularity, or equality of opposite-edge lengths, because the vertices have been rounded to `float32`.
- Treat `R` as its perimeter only, consisting of four closed edge segments, not as a filled region.

## Intersection and numerical semantics

Interpret every input `float32` coordinate as the exact real value represented by that binary32 value. The target result is exact intersection of the resulting closed line segments. Implementations may use higher-precision or exact intermediate arithmetic.

Return `True` for proper crossings, endpoint contact, tangential contact, and collinear overlap. Containment without edge contact does not count.

Numerical robustness is part of the task. Exact solutions must preserve the mathematical predicate above. Tolerance-based solutions may also be explored as practical alternatives, but they must be identified as approximate and must define and justify their tolerance policy and error behavior.
