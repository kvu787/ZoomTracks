#include "collision/algorithms.hpp"
#include "collision/exact_predicates.hpp"

#include <algorithm>
#include <array>
#include <atomic>
#include <bit>
#include <cfenv>
#include <cmath>
#include <cstdint>
#include <exception>
#include <iostream>
#include <limits>
#include <memory>
#include <numbers>
#include <random>
#include <stdexcept>
#include <string>
#include <string_view>
#include <thread>
#include <vector>

#if defined(_MSC_VER) && (defined(_M_X64) || defined(_M_IX86))
#include <immintrin.h>
#endif

namespace {

using collision::Outline;
using collision::Point;
using collision::QueryPerimeter;

std::uint64_t checks = 0;

void require(bool condition, std::string_view message) {
    ++checks;
    if (!condition) {
        throw std::runtime_error(std::string(message));
    }
}

void check_segment(Point a, Point b, Point c, Point d, bool expected,
                   std::string_view label) {
    using collision::PredicatePolicy;
    const bool adaptive = collision::segments_intersect(
        a, b, c, d, PredicatePolicy::AdaptiveExact);
    const bool integer = collision::segments_intersect(
        a, b, c, d, PredicatePolicy::AlwaysExact);
    require(adaptive == expected, std::string(label) + " (adaptive)");
    require(integer == expected, std::string(label) + " (integer)");
}

void test_hand_segment_cases() {
    require(!collision::exact_less(-0.0F, 0.0F) &&
                !collision::exact_less(0.0F, -0.0F),
            "signed zeros have equal exact ordering");
    require(collision::exact_less_equal(-0.0F, 0.0F) &&
                collision::exact_less_equal(0.0F, -0.0F),
            "signed zeros compare equal");
    check_segment({0, 0}, {4, 4}, {0, 4}, {4, 0}, true,
                  "proper crossing");
    check_segment({0, 0}, {2, 0}, {2, 0}, {3, 1}, true,
                  "shared endpoint");
    check_segment({0, 0}, {4, 0}, {1, 0}, {3, 0}, true,
                  "collinear overlap");
    check_segment({0, 0}, {4, 0}, {-2, 0}, {0, 0}, true,
                  "collinear endpoint contact");
    check_segment({0, 0}, {1, 0}, {2, 0}, {3, 0}, false,
                  "collinear separated");
    check_segment({0, 0}, {4, 0}, {2, 1}, {2, 3}, false,
                  "AABB x overlap but separated");
    check_segment({0, 0}, {0, 4}, {0, 2}, {0, 6}, true,
                  "vertical overlap");

    const float denorm = std::numeric_limits<float>::denorm_min();
    check_segment({0, 0}, {4 * denorm, 0},
                  {2 * denorm, -denorm}, {2 * denorm, denorm}, true,
                  "subnormal proper crossing");
    check_segment({0, 0}, {4 * denorm, 0},
                  {2 * denorm, denorm}, {4 * denorm, denorm}, false,
                  "subnormal separated");

    const float huge = std::ldexp(1.0F, 126);
    const float huge_next = std::nextafter(huge, std::numeric_limits<float>::infinity());
    const float huge_next2 = std::nextafter(huge_next, std::numeric_limits<float>::infinity());
    check_segment({huge, huge}, {huge_next2, huge_next2},
                  {huge, huge_next2}, {huge_next2, huge}, true,
                  "huge-coordinate crossing");
    check_segment({huge, huge}, {huge_next, huge},
                  {huge, huge_next}, {huge_next, huge_next}, false,
                  "huge-coordinate separated");

    // Degenerate segments are outside the stated workload, but supporting them
    // makes the primitive easier to reuse and exercises the on-segment path.
    check_segment({1, 1}, {1, 1}, {0, 1}, {2, 1}, true,
                  "point segment on line");
    check_segment({1, 2}, {1, 2}, {0, 1}, {2, 1}, false,
                  "point segment off line");
}

int integer_orientation(std::int64_t ax, std::int64_t ay,
                        std::int64_t bx, std::int64_t by,
                        std::int64_t cx, std::int64_t cy) {
    const std::int64_t determinant =
        (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
    return (determinant > 0) - (determinant < 0);
}

float random_finite_float(std::mt19937_64& random) {
    for (;;) {
        const std::uint32_t bits = static_cast<std::uint32_t>(random());
        if ((bits & 0x7F800000U) != 0x7F800000U) {
            return std::bit_cast<float>(bits);
        }
    }
}

void test_orientation_differential() {
    using collision::PredicatePolicy;
    std::mt19937_64 random(0xBADC0FFEEULL);
    std::uniform_int_distribution<std::int64_t> small(-1'000'000, 1'000'000);

    for (int iteration = 0; iteration < 100'000; ++iteration) {
        const auto ax = small(random);
        const auto ay = small(random);
        const auto bx = small(random);
        const auto by = small(random);
        const auto cx = small(random);
        const auto cy = small(random);
        const int reference = integer_orientation(ax, ay, bx, by, cx, cy);
        const Point a{static_cast<float>(ax), static_cast<float>(ay)};
        const Point b{static_cast<float>(bx), static_cast<float>(by)};
        const Point c{static_cast<float>(cx), static_cast<float>(cy)};
        require(collision::orientation_sign(a, b, c, PredicatePolicy::AlwaysExact) == reference,
                "bounded-integer orientation reference");
        require(collision::orientation_sign(a, b, c, PredicatePolicy::AdaptiveExact) == reference,
                "bounded-integer adaptive orientation reference");
    }

    collision::QueryStats stats{};
    for (int iteration = 0; iteration < 200'000; ++iteration) {
        const Point a{random_finite_float(random), random_finite_float(random)};
        const Point b{random_finite_float(random), random_finite_float(random)};
        const Point c{random_finite_float(random), random_finite_float(random)};
        const int integer = collision::orientation_sign(
            a, b, c, PredicatePolicy::AlwaysExact);
        const int adaptive = collision::orientation_sign(
            a, b, c, PredicatePolicy::AdaptiveExact, &stats);
        require(adaptive == integer, "full-range adaptive/integer orientation differential");
    }

    for (int iteration = 0; iteration < 50'000; ++iteration) {
        const Point a{random_finite_float(random), random_finite_float(random)};
        const Point b{random_finite_float(random), random_finite_float(random)};
        const Point c{random_finite_float(random), random_finite_float(random)};
        const Point d{random_finite_float(random), random_finite_float(random)};
        const bool integer = collision::segments_intersect(
            a, b, c, d, PredicatePolicy::AlwaysExact);
        const bool adaptive = collision::segments_intersect(
            a, b, c, d, PredicatePolicy::AdaptiveExact);
        require(adaptive == integer,
                "full-range adaptive/integer segment differential");
    }

    // Force exact fallbacks through cancellation/collinearity and verify that
    // instrumentation observes them.
    for (int i = 0; i < 100; ++i) {
        const Point a{static_cast<float>(i), static_cast<float>(i)};
        const Point b{static_cast<float>(i + 1), static_cast<float>(i + 1)};
        const Point c{static_cast<float>(i + 2), static_cast<float>(i + 2)};
        require(collision::orientation_sign(a, b, c,
                    PredicatePolicy::AdaptiveExact, &stats) == 0,
                "collinear fallback result");
    }
    require(stats.exact_orientation_fallbacks >= 100,
            "adaptive predicate records exact fallbacks");

    const int original_rounding = std::fegetround();
    bool alternate_rounding_ok = true;
    for (const int mode : {FE_DOWNWARD, FE_UPWARD, FE_TOWARDZERO}) {
        if (std::fesetround(mode) == 0) {
            collision::QueryStats rounding_stats{};
            const Point a{0.0F, 0.0F};
            const Point b{3.0F, 1.0F};
            const Point c{1.0F, 2.0F};
            const int exact = collision::orientation_sign(
                a, b, c, PredicatePolicy::AlwaysExact);
            const int adaptive = collision::orientation_sign(
                a, b, c, PredicatePolicy::AdaptiveExact, &rounding_stats);
            alternate_rounding_ok = alternate_rounding_ok && adaptive == exact &&
                                    rounding_stats.exact_orientation_fallbacks == 1;
        }
    }
    if (original_rounding != -1) {
        std::fesetround(original_rounding);
    }
    require(alternate_rounding_ok,
            "non-nearest rounding modes force the exact orientation path");
}

Outline square(float half_extent) {
    return {
        {-half_extent, -half_extent},
        {half_extent, -half_extent},
        {half_extent, half_extent},
        {-half_extent, half_extent},
    };
}

QueryPerimeter axis_rectangle(float center_x, float center_y,
                              float half_x, float half_y) {
    return {{{
        {center_x - half_x, center_y - half_y},
        {center_x + half_x, center_y - half_y},
        {center_x + half_x, center_y + half_y},
        {center_x - half_x, center_y + half_y},
    }}};
}

QueryPerimeter rotated_rectangle(float center_x, float center_y,
                                 float half_x, float half_y, double angle) {
    const double ux = std::cos(angle);
    const double uy = std::sin(angle);
    const double vx = -uy;
    const double vy = ux;
    const std::array<std::array<double, 2>, 4> signs{{
        {{-1, -1}}, {{1, -1}}, {{1, 1}}, {{-1, 1}},
    }};
    QueryPerimeter result{};
    for (std::size_t i = 0; i < 4; ++i) {
        result.vertices[i] = {
            static_cast<float>(center_x + signs[i][0] * half_x * ux +
                               signs[i][1] * half_y * vx),
            static_cast<float>(center_y + signs[i][0] * half_x * uy +
                               signs[i][1] * half_y * vy),
        };
    }
    return result;
}

std::vector<std::unique_ptr<collision::CollisionIndex>> make_all_indices(
    const Outline& outer, const Outline& inner) {
    std::vector<std::unique_ptr<collision::CollisionIndex>> result;
    result.push_back(collision::make_linear_index(
        outer, inner, collision::PredicatePolicy::AlwaysExact));
    result.push_back(collision::make_linear_index(outer, inner));
    result.push_back(collision::make_bvh_index(outer, inner));
    result.push_back(collision::make_uniform_grid_index(outer, inner));
    return result;
}

void check_query(const std::vector<std::unique_ptr<collision::CollisionIndex>>& indices,
                 const QueryPerimeter& query, bool expected, std::string_view label) {
    for (const auto& index : indices) {
        collision::QueryStats stats{};
        const bool actual = index->intersects(query, &stats);
        require(actual == expected,
                std::string(label) + " (" + std::string(index->name()) + ")");
    }
}

void test_hand_algorithm_cases() {
    const Outline outer = square(10.0F);
    const Outline inner = square(2.0F);
    const auto indices = make_all_indices(outer, inner);

    check_query(indices, axis_rectangle(20, 0, 1, 1), false, "outside both");
    check_query(indices, axis_rectangle(6, 0, 1, 1), false, "inside annulus");
    check_query(indices, axis_rectangle(0, 0, 1, 1), false, "inside hole");
    check_query(indices, axis_rectangle(10, 0, 1, 1), true, "outer crossing");
    check_query(indices, axis_rectangle(2, 0, 0.5F, 0.5F), true, "inner crossing");
    check_query(indices, axis_rectangle(0, 0, 20, 20), false,
                "containment without perimeter contact");
    check_query(indices, axis_rectangle(10.5F, 0, 0.5F, 3), true,
                "collinear overlap with outline edge");
    check_query(indices, axis_rectangle(11, 11, 1, 1), true,
                "single corner contact");
    check_query(indices, rotated_rectangle(10.5F, 0, 1, 0.25F,
                                            std::numbers::pi / 4.0),
                true, "rotated outer crossing");

    QueryPerimeter clockwise = axis_rectangle(10, 0, 1, 1);
    std::reverse(clockwise.vertices.begin(), clockwise.vertices.end());
    check_query(indices, clockwise, true, "clockwise query order");

    QueryPerimeter rounded_trapezoid{{{{-3.0F, 1.0F}, {3.0F, 1.0000001F},
                                       {2.5F, 3.0F}, {-2.7F, 3.1F}}}};
    check_query(indices, rounded_trapezoid, true,
                "authoritative non-ideal rectangle vertices");

    collision::GridOptions overflow_options{};
    overflow_options.target_cells_per_edge =
        std::numeric_limits<double>::quiet_NaN();
    overflow_options.max_cells_per_edge = 0;
    overflow_options.max_axis_cells = 0;
    const auto overflow_grid = collision::make_uniform_grid_index(
        outer, inner, overflow_options);
    require(overflow_grid->metrics().overflow_edges == outer.size() + inner.size(),
            "forced-overflow grid metrics");
    require(overflow_grid->intersects(axis_rectangle(10, 0, 1, 1)),
            "forced-overflow grid hit");
    require(!overflow_grid->intersects(axis_rectangle(20, 0, 1, 1)),
            "forced-overflow grid outside-domain miss");

    for (const std::size_t leaf_size : {0U, 1U, 3U, 1000U}) {
        const auto bvh = collision::make_bvh_index(
            outer, inner, collision::BvhOptions{leaf_size});
        require(bvh->intersects(axis_rectangle(10, 0, 1, 1)),
                "BVH leaf-size hit");
        require(!bvh->intersects(axis_rectangle(6, 0, 1, 1)),
                "BVH leaf-size miss");
    }
}

Outline radial_outline(std::size_t count, double radius, double phase,
                       double ripple) {
    Outline result;
    result.reserve(count);
    for (std::size_t i = 0; i < count; ++i) {
        const double angle = phase + 2.0 * std::numbers::pi *
                                         static_cast<double>(i) /
                                         static_cast<double>(count);
        const double local_radius = radius *
            (1.0 + ripple * std::sin(5.0 * angle) +
             0.5 * ripple * std::cos(11.0 * angle));
        result.push_back({static_cast<float>(local_radius * std::cos(angle)),
                          static_cast<float>(local_radius * std::sin(angle))});
    }
    return result;
}

void test_random_algorithm_differential() {
    const Outline outer = radial_outline(733, 1000.0, 0.01, 0.08);
    const Outline inner = radial_outline(379, 280.0, 0.02, 0.12);
    const auto indices = make_all_indices(outer, inner);

    std::mt19937_64 random(0x123456789ABCDEF0ULL);
    std::uniform_real_distribution<float> center(-1500.0F, 1500.0F);
    std::uniform_real_distribution<float> size(0.05F, 700.0F);
    std::uniform_real_distribution<double> angle(0.0, 2.0 * std::numbers::pi);

    for (int iteration = 0; iteration < 10'000; ++iteration) {
        const QueryPerimeter query = rotated_rectangle(
            center(random), center(random), size(random), size(random), angle(random));
        const bool reference = indices.front()->intersects(query);
        for (std::size_t index = 1; index < indices.size(); ++index) {
            require(indices[index]->intersects(query) == reference,
                    "random algorithm differential");
        }
    }
}

void test_extreme_scale_indices() {
    const float tiny = std::numeric_limits<float>::denorm_min();
    const Outline tiny_outer{{-100 * tiny, -100 * tiny},
                             {100 * tiny, -100 * tiny},
                             {100 * tiny, 100 * tiny},
                             {-100 * tiny, 100 * tiny}};
    const Outline tiny_inner{{-20 * tiny, -20 * tiny},
                             {20 * tiny, -20 * tiny},
                             {20 * tiny, 20 * tiny},
                             {-20 * tiny, 20 * tiny}};
    const auto tiny_indices = make_all_indices(tiny_outer, tiny_inner);
    check_query(tiny_indices,
                axis_rectangle(100 * tiny, 0, 10 * tiny, 10 * tiny),
                true, "subnormal-scale outline query");
    check_query(tiny_indices,
                axis_rectangle(60 * tiny, 0, 10 * tiny, 10 * tiny),
                false, "subnormal-scale containment query");

    const float scale = std::ldexp(1.0F, 124);
    const Outline huge_outer{{-4 * scale, -4 * scale},
                             {4 * scale, -4 * scale},
                             {4 * scale, 4 * scale},
                             {-4 * scale, 4 * scale}};
    const Outline huge_inner{{-scale, -scale}, {scale, -scale},
                             {scale, scale}, {-scale, scale}};
    const auto huge_indices = make_all_indices(huge_outer, huge_inner);
    check_query(huge_indices, axis_rectangle(4 * scale, 0, scale, scale),
                true, "huge-scale outline crossing");
    check_query(huge_indices, axis_rectangle(0, 0, 0.5F * scale, 0.5F * scale),
                false, "huge-scale inside hole");
}

void test_concave_and_minimum_vertex_outlines() {
    const Outline concave_outer{{-10, -10}, {10, -10}, {10, 10}, {2, 10},
                                {2, -2}, {-2, -2}, {-2, 10}, {-10, 10}};
    const Outline concave_inner{{-1, -7}, {1, -7}, {1, -5}, {-1, -5}};
    const auto concave_indices = make_all_indices(concave_outer, concave_inner);
    check_query(concave_indices, axis_rectangle(0, 5, 0.5F, 0.5F), false,
                "concave notch miss");
    check_query(concave_indices, axis_rectangle(2, 5, 0.5F, 0.5F), true,
                "concave notch wall crossing");
    check_query(concave_indices, axis_rectangle(0, -7, 1.25F, 0.25F), true,
                "concave outline inner-loop crossing");

    const Outline triangle_outer{{0, 10}, {-10, -10}, {10, -10}};
    const Outline triangle_inner{{0, 0}, {-1, -2}, {1, -2}};
    const auto triangle_indices = make_all_indices(triangle_outer, triangle_inner);
    check_query(triangle_indices, axis_rectangle(0, 0, 0.25F, 0.25F), true,
                "three-edge inner outline crossing");
    check_query(triangle_indices, axis_rectangle(0, 5, 0.25F, 0.25F), false,
                "three-edge outline containment miss");
}

void test_concurrent_grid_queries() {
    const Outline outer = radial_outline(733, 1000.0, 0.01, 0.08);
    const Outline inner = radial_outline(379, 280.0, 0.02, 0.12);
    const auto oracle = collision::make_linear_index(
        outer, inner, collision::PredicatePolicy::AlwaysExact);
    const auto grid = collision::make_uniform_grid_index(outer, inner);

    std::vector<QueryPerimeter> queries;
    std::vector<unsigned char> expected;
    for (int index = 0; index < 256; ++index) {
        const double angle = 2.0 * std::numbers::pi * index / 256.0;
        queries.push_back(rotated_rectangle(
            static_cast<float>(1200.0 * std::cos(angle)),
            static_cast<float>(1200.0 * std::sin(angle)),
            30.0F, 10.0F, 0.37 * angle));
        expected.push_back(static_cast<unsigned char>(oracle->intersects(queries.back())));
    }

    std::atomic<bool> all_correct{true};
    std::array<std::thread, 8> threads;
    for (std::thread& worker : threads) {
        worker = std::thread([&] {
            for (std::size_t query = 0; query < queries.size(); ++query) {
                if (grid->intersects(queries[query]) != (expected[query] != 0)) {
                    all_correct.store(false, std::memory_order_relaxed);
                    return;
                }
            }
        });
    }
    for (std::thread& worker : threads) {
        worker.join();
    }
    require(all_correct.load(std::memory_order_relaxed),
            "concurrent const grid queries");
}

void test_x86_denormals_are_zero_mode() {
#if defined(_MSC_VER) && (defined(_M_X64) || defined(_M_IX86))
    const auto subnormal = [](std::uint32_t magnitude, bool negative = false) {
        return std::bit_cast<float>(magnitude | (negative ? 0x80000000U : 0U));
    };
    const float s = subnormal(1U);

    const Outline outer{{subnormal(100U, true), subnormal(100U, true)},
                        {subnormal(100U), subnormal(100U, true)},
                        {subnormal(100U), subnormal(100U)},
                        {subnormal(100U, true), subnormal(100U)}};
    const Outline inner{{subnormal(20U, true), subnormal(20U, true)},
                        {subnormal(20U), subnormal(20U, true)},
                        {subnormal(20U), subnormal(20U)},
                        {subnormal(20U, true), subnormal(20U)}};
    const QueryPerimeter hit{{{{subnormal(90U), subnormal(10U, true)},
                               {subnormal(110U), subnormal(10U, true)},
                               {subnormal(110U), subnormal(10U)},
                               {subnormal(90U), subnormal(10U)}}}};
    const QueryPerimeter miss{{{{subnormal(50U), subnormal(10U, true)},
                                {subnormal(70U), subnormal(10U, true)},
                                {subnormal(70U), subnormal(10U)},
                                {subnormal(50U), subnormal(10U)}}}};

    bool orientation_ok = false;
    bool separated_segments_ok = false;
    bool indices_ok = true;
    {
        struct MxcsrGuard {
            unsigned saved = _mm_getcsr();
            ~MxcsrGuard() { _mm_setcsr(saved); }
        } guard;
        _mm_setcsr(guard.saved | 0x0040U);  // DAZ

        orientation_ok = collision::orientation_sign(
            {0.0F, 0.0F}, {1.0F, s},
            {std::numeric_limits<float>::max(),
             std::numeric_limits<float>::min()},
            collision::PredicatePolicy::AdaptiveExact) == -1;

        // Both segments have length approximately one, but the exact gap s is
        // lost by ordinary hardware comparisons when DAZ is active.
        separated_segments_ok = !collision::segments_intersect(
            {0.0F, -1.0F}, {0.0F, 0.0F}, {0.0F, s}, {0.0F, 1.0F},
            collision::PredicatePolicy::AdaptiveExact);

        const auto indices = make_all_indices(outer, inner);
        for (const auto& index : indices) {
            indices_ok = indices_ok && index->intersects(hit) &&
                         !index->intersects(miss);
        }
    }
    require(orientation_ok, "DAZ-safe adaptive orientation");
    require(separated_segments_ok, "DAZ-safe exact bounds comparison");
    require(indices_ok, "DAZ-safe indices at subnormal scale");
#endif
}

}  // namespace

int main() {
    try {
        test_hand_segment_cases();
        test_orientation_differential();
        test_hand_algorithm_cases();
        test_random_algorithm_differential();
        test_extreme_scale_indices();
        test_concave_and_minimum_vertex_outlines();
        test_concurrent_grid_queries();
        test_x86_denormals_are_zero_mode();
        std::cout << "PASS: " << checks << " checks\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << "FAIL after " << checks << " checks: "
                  << exception.what() << '\n';
        return 1;
    }
}
