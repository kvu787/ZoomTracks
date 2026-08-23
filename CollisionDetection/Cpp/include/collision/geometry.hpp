#pragma once

#include <algorithm>
#include <array>
#include <bit>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <vector>

namespace collision {

struct Point {
    float x = 0.0F;
    float y = 0.0F;
};

using Outline = std::vector<Point>;

struct QueryPerimeter {
    std::array<Point, 4> vertices{};
};

// Hardware DAZ (denormals-are-zero) mode can make ordinary float comparisons
// treat a binary32 subnormal as zero. These helpers order the finite IEEE bit
// patterns directly, with -0 and +0 normalized to the same exact real value.
inline std::uint32_t finite_float_order_key(float value) noexcept {
    std::uint32_t bits = std::bit_cast<std::uint32_t>(value);
    const std::uint32_t nonzero_mask =
        0U - static_cast<std::uint32_t>((bits & 0x7FFFFFFFU) != 0U);
    bits &= nonzero_mask;  // normalize both signed-zero encodings to +0
    const std::uint32_t negative_mask = 0U - (bits >> 31U);
    return bits ^ (negative_mask | 0x80000000U);
}

inline bool exact_less(float left, float right) noexcept {
    return finite_float_order_key(left) < finite_float_order_key(right);
}

inline bool exact_less_equal(float left, float right) noexcept {
    return !exact_less(right, left);
}

inline float exact_min(float left, float right) noexcept {
    return exact_less(right, left) ? right : left;
}

inline float exact_max(float left, float right) noexcept {
    return exact_less(left, right) ? right : left;
}

// Convert from the binary32 bit pattern without a hardware float-to-double
// instruction, which can also flush a subnormal operand when DAZ is enabled.
inline double binary32_to_double_exact(float value) noexcept {
    const std::uint32_t bits = std::bit_cast<std::uint32_t>(value);
    const std::uint32_t exponent = (bits >> 23U) & 0xFFU;
    const std::uint32_t fraction = bits & 0x7FFFFFU;
    const std::uint64_t sign = static_cast<std::uint64_t>(bits >> 31U) << 63U;

    if (exponent != 0U) {
        // Normal binary32 values only need an exponent rebias and a 29-bit
        // left shift of their stored fraction.
        const std::uint64_t double_exponent =
            static_cast<std::uint64_t>(exponent + 896U) << 52U;
        const std::uint64_t double_fraction =
            static_cast<std::uint64_t>(fraction) << 29U;
        return std::bit_cast<double>(sign | double_exponent | double_fraction);
    }
    if (fraction == 0U) {
        return std::bit_cast<double>(sign);
    }

    // Normalize a binary32 subnormal entirely with integer operations. The
    // resulting value is a normal binary64 number (the smallest is 2^-149).
    const unsigned highest_bit = 31U - std::countl_zero(fraction);
    const std::uint64_t double_exponent =
        static_cast<std::uint64_t>(highest_bit + 874U) << 52U;
    const std::uint32_t leading_bit = std::uint32_t{1} << highest_bit;
    const std::uint64_t double_fraction =
        static_cast<std::uint64_t>(fraction ^ leading_bit) << (52U - highest_bit);
    return std::bit_cast<double>(sign | double_exponent | double_fraction);
}

struct Aabb {
    float min_x = std::numeric_limits<float>::infinity();
    float min_y = std::numeric_limits<float>::infinity();
    float max_x = -std::numeric_limits<float>::infinity();
    float max_y = -std::numeric_limits<float>::infinity();

    void include(Point point) noexcept {
        min_x = exact_min(min_x, point.x);
        min_y = exact_min(min_y, point.y);
        max_x = exact_max(max_x, point.x);
        max_y = exact_max(max_y, point.y);
    }

    void include(const Aabb& other) noexcept {
        min_x = exact_min(min_x, other.min_x);
        min_y = exact_min(min_y, other.min_y);
        max_x = exact_max(max_x, other.max_x);
        max_y = exact_max(max_y, other.max_y);
    }
};

inline Aabb make_aabb(Point a, Point b) noexcept {
    return {
        exact_min(a.x, b.x),
        exact_min(a.y, b.y),
        exact_max(a.x, b.x),
        exact_max(a.y, b.y),
    };
}

inline bool overlaps(const Aabb& a, const Aabb& b) noexcept {
    return exact_less_equal(a.min_x, b.max_x) &&
           exact_less_equal(b.min_x, a.max_x) &&
           exact_less_equal(a.min_y, b.max_y) &&
           exact_less_equal(b.min_y, a.max_y);
}

struct QueryStats {
    std::uint64_t broad_phase_tests = 0;
    std::uint64_t candidate_segment_tests = 0;
    std::uint64_t orientation_tests = 0;
    std::uint64_t exact_orientation_fallbacks = 0;
    std::uint64_t nodes_visited = 0;
    std::uint64_t cells_visited = 0;
    std::uint64_t duplicate_references = 0;
};

}  // namespace collision
