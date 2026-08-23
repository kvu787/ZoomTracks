"""Reproducible standard-library benchmark for the collision algorithms."""

from __future__ import annotations

import argparse
import gc
from math import isfinite
import platform
import statistics
import sys
from time import perf_counter_ns
from typing import Callable

if __package__:
    from .rectangle_segments import (
        BVHIndex,
        CoherentBlockIndex,
        CoherentHierarchyIndex,
        LinearScanIndex,
        OrientedRectangle,
        PreparedOutlines,
        SpatialChainBVHIndex,
        UniformGridIndex,
    )
    from .workloads import (
        generate_far_queries,
        generate_folded_outlines,
        generate_folded_queries,
        generate_lap_queries,
        generate_near_miss_queries,
        generate_track_outlines,
    )
else:
    from rectangle_segments import (
        BVHIndex,
        CoherentBlockIndex,
        CoherentHierarchyIndex,
        LinearScanIndex,
        OrientedRectangle,
        PreparedOutlines,
        SpatialChainBVHIndex,
        UniformGridIndex,
    )
    from workloads import (
        generate_far_queries,
        generate_folded_outlines,
        generate_folded_queries,
        generate_lap_queries,
        generate_near_miss_queries,
        generate_track_outlines,
    )


Factory = Callable[[PreparedOutlines], object]


def _parse_sizes(value: str) -> list[int]:
    try:
        sizes = [int(item.strip()) for item in value.split(",") if item.strip()]
    except ValueError as error:
        raise argparse.ArgumentTypeError("sizes must be comma-separated integers") from error
    if not sizes or any(size < 8 or size % 2 for size in sizes):
        raise argparse.ArgumentTypeError("every size must be an even integer of at least 8")
    return sizes


def _time_build(factory: Factory, prepared: PreparedOutlines, repeats: int) -> int:
    samples: list[int] = []
    for _ in range(repeats):
        start = perf_counter_ns()
        detector = factory(prepared)
        elapsed = perf_counter_ns() - start
        samples.append(elapsed)
        del detector
    return int(statistics.median(samples))


def _time_queries(
    detector: object,
    queries: list[OrientedRectangle],
    repeats: int,
    expected_hits: int,
) -> tuple[float, float]:
    intersects = detector.intersects  # type: ignore[attr-defined]
    for _ in range(2):
        if sum(intersects(query) for query in queries) != expected_hits:
            raise RuntimeError("detector returned unstable results during warm-up")

    samples: list[float] = []
    gc_was_enabled = gc.isenabled()
    gc.disable()
    try:
        for _ in range(repeats):
            start = perf_counter_ns()
            hits = sum(intersects(query) for query in queries)
            elapsed = perf_counter_ns() - start
            if hits != expected_hits:
                raise RuntimeError("detector returned unstable results")
            samples.append(elapsed / len(queries))
    finally:
        if gc_was_enabled:
            gc.enable()

    return statistics.median(samples), max(samples)


def _format_time(nanoseconds: float) -> str:
    if nanoseconds < 1_000.0:
        return f"{nanoseconds:.0f} ns"
    if nanoseconds < 1_000_000.0:
        return f"{nanoseconds / 1_000.0:.2f} us"
    return f"{nanoseconds / 1_000_000.0:.2f} ms"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--vertices",
        type=_parse_sizes,
        default=_parse_sizes("256,1024,4096"),
        help="comma-separated total loop-vertex counts (default: 256,1024,4096)",
    )
    parser.add_argument("--queries", type=int, default=2_000)
    parser.add_argument("--repeats", type=int, default=7)
    parser.add_argument("--seed", type=int, default=17)
    parser.add_argument(
        "--workload",
        choices=("lap", "near", "far", "both", "all", "folded"),
        default="all",
    )
    parser.add_argument("--folds", type=int, default=20)
    parser.add_argument("--block-size", type=int, default=16)
    parser.add_argument("--hierarchy-leaf-size", type=int, default=8)
    parser.add_argument("--hierarchy-branching-factor", type=int, default=4)
    parser.add_argument("--chain-size", type=int, default=8)
    parser.add_argument(
        "--cell-size",
        type=float,
        action="append",
        help="grid cell size; repeat to compare values (default: automatic)",
    )
    parser.add_argument("--max-query-cells", type=int, default=256)
    parser.add_argument("--max-cells-per-segment", type=int, default=4_096)
    parser.add_argument("--bvh-leaf-size", type=int, default=8)
    parser.add_argument("--bvh-bin-count", type=int, default=12)
    parser.add_argument("--bvh-expected-query-width", type=float, default=5.0)
    parser.add_argument("--bvh-expected-query-height", type=float, default=5.0)
    arguments = parser.parse_args()
    if arguments.queries < 1 or arguments.repeats < 1 or arguments.folds < 1:
        parser.error("queries, repeats, and folds must be positive")
    if min(
        arguments.block_size,
        arguments.hierarchy_leaf_size,
        arguments.hierarchy_branching_factor,
        arguments.chain_size,
        arguments.bvh_leaf_size,
        arguments.max_query_cells,
        arguments.max_cells_per_segment,
    ) < 1:
        parser.error("index tuning values must be positive")
    if arguments.bvh_bin_count < 2:
        parser.error("bvh-bin-count must be at least two")
    if arguments.hierarchy_branching_factor < 2:
        parser.error("hierarchy-branching-factor must be at least two")
    if arguments.cell_size and any(
        not isfinite(value) or value <= 0.0 for value in arguments.cell_size
    ):
        parser.error("cell-size values must be finite and positive")
    if (
        not isfinite(arguments.bvh_expected_query_width)
        or not isfinite(arguments.bvh_expected_query_height)
        or arguments.bvh_expected_query_width <= 0.0
        or arguments.bvh_expected_query_height <= 0.0
    ):
        parser.error("BVH expected query dimensions must be finite and positive")

    factories: list[tuple[str, Factory]] = [
        ("Linear scan", LinearScanIndex),
        (
            "Coherent blocks",
            lambda prepared: CoherentBlockIndex(
                prepared, block_size=arguments.block_size
            ),
        ),
        (
            "Ordered hierarchy",
            lambda prepared: CoherentHierarchyIndex(
                prepared,
                leaf_size=arguments.hierarchy_leaf_size,
                branching_factor=arguments.hierarchy_branching_factor,
            ),
        ),
        (
            "Spatial chain BVH",
            lambda prepared: SpatialChainBVHIndex(
                prepared, chain_size=arguments.chain_size
            ),
        ),
    ]
    cell_sizes = arguments.cell_size or [None]
    for cell_size in cell_sizes:
        label = "Uniform grid(auto)" if cell_size is None else f"Uniform grid({cell_size:g})"
        factories.append(
            (
                label,
                lambda prepared, size=cell_size: UniformGridIndex(
                    prepared,
                    cell_size=size,
                    max_query_cells=arguments.max_query_cells,
                    max_cells_per_segment=arguments.max_cells_per_segment,
                ),
            )
        )
    factories.append(
        (
            "SAH BVH",
            lambda prepared: BVHIndex(
                prepared,
                leaf_size=arguments.bvh_leaf_size,
                bin_count=arguments.bvh_bin_count,
                expected_query_width=arguments.bvh_expected_query_width,
                expected_query_height=arguments.bvh_expected_query_height,
            ),
        )
    )
    print(f"Python {platform.python_version()} | {platform.platform()}")
    print(
        f"queries={arguments.queries:,}, repeats={arguments.repeats}, "
        f"seed={arguments.seed}"
    )

    for vertex_count in arguments.vertices:
        if arguments.workload == "folded":
            outer, inner = generate_folded_outlines(vertex_count, arguments.folds)
        else:
            outer, inner = generate_track_outlines(vertex_count, arguments.seed)
        preparation_start = perf_counter_ns()
        prepared = PreparedOutlines(outer, inner)
        preparation_ns = perf_counter_ns() - preparation_start
        workloads: list[tuple[str, list[OrientedRectangle]]] = []
        if arguments.workload in ("lap", "both", "all"):
            workloads.append(
                ("lap", generate_lap_queries(arguments.queries, arguments.seed))
            )
        if arguments.workload in ("near", "all"):
            workloads.append(
                (
                    "near",
                    generate_near_miss_queries(arguments.queries, arguments.seed),
                )
            )
        if arguments.workload in ("far", "both", "all"):
            workloads.append(
                ("far", generate_far_queries(arguments.queries, arguments.seed))
            )
        if arguments.workload == "folded":
            workloads.append(
                ("folded", generate_folded_queries(arguments.queries, arguments.folds))
            )

        print()
        print(
            f"{vertex_count:,} vertices / {prepared.edge_count:,} derived edges | "
            f"common preparation "
            f"{_format_time(preparation_ns)}"
        )
        print(
            f"{'algorithm':<22} {'workload':<8} {'build':>10} "
            f"{'median/query':>14} {'slowest mean':>13} {'queries/s':>12} {'hits':>8}"
        )
        print("-" * 96)

        expected_by_workload: dict[str, tuple[bool, ...]] = {}
        for name, factory in factories:
            build_ns = _time_build(factory, prepared, min(arguments.repeats, 5))
            detector = factory(prepared)
            for workload_name, queries in workloads:
                outcomes = tuple(detector.intersects(query) for query in queries)
                expected = expected_by_workload.setdefault(workload_name, outcomes)
                if outcomes != expected:
                    mismatch = next(
                        index
                        for index, (actual, wanted) in enumerate(zip(outcomes, expected))
                        if actual != wanted
                    )
                    raise RuntimeError(
                        f"{name} disagreed on {workload_name} query {mismatch}: "
                        f"{outcomes[mismatch]} != {expected[mismatch]}"
                    )
                hits = sum(outcomes)
                median_ns, slowest_ns = _time_queries(
                    detector, queries, arguments.repeats, hits
                )
                queries_per_second = 1.0e9 / median_ns
                print(
                    f"{name:<22} {workload_name:<8} {_format_time(build_ns):>10} "
                    f"{_format_time(median_ns):>14} {_format_time(slowest_ns):>13} "
                    f"{queries_per_second:>12,.0f} {hits:>8,}"
                )

    return 0


if __name__ == "__main__":
    sys.exit(main())
