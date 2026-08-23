#pragma once

#include "collision/geometry.hpp"

namespace collision {

enum class PredicatePolicy {
    AdaptiveExact,
    AlwaysExact,
};

// Returns -1, 0, or +1 for the exact sign of
// (b - a) x (c - a), interpreting each binary32 input as its exact value.
int orientation_sign(Point a,
                     Point b,
                     Point c,
                     PredicatePolicy policy = PredicatePolicy::AdaptiveExact,
                     QueryStats* stats = nullptr) noexcept;

// Exact closed-segment intersection: proper crossings, endpoint contact,
// tangency, and collinear overlap all return true.
bool segments_intersect(Point a,
                        Point b,
                        Point c,
                        Point d,
                        PredicatePolicy policy = PredicatePolicy::AdaptiveExact,
                        QueryStats* stats = nullptr) noexcept;

}  // namespace collision
