# Prompt

Find optimal algorithms for the following task. Do this for `<insert programming language here>`.

For each algorithm:

- Explain how it works.
- Explain what tolerance or tolerances are used and why.
- Identify the optimization objective or objectives.
- Report its preprocessing-time complexity.
- Report its per-query-time complexity.
- Report its storage complexity.
- Explain trade-offs.
- Do proper testing and performance profiling.

The intended usage of the task is to execute it for many different R for the same immutable O1 and O2. Your optimization should take this into account. Preprocessing is allowed.

Generally speaking, the lengths L of edges in all the inputs should be `0.1 <= L <= 1,000`.

# Task

Given two outline loops O1 and O2 in the 2D XY plane and a rectangle R that may be rotated, return True if and only if R intersects O1 or O2

Each outline is an ordered vertex sequence `v[0..n-1]`, where `n >= 3`. Its segments are the closed line segments:

```text
segment[i] = (v[i], v[(i+1) mod n])
```

Each outline is a simple, connected, closed polygonal loop: it does not self-intersect, and its segments meet only where adjacent segments share a vertex. All coordinates are finite IEEE 754 32-bit floating-point values, and every outline segment has strictly positive length.

O1 fully encloses O2. O1 doesn't touch O2 at all.

Each R is a possibly rotated rectangle represented in `float32`. Its valid input and collision geometry are defined as follows:

- May have any rotation in the XY plane.
- Is represented by four finite `float32` XY vertices supplied in cyclic perimeter order, either clockwise or counterclockwise.
- The four segments connecting the vertices in cyclic order form a simple, strictly convex, closed loop, and every segment has strictly positive length.
- Has a perimeter authoritatively defined by the four supplied vertices and their connecting segments. Implementations must test these segments directly without reconstructing or regularizing an ideal mathematical rectangle.
- Exact parallelism, perpendicularity, and equality of opposite-edge lengths must not be assumed because of `float32` rounding.
- Is treated as its perimeter only, consisting of four closed edge segments—not as a filled region.

Return `True` if and only if at least one rectangle edge touches or intersects at least one segment from either outline. This includes proper crossings, endpoint contact, tangential contact, and collinear overlap. Containment without edge contact does not count.

Touching must be handled reasonably with `float32` inputs. No tolerance is prescribed: selecting and justifying a numerically robust tolerance or predicate policy is part of the task.
