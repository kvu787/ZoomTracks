# Task prompt

Given two immutable outline loops in the 2D XY plane, preprocess them so that many query rectangles can be tested efficiently for edge intersection.
Each outline is an ordered vertex sequence v[0..n-1], where n >= 3. Its segments are the closed line segments
segment[i] = (v[i], v[(i+1) mod n]).
Each outline is a simple, connected, closed polygonal loop: it does not self-intersect, and its segments meet only where adjacent segments share a vertex. All coordinates are finite IEEE 754 32-bit floating-point values, and every outline segment has strictly positive length.
Each query rectangle:
- May have any rotation in the XY plane.
- Has strictly positive width and height.
- Is represented by four finite float32 XY vertices supplied in cyclic perimeter order, either clockwise or counterclockwise.
- Is treated as its perimeter only, consisting of four closed edge segments—not as a filled region.
Return True if and only if at least one rectangle edge touches or intersects at least one segment from either outline. This includes proper crossings, endpoint contact, tangential contact, and collinear overlap. Containment without edge contact does not count.
Touching must be handled reasonably with float32 inputs. No tolerance is prescribed: selecting and justifying a numerically robust tolerance or predicate policy is part of the task.
Explore optimal solutions for this many-query problem. Define relevant notions of optimality and compare approaches that prioritize different objectives, including:
- Preprocessing time
- Per-query runtime
- Worst-case and expected performance
- Memory usage
- Implementation complexity
- Numerical robustness
For each worthwhile approach, describe its assumptions, algorithm, complexity, failure modes, and preferred workloads. Identify the trade-offs rather than assuming that one solution is optimal under every objective.
