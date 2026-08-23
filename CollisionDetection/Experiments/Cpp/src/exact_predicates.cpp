#include "collision/exact_predicates.hpp"

#include <array>
#include <cfenv>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <limits>

namespace collision {
namespace {

// Multiplying any finite binary32 value by 2^149 produces an integer.  Its
// magnitude needs at most 277 bits; subtracting two such values needs at most
// 278 bits.  Nine base-2^32 limbs therefore suffice for a coordinate
// difference, and eighteen limbs suffice for a product of two differences.
constexpr std::size_t kDifferenceLimbs = 9;
constexpr std::size_t kProductLimbs = 2 * kDifferenceLimbs;

using DifferenceMagnitude = std::array<std::uint32_t, kDifferenceLimbs>;
using ProductMagnitude = std::array<std::uint32_t, kProductLimbs>;

struct SignedCoordinate {
    int sign = 0;
    DifferenceMagnitude magnitude{};
};

struct SignedDifference {
    int sign = 0;
    DifferenceMagnitude magnitude{};
};

static_assert(sizeof(float) == sizeof(std::uint32_t));
static_assert(std::numeric_limits<float>::is_iec559);
static_assert(std::numeric_limits<float>::radix == 2);
static_assert(std::numeric_limits<float>::digits == 24);
static_assert(std::numeric_limits<double>::is_iec559);
static_assert(std::numeric_limits<double>::radix == 2);
static_assert(std::numeric_limits<double>::digits == 53);

template <std::size_t LimbCount>
int compare_magnitudes(const std::array<std::uint32_t, LimbCount>& left,
                       const std::array<std::uint32_t, LimbCount>& right) noexcept {
    for (std::size_t index = LimbCount; index-- > 0;) {
        if (left[index] < right[index]) {
            return -1;
        }
        if (left[index] > right[index]) {
            return 1;
        }
    }
    return 0;
}

DifferenceMagnitude add_magnitudes(const DifferenceMagnitude& left,
                                   const DifferenceMagnitude& right) noexcept {
    DifferenceMagnitude result{};
    std::uint64_t carry = 0;
    for (std::size_t index = 0; index < kDifferenceLimbs; ++index) {
        const std::uint64_t sum = static_cast<std::uint64_t>(left[index]) +
                                  static_cast<std::uint64_t>(right[index]) + carry;
        result[index] = static_cast<std::uint32_t>(sum);
        carry = sum >> 32U;
    }

    // A difference of finite binary32 values occupies at most 278 bits, so
    // the 288-bit result cannot overflow.
    return result;
}

DifferenceMagnitude subtract_magnitudes(
    const DifferenceMagnitude& larger,
    const DifferenceMagnitude& smaller) noexcept {
    DifferenceMagnitude result{};
    std::uint64_t borrow = 0;
    for (std::size_t index = 0; index < kDifferenceLimbs; ++index) {
        const std::uint64_t subtrahend =
            static_cast<std::uint64_t>(smaller[index]) + borrow;
        const std::uint64_t minuend = larger[index];
        if (minuend >= subtrahend) {
            result[index] = static_cast<std::uint32_t>(minuend - subtrahend);
            borrow = 0;
        } else {
            result[index] = static_cast<std::uint32_t>(
                (std::uint64_t{1} << 32U) + minuend - subtrahend);
            borrow = 1;
        }
    }
    return result;
}

SignedCoordinate decode_coordinate(float value) noexcept {
    std::uint32_t bits = 0;
    std::memcpy(&bits, &value, sizeof(bits));

    const std::uint32_t exponent = (bits >> 23U) & 0xffU;
    const std::uint32_t fraction = bits & 0x7fffffU;
    const std::uint32_t significand =
        exponent == 0 ? fraction : (fraction | 0x800000U);

    SignedCoordinate result;
    if (significand == 0) {
        // Treat +0 and -0 as the same exact real value.
        return result;
    }

    result.sign = (bits & 0x80000000U) == 0 ? 1 : -1;

    // For a normal value, value * 2^149 is significand * 2^(exponent-1).
    // For a subnormal value, it is simply the fraction.
    const std::uint32_t shift = exponent == 0 ? 0 : exponent - 1U;
    const std::size_t limb = shift / 32U;
    const unsigned bit = shift % 32U;
    const std::uint64_t shifted = static_cast<std::uint64_t>(significand) << bit;
    result.magnitude[limb] = static_cast<std::uint32_t>(shifted);
    if (limb + 1U < kDifferenceLimbs) {
        result.magnitude[limb + 1U] = static_cast<std::uint32_t>(shifted >> 32U);
    }
    return result;
}

SignedDifference subtract_coordinates(const SignedCoordinate& left,
                                      const SignedCoordinate& right) noexcept {
    if (left.sign == 0) {
        return {-right.sign, right.magnitude};
    }
    if (right.sign == 0) {
        return {left.sign, left.magnitude};
    }

    if (left.sign != right.sign) {
        return {left.sign, add_magnitudes(left.magnitude, right.magnitude)};
    }

    const int comparison = compare_magnitudes(left.magnitude, right.magnitude);
    if (comparison == 0) {
        return {};
    }
    if (comparison > 0) {
        return {left.sign,
                subtract_magnitudes(left.magnitude, right.magnitude)};
    }
    return {-left.sign,
            subtract_magnitudes(right.magnitude, left.magnitude)};
}

ProductMagnitude multiply_magnitudes(const DifferenceMagnitude& left,
                                     const DifferenceMagnitude& right) noexcept {
    ProductMagnitude result{};
    for (std::size_t left_index = 0; left_index < kDifferenceLimbs;
         ++left_index) {
        std::uint64_t carry = 0;
        for (std::size_t right_index = 0; right_index < kDifferenceLimbs;
             ++right_index) {
            const std::size_t output_index = left_index + right_index;
            const std::uint64_t accumulated =
                static_cast<std::uint64_t>(left[left_index]) *
                    static_cast<std::uint64_t>(right[right_index]) +
                static_cast<std::uint64_t>(result[output_index]) + carry;
            result[output_index] = static_cast<std::uint32_t>(accumulated);
            carry = accumulated >> 32U;
        }
        result[left_index + kDifferenceLimbs] =
            static_cast<std::uint32_t>(carry);
    }
    return result;
}

int exact_orientation_sign(Point a, Point b, Point c) noexcept {
    const SignedCoordinate ax = decode_coordinate(a.x);
    const SignedCoordinate ay = decode_coordinate(a.y);
    const SignedCoordinate bx = decode_coordinate(b.x);
    const SignedCoordinate by = decode_coordinate(b.y);
    const SignedCoordinate cx = decode_coordinate(c.x);
    const SignedCoordinate cy = decode_coordinate(c.y);

    const SignedDifference bax = subtract_coordinates(bx, ax);
    const SignedDifference bay = subtract_coordinates(by, ay);
    const SignedDifference cax = subtract_coordinates(cx, ax);
    const SignedDifference cay = subtract_coordinates(cy, ay);

    const int left_sign = bax.sign * cay.sign;
    const int right_sign = bay.sign * cax.sign;
    if (left_sign == 0) {
        return -right_sign;
    }
    if (right_sign == 0) {
        return left_sign;
    }
    if (left_sign != right_sign) {
        return left_sign;
    }

    const ProductMagnitude left =
        multiply_magnitudes(bax.magnitude, cay.magnitude);
    const ProductMagnitude right =
        multiply_magnitudes(bay.magnitude, cax.magnitude);
    const int comparison = compare_magnitudes(left, right);
    if (comparison == 0) {
        return 0;
    }
    return comparison > 0 ? left_sign : -left_sign;
}

int filtered_orientation_sign(Point a, Point b, Point c) noexcept {
    // This is the orient2d "A" filter.  All binary32 inputs convert exactly to
    // binary64.  Full-range input differences and products are normal finite
    // binary64 values, so neither overflow nor gradual-underflow caveats apply.
    const double bax = binary32_to_double_exact(b.x) - binary32_to_double_exact(a.x);
    const double bay = binary32_to_double_exact(b.y) - binary32_to_double_exact(a.y);
    const double cax = binary32_to_double_exact(c.x) - binary32_to_double_exact(a.x);
    const double cay = binary32_to_double_exact(c.y) - binary32_to_double_exact(a.y);
    const double left = bax * cay;
    const double right = bay * cax;
    const double determinant = left - right;
    const double permanent = std::fabs(left) + std::fabs(right);

    // u = 2^-53.  (3 + 16u)u bounds the accumulated binary64 rounding error
    // in the determinant when operations use round-to-nearest.
    constexpr double kErrorBoundA =
        (3.0 + 16.0 * 0x1p-53) * 0x1p-53;
    const double error_bound = kErrorBoundA * permanent;
    if (determinant > error_bound) {
        return 1;
    }
    if (determinant < -error_bound) {
        return -1;
    }
    return 0;
}

bool point_on_segment(Point a, Point b, Point point) noexcept {
    return exact_less_equal(exact_min(a.x, b.x), point.x) &&
           exact_less_equal(point.x, exact_max(a.x, b.x)) &&
           exact_less_equal(exact_min(a.y, b.y), point.y) &&
           exact_less_equal(point.y, exact_max(a.y, b.y));
}

bool opposite_nonzero_signs(int left, int right) noexcept {
    return (left < 0 && right > 0) || (left > 0 && right < 0);
}

}  // namespace

int orientation_sign(Point a,
                     Point b,
                     Point c,
                     PredicatePolicy policy,
                     QueryStats* stats) noexcept {
    if (stats != nullptr) {
        ++stats->orientation_tests;
    }

    if (policy == PredicatePolicy::AdaptiveExact) {
        if (std::fegetround() == FE_TONEAREST) {
            const int filtered_sign = filtered_orientation_sign(a, b, c);
            if (filtered_sign != 0) {
                return filtered_sign;
            }
        }
        if (stats != nullptr) {
            ++stats->exact_orientation_fallbacks;
        }
    }
    return exact_orientation_sign(a, b, c);
}

bool segments_intersect(Point a,
                        Point b,
                        Point c,
                        Point d,
                        PredicatePolicy policy,
                        QueryStats* stats) noexcept {
    // This rejection is exact because the input coordinates themselves are
    // the exact endpoints and all comparisons are between binary32 values.
    if (exact_less(exact_max(a.x, b.x), exact_min(c.x, d.x)) ||
        exact_less(exact_max(c.x, d.x), exact_min(a.x, b.x)) ||
        exact_less(exact_max(a.y, b.y), exact_min(c.y, d.y)) ||
        exact_less(exact_max(c.y, d.y), exact_min(a.y, b.y))) {
        return false;
    }

    const int abc = orientation_sign(a, b, c, policy, stats);
    const int abd = orientation_sign(a, b, d, policy, stats);
    const int cda = orientation_sign(c, d, a, policy, stats);
    const int cdb = orientation_sign(c, d, b, policy, stats);

    if (abc == 0 && point_on_segment(a, b, c)) {
        return true;
    }
    if (abd == 0 && point_on_segment(a, b, d)) {
        return true;
    }
    if (cda == 0 && point_on_segment(c, d, a)) {
        return true;
    }
    if (cdb == 0 && point_on_segment(c, d, b)) {
        return true;
    }

    return opposite_nonzero_signs(abc, abd) &&
           opposite_nonzero_signs(cda, cdb);
}

}  // namespace collision
