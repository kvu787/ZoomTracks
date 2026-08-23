#include "collision/algorithms.hpp"

#include <array>
#include <cstdint>
#include <memory>
#include <string_view>
#include <utility>
#include <vector>

namespace collision {
namespace {

struct StoredSegment {
    Point a;
    Point b;
    Aabb bounds;
};

void append_outline(const Outline& outline,
                    std::vector<StoredSegment>& output,
                    Aabb& domain) {
    const std::size_t count = outline.size();
    for (std::size_t index = 0; index < count; ++index) {
        const Point a = outline[index];
        const Point b = outline[(index + 1U) % count];
        const Aabb bounds = make_aabb(a, b);
        output.push_back({a, b, bounds});
        domain.include(bounds);
    }
}

class LinearIndex final : public CollisionIndex {
public:
    LinearIndex(const Outline& first,
                const Outline& second,
                PredicatePolicy policy)
        : policy_(policy) {
        segments_.reserve(first.size() + second.size());
        append_outline(first, segments_, domain_);
        append_outline(second, segments_, domain_);
    }

    [[nodiscard]] std::string_view name() const noexcept override {
        return policy_ == PredicatePolicy::AlwaysExact
                   ? "LinearAlwaysExact"
                   : "LinearAdaptiveExact";
    }

    [[nodiscard]] bool intersects(const QueryPerimeter& query,
                                  QueryStats* stats) const override {
        for (std::size_t query_edge = 0; query_edge < 4; ++query_edge) {
            const Point a = query.vertices[query_edge];
            const Point b = query.vertices[(query_edge + 1U) % 4U];
            const Aabb query_bounds = make_aabb(a, b);

            if (stats != nullptr) {
                ++stats->broad_phase_tests;
            }
            if (!overlaps(query_bounds, domain_)) {
                continue;
            }

            for (const StoredSegment& segment : segments_) {
                if (stats != nullptr) {
                    ++stats->broad_phase_tests;
                }
                if (!overlaps(query_bounds, segment.bounds)) {
                    continue;
                }
                if (stats != nullptr) {
                    ++stats->candidate_segment_tests;
                }
                if (segments_intersect(a, b, segment.a, segment.b, policy_, stats)) {
                    return true;
                }
            }
        }
        return false;
    }

    [[nodiscard]] IndexMetrics metrics() const noexcept override {
        return {
            segments_.size(),
            sizeof(*this) + segments_.capacity() * sizeof(StoredSegment),
            0,
            segments_.size(),
            0,
        };
    }

private:
    PredicatePolicy policy_;
    std::vector<StoredSegment> segments_;
    Aabb domain_;
};

}  // namespace

std::unique_ptr<CollisionIndex> make_linear_index(const Outline& first,
                                                  const Outline& second,
                                                  PredicatePolicy policy) {
    return std::make_unique<LinearIndex>(first, second, policy);
}

}  // namespace collision
