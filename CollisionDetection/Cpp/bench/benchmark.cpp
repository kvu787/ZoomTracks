#include "collision/algorithms.hpp"

#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <fstream>
#include <functional>
#include <iomanip>
#include <iostream>
#include <limits>
#include <memory>
#include <numbers>
#include <random>
#include <sstream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <thread>
#include <utility>
#include <vector>

#if defined(_MSC_VER)
#include <intrin.h>
#endif

namespace {

using Clock = std::chrono::steady_clock;
using collision::CollisionIndex;
using collision::Outline;
using collision::Point;
using collision::QueryPerimeter;

volatile std::uint64_t benchmark_sink = 0;

struct Workload {
    std::string name;
    std::vector<QueryPerimeter> queries;
    std::vector<unsigned char> expected;
    std::size_t expected_hits = 0;
};

struct Result {
    std::string algorithm;
    std::string geometry;
    std::size_t total_edges = 0;
    std::size_t first_edges = 0;
    std::size_t second_edges = 0;
    std::string workload;
    std::size_t queries = 0;
    std::size_t hits = 0;
    double preprocessing_us = 0;
    double query_ns = 0;
    collision::IndexMetrics metrics{};
    collision::QueryStats stats{};
};

std::string compiler_name() {
#if defined(_MSC_VER)
    return "MSVC " + std::to_string(_MSC_FULL_VER);
#elif defined(__clang__)
    return "Clang " __clang_version__;
#elif defined(__GNUC__)
    return "GCC " __VERSION__;
#else
    return "unknown compiler";
#endif
}

std::string cpu_brand() {
#if defined(_MSC_VER) && (defined(_M_X64) || defined(_M_IX86))
    std::array<int, 4> registers{};
    std::array<char, 49> brand{};
    __cpuid(registers.data(), 0x80000000);
    if (static_cast<unsigned int>(registers[0]) < 0x80000004U) {
        return "x86 CPU (brand unavailable)";
    }
    for (int leaf = 0; leaf < 3; ++leaf) {
        __cpuid(registers.data(), 0x80000002 + leaf);
        std::memcpy(brand.data() + leaf * 16, registers.data(), 16);
    }
    return std::string(brand.data());
#else
    return "CPU brand unavailable";
#endif
}

Outline regular_outline(std::size_t count, double radius, double phase) {
    Outline outline;
    outline.reserve(count);
    for (std::size_t index = 0; index < count; ++index) {
        const double angle = phase + 2.0 * std::numbers::pi *
                                         static_cast<double>(index) /
                                         static_cast<double>(count);
        outline.push_back({static_cast<float>(radius * std::cos(angle)),
                           static_cast<float>(radius * std::sin(angle))});
    }
    return outline;
}

Outline clustered_outline(std::size_t count, double radius, double phase) {
    // Eighty percent of the edges occupy a 0.2-radian arc. At N=2048 the
    // resulting edge lengths still stay within the prompt's typical range,
    // while several hundred AABBs share a small number of grid cells.
    constexpr double dense_fraction = 0.8;
    constexpr double dense_arc = 0.2;
    Outline outline;
    outline.reserve(count);
    for (std::size_t index = 0; index < count; ++index) {
        const double fraction = static_cast<double>(index) /
                                static_cast<double>(count);
        const double angle = fraction < dense_fraction
            ? dense_arc * (fraction / dense_fraction)
            : dense_arc + (2.0 * std::numbers::pi - dense_arc) *
                              ((fraction - dense_fraction) /
                               (1.0 - dense_fraction));
        outline.push_back({static_cast<float>(radius * std::cos(angle + phase)),
                           static_cast<float>(radius * std::sin(angle + phase))});
    }
    return outline;
}

QueryPerimeter rectangle(double center_x, double center_y,
                         double half_x, double half_y, double angle) {
    const double ux = std::cos(angle);
    const double uy = std::sin(angle);
    const double vx = -uy;
    const double vy = ux;
    constexpr std::array<std::array<double, 2>, 4> signs{{
        {{-1.0, -1.0}}, {{1.0, -1.0}},
        {{1.0, 1.0}}, {{-1.0, 1.0}},
    }};
    QueryPerimeter query{};
    for (std::size_t index = 0; index < 4; ++index) {
        query.vertices[index] = {
            static_cast<float>(center_x + signs[index][0] * half_x * ux +
                               signs[index][1] * half_y * vx),
            static_cast<float>(center_y + signs[index][0] * half_x * uy +
                               signs[index][1] * half_y * vy),
        };
    }
    return query;
}

std::vector<Workload> make_workloads(const Outline& outer,
                                     std::size_t query_count) {
    std::mt19937_64 random(0xC0111510ULL + outer.size());
    std::uniform_real_distribution<double> unit(0.0, 1.0);
    std::uniform_real_distribution<double> angle(0.0, 2.0 * std::numbers::pi);

    std::vector<Workload> workloads;
    workloads.push_back({"far_miss"});
    workloads.push_back({"annulus_local_miss"});
    workloads.push_back({"mixed_local"});
    workloads.push_back({"large_rotated_containment_miss"});
    workloads.push_back({"vertex_touch"});
    for (Workload& workload : workloads) {
        workload.queries.reserve(query_count);
    }

    for (std::size_t index = 0; index < query_count; ++index) {
        const double far_angle = angle(random);
        const double far_radius = 1800.0 + 900.0 * unit(random);
        workloads[0].queries.push_back(rectangle(
            far_radius * std::cos(far_angle),
            far_radius * std::sin(far_angle),
            0.1 + 20.0 * unit(random), 0.1 + 20.0 * unit(random), angle(random)));

        const double annulus_angle = angle(random);
        const double annulus_radius = 520.0 + 160.0 * unit(random);
        workloads[1].queries.push_back(rectangle(
            annulus_radius * std::cos(annulus_angle),
            annulus_radius * std::sin(annulus_angle),
            0.1 + 4.0 * unit(random), 0.1 + 4.0 * unit(random), angle(random)));

        workloads[2].queries.push_back(rectangle(
            -1150.0 + 2300.0 * unit(random),
            -1150.0 + 2300.0 * unit(random),
            0.1 + 100.0 * unit(random), 0.1 + 100.0 * unit(random), angle(random)));

        workloads[3].queries.push_back(rectangle(
            0.0, 0.0, 1200.0 + 100.0 * unit(random),
            1200.0 + 100.0 * unit(random), angle(random)));

        // Spread contact points around the whole loop so early-exit scans are
        // not accidentally benchmarked only against the first few edges when
        // the outline is much larger than the query set.
        const std::size_t contact_index =
            (index * outer.size()) / query_count;
        const Point contact = outer[contact_index];
        const double length = std::hypot(static_cast<double>(contact.x),
                                         static_cast<double>(contact.y));
        const double outward_x = static_cast<double>(contact.x) / length;
        const double outward_y = static_cast<double>(contact.y) / length;
        const double tangent_x = -outward_y;
        const double tangent_y = outward_x;
        const double outward_extent = 0.1 + 10.0 * unit(random);
        const double tangent_extent = 0.1 + 10.0 * unit(random);
        QueryPerimeter touch{};
        touch.vertices[0] = contact;
        touch.vertices[1] = {
            static_cast<float>(contact.x + outward_extent * outward_x),
            static_cast<float>(contact.y + outward_extent * outward_y),
        };
        touch.vertices[2] = {
            static_cast<float>(contact.x + outward_extent * outward_x +
                               tangent_extent * tangent_x),
            static_cast<float>(contact.y + outward_extent * outward_y +
                               tangent_extent * tangent_y),
        };
        touch.vertices[3] = {
            static_cast<float>(contact.x + tangent_extent * tangent_x),
            static_cast<float>(contact.y + tangent_extent * tangent_y),
        };
        workloads[4].queries.push_back(touch);
    }
    return workloads;
}

Workload make_clustered_cell_workload(std::size_t query_count) {
    Workload workload{"clustered_cell_miss"};
    workload.queries.reserve(query_count);
    std::mt19937_64 random(0xC1A57E2ULL);
    std::uniform_real_distribution<double> unit(0.0, 1.0);
    for (std::size_t index = 0; index < query_count; ++index) {
        const double angle = 0.01 + 0.18 * unit(random);
        const double radius = 988.0 + 3.0 * unit(random);
        workload.queries.push_back(rectangle(
            radius * std::cos(angle), radius * std::sin(angle),
            0.25 + 0.75 * unit(random), 0.25 + 0.75 * unit(random),
            2.0 * std::numbers::pi * unit(random)));
    }
    return workload;
}

void establish_expected(Workload& workload, const CollisionIndex& oracle) {
    workload.expected.resize(workload.queries.size());
    workload.expected_hits = 0;
    for (std::size_t index = 0; index < workload.queries.size(); ++index) {
        const bool hit = oracle.intersects(workload.queries[index]);
        workload.expected[index] = static_cast<unsigned char>(hit);
        workload.expected_hits += static_cast<std::size_t>(hit);
    }
}

std::uint64_t execute_queries(const CollisionIndex& index,
                              const Workload& workload,
                              std::size_t repetitions) {
    std::uint64_t checksum = 0;
    for (std::size_t repetition = 0; repetition < repetitions; ++repetition) {
        for (std::size_t query = 0; query < workload.queries.size(); ++query) {
            const bool hit = index.intersects(workload.queries[query]);
            checksum += static_cast<std::uint64_t>(hit);
        }
    }
    benchmark_sink = checksum;
    return checksum;
}

void validate_queries(const CollisionIndex& index, const Workload& workload) {
    for (std::size_t query = 0; query < workload.queries.size(); ++query) {
        const bool hit = index.intersects(workload.queries[query]);
        if (hit != (workload.expected[query] != 0)) {
            throw std::runtime_error(std::string(index.name()) +
                                     " disagreed with the exact oracle in " +
                                     workload.name);
        }
    }
}

double median(std::vector<double> values) {
    std::sort(values.begin(), values.end());
    return values[values.size() / 2];
}

template <typename Factory>
std::pair<double, std::unique_ptr<CollisionIndex>> measure_preprocessing(
    Factory&& factory, int samples) {
    std::vector<double> durations;
    durations.reserve(static_cast<std::size_t>(samples));
    std::unique_ptr<CollisionIndex> retained;
    for (int sample = 0; sample < samples; ++sample) {
        const auto start = Clock::now();
        auto candidate = factory();
        const auto stop = Clock::now();
        durations.push_back(std::chrono::duration<double, std::micro>(stop - start).count());
        retained = std::move(candidate);
    }
    return {median(std::move(durations)), std::move(retained)};
}

double measure_queries(const CollisionIndex& index, const Workload& workload) {
    // Correctness is checked immediately before timing, outside the measured
    // loop so a vector lookup and comparison do not dominate very fast misses.
    validate_queries(index, workload);
    const std::size_t warm_count = std::min<std::size_t>(256, workload.queries.size());
    for (std::size_t query = 0; query < warm_count; ++query) {
        benchmark_sink = benchmark_sink ^
                         static_cast<std::uint64_t>(index.intersects(workload.queries[query]));
    }

    std::size_t repetitions = 1;
    for (;;) {
        const auto start = Clock::now();
        execute_queries(index, workload, repetitions);
        const double seconds = std::chrono::duration<double>(Clock::now() - start).count();
        if (seconds >= 0.08 || repetitions >= 1024) {
            break;
        }
        repetitions *= 2;
    }

    std::vector<double> samples;
    samples.reserve(3);
    for (int sample = 0; sample < 3; ++sample) {
        const auto start = Clock::now();
        execute_queries(index, workload, repetitions);
        const auto stop = Clock::now();
        const double nanoseconds =
            std::chrono::duration<double, std::nano>(stop - start).count();
        samples.push_back(nanoseconds /
                          static_cast<double>(repetitions * workload.queries.size()));
    }
    return median(std::move(samples));
}

collision::QueryStats collect_stats(const CollisionIndex& index,
                                    const Workload& workload) {
    collision::QueryStats total{};
    for (const QueryPerimeter& query : workload.queries) {
        benchmark_sink = benchmark_sink ^
                         static_cast<std::uint64_t>(index.intersects(query, &total));
    }
    return total;
}

std::string csv_escape(std::string_view text) {
    std::string result = "\"";
    for (char character : text) {
        if (character == '"') {
            result += '"';
        }
        result += character;
    }
    result += '"';
    return result;
}

void write_csv(std::ostream& output, const std::vector<Result>& results) {
    output << "algorithm,geometry,N,n1,n2,workload,queries,hits,preprocessing_us,"
              "query_ns,storage_bytes,auxiliary_nodes,stored_edge_references,"
              "overflow_edges,broad_phase_tests,candidate_segment_tests,"
              "orientation_tests,exact_orientation_fallbacks,nodes_visited,"
              "cells_visited,duplicate_references\n";
    output << std::setprecision(12);
    for (const Result& result : results) {
        output << csv_escape(result.algorithm) << ',' << csv_escape(result.geometry)
               << ',' << result.total_edges << ','
               << result.first_edges << ',' << result.second_edges << ','
               << csv_escape(result.workload) << ',' << result.queries << ','
               << result.hits << ',' << result.preprocessing_us << ','
               << result.query_ns << ',' << result.metrics.storage_bytes << ','
               << result.metrics.auxiliary_nodes << ','
               << result.metrics.stored_edge_references << ','
               << result.metrics.overflow_edges << ','
               << result.stats.broad_phase_tests << ','
               << result.stats.candidate_segment_tests << ','
               << result.stats.orientation_tests << ','
               << result.stats.exact_orientation_fallbacks << ','
               << result.stats.nodes_visited << ','
               << result.stats.cells_visited << ','
               << result.stats.duplicate_references << '\n';
    }
}

}  // namespace

int main(int argc, char** argv) {
    try {
        const std::string output_path = argc > 1 ? argv[1] : "results/benchmark.csv";
        std::cout << "Compiler: " << compiler_name() << '\n'
                  << "CPU: " << cpu_brand() << '\n'
                  << "Hardware threads: " << std::thread::hardware_concurrency() << '\n'
#if defined(NDEBUG)
                  << "Build: Release\n";
#else
                  << "Build: Debug\n";
#endif

        std::vector<Result> results;
        constexpr std::array<std::size_t, 3> sizes{{128, 2048, 32768}};
        constexpr std::size_t query_count = 512;

        for (const std::size_t total_edges : sizes) {
            const std::size_t second_edges = std::max<std::size_t>(3, total_edges / 3);
            const std::size_t first_edges = total_edges - second_edges;
            const Outline outer = regular_outline(first_edges, 1000.0, 0.0);
            const Outline inner = regular_outline(second_edges, 300.0, 0.013);
            std::vector<Workload> workloads = make_workloads(outer, query_count);

            auto oracle = collision::make_linear_index(
                outer, inner, collision::PredicatePolicy::AlwaysExact);
            for (Workload& workload : workloads) {
                establish_expected(workload, *oracle);
            }

            struct NamedFactory {
                std::string name;
                std::function<std::unique_ptr<CollisionIndex>()> make;
            };
            std::vector<NamedFactory> factories;
            factories.push_back({"LinearAlwaysExact", [&] {
                return collision::make_linear_index(
                    outer, inner, collision::PredicatePolicy::AlwaysExact);
            }});
            factories.push_back({"LinearAdaptiveExact", [&] {
                return collision::make_linear_index(outer, inner);
            }});
            factories.push_back({"BvhAdaptiveExact", [&] {
                return collision::make_bvh_index(outer, inner);
            }});
            factories.push_back({"UniformGridAdaptiveExact", [&] {
                return collision::make_uniform_grid_index(outer, inner);
            }});

            std::cout << "\nN=" << total_edges << " (n1=" << first_edges
                      << ", n2=" << second_edges << ")\n";
            for (const NamedFactory& factory : factories) {
                const int build_samples = total_edges >= 32768 ? 3 : 7;
                auto [build_us, index] = measure_preprocessing(factory.make, build_samples);
                const collision::IndexMetrics metrics = index->metrics();
                std::cout << "  " << std::setw(25) << std::left << index->name()
                          << " build " << std::setw(10) << std::right
                          << std::fixed << std::setprecision(2) << build_us
                          << " us, storage " << metrics.storage_bytes << " B\n";

                for (const Workload& workload : workloads) {
                    const double query_ns = measure_queries(*index, workload);
                    const collision::QueryStats stats = collect_stats(*index, workload);
                    results.push_back({
                        std::string(index->name()), "regular", total_edges,
                        first_edges, second_edges, workload.name,
                        workload.queries.size(), workload.expected_hits, build_us,
                        query_ns, metrics, stats,
                    });
                    std::cout << "    " << std::setw(31) << std::left << workload.name
                              << std::setw(12) << std::right << std::fixed
                              << std::setprecision(1) << query_ns << " ns/query, hits "
                              << workload.expected_hits << '/' << workload.queries.size()
                              << '\n';
                }
            }
        }

        // A second geometry distribution separates the BVH and grid objectives:
        // 80% of each loop's edges occupy a small arc, yet all typical edge
        // lengths remain between 0.1 and 1,000 application units.
        {
            constexpr std::size_t total_edges = 2048;
            constexpr std::size_t first_edges = 1366;
            constexpr std::size_t second_edges = 682;
            const Outline outer = clustered_outline(first_edges, 1000.0, 0.0);
            const Outline inner = clustered_outline(second_edges, 300.0, 0.013);
            Workload workload = make_clustered_cell_workload(query_count);
            auto oracle = collision::make_linear_index(
                outer, inner, collision::PredicatePolicy::AlwaysExact);
            establish_expected(workload, *oracle);

            struct NamedFactory {
                std::function<std::unique_ptr<CollisionIndex>()> make;
            };
            const std::array<NamedFactory, 4> factories{{
                { [&] { return collision::make_linear_index(
                    outer, inner, collision::PredicatePolicy::AlwaysExact); } },
                { [&] { return collision::make_linear_index(outer, inner); } },
                { [&] { return collision::make_bvh_index(outer, inner); } },
                { [&] { return collision::make_uniform_grid_index(outer, inner); } },
            }};

            std::cout << "\nN=2048 clustered geometry (80% of edges in a 0.2-radian arc)\n";
            for (const NamedFactory& factory : factories) {
                auto [build_us, index] = measure_preprocessing(factory.make, 7);
                const collision::IndexMetrics metrics = index->metrics();
                const double query_ns = measure_queries(*index, workload);
                const collision::QueryStats stats = collect_stats(*index, workload);
                results.push_back({
                    std::string(index->name()), "clustered_80pct_arc", total_edges,
                    first_edges, second_edges, workload.name, workload.queries.size(),
                    workload.expected_hits, build_us, query_ns, metrics, stats,
                });
                std::cout << "  " << std::setw(25) << std::left << index->name()
                          << " build " << std::setw(10) << std::right << std::fixed
                          << std::setprecision(2) << build_us << " us, "
                          << std::setw(12) << std::setprecision(1) << query_ns
                          << " ns/query, hits " << workload.expected_hits << '/'
                          << workload.queries.size() << '\n';
            }
        }

        std::ofstream output(output_path, std::ios::binary);
        if (!output) {
            throw std::runtime_error("could not open benchmark output: " + output_path);
        }
        write_csv(output, results);
        std::cout << "\nWrote " << results.size() << " rows to " << output_path << '\n';
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << "Benchmark failed: " << exception.what() << '\n';
        return 1;
    }
}
