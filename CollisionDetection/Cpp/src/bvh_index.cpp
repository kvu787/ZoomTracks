#include "collision/algorithms.hpp"

#include <algorithm>
#include <cstddef>
#include <limits>
#include <memory>
#include <stdexcept>
#include <string_view>
#include <vector>

namespace collision {
namespace {

struct EdgeRecord {
    Point first;
    Point second;
    Aabb bounds;
};

// Nodes are stored in preorder. For an interior node, offset is the index just
// past its complete subtree (its escape index) and count is zero. For a leaf,
// offset and count describe one contiguous range in the reordered edge array.
// This representation needs no traversal stack and touches nodes linearly when
// their bounds overlap a query edge.
struct BvhNode {
    Aabb bounds;
    std::size_t offset = 0;
    std::size_t count = 0;
};

[[nodiscard]] double centroid_coordinate(const EdgeRecord& edge,
                                         bool split_on_x) noexcept {
    const double minimum = split_on_x
                               ? binary32_to_double_exact(edge.bounds.min_x)
                               : binary32_to_double_exact(edge.bounds.min_y);
    const double maximum = split_on_x
                               ? binary32_to_double_exact(edge.bounds.max_x)
                               : binary32_to_double_exact(edge.bounds.max_y);

    // binary32 endpoints are finite. Converting before adding prevents the
    // overflow that (minimum + maximum) could incur in binary32.
    return (minimum + maximum) * 0.5;
}

[[nodiscard]] std::size_t node_count_for(std::size_t edge_count,
                                         std::size_t leaf_size) {
    if (edge_count <= leaf_size) {
        return 1;
    }

    const std::size_t left_count = edge_count / 2;
    const std::size_t left_nodes = node_count_for(left_count, leaf_size);
    const std::size_t right_nodes =
        node_count_for(edge_count - left_count, leaf_size);

    const std::size_t maximum = std::numeric_limits<std::size_t>::max();
    if (right_nodes == maximum || left_nodes > maximum - right_nodes - 1) {
        throw std::length_error("BVH node count exceeds size_t capacity");
    }
    return 1 + left_nodes + right_nodes;
}

class BvhIndex final : public CollisionIndex {
public:
    BvhIndex(const Outline& first,
             const Outline& second,
             BvhOptions options)
        : leaf_size_(std::max<std::size_t>(options.leaf_size, 1)) {
        const std::size_t first_count = first.size();
        const std::size_t second_count = second.size();
        if (first_count >
            std::numeric_limits<std::size_t>::max() - second_count) {
            throw std::length_error("outline edge count exceeds size_t capacity");
        }

        edges_.reserve(first_count + second_count);
        append_outline(first);
        append_outline(second);

        if (!edges_.empty()) {
            nodes_.reserve(node_count_for(edges_.size(), leaf_size_));
            build_subtree(0, edges_.size());
        }
    }

    [[nodiscard]] std::string_view name() const noexcept override {
        return "BvhAdaptiveExact";
    }

    [[nodiscard]] bool intersects(const QueryPerimeter& query,
                                  QueryStats* stats) const override {
        for (std::size_t query_edge = 0; query_edge < query.vertices.size();
             ++query_edge) {
            const Point first = query.vertices[query_edge];
            const Point second =
                query.vertices[(query_edge + 1) % query.vertices.size()];
            const Aabb query_bounds = make_aabb(first, second);

            std::size_t node_index = 0;
            while (node_index < nodes_.size()) {
                const BvhNode& node = nodes_[node_index];
                if (stats != nullptr) {
                    ++stats->nodes_visited;
                    ++stats->broad_phase_tests;
                }

                if (!overlaps(query_bounds, node.bounds)) {
                    node_index = node.count == 0 ? node.offset
                                                 : node_index + 1;
                    continue;
                }

                if (node.count == 0) {
                    // The left child immediately follows an interior node in
                    // the flattened preorder representation.
                    ++node_index;
                    continue;
                }

                const std::size_t edge_end = node.offset + node.count;
                for (std::size_t edge_index = node.offset;
                     edge_index < edge_end;
                     ++edge_index) {
                    const EdgeRecord& edge = edges_[edge_index];
                    if (stats != nullptr) {
                        ++stats->broad_phase_tests;
                    }
                    if (!overlaps(query_bounds, edge.bounds)) {
                        continue;
                    }

                    if (stats != nullptr) {
                        ++stats->candidate_segment_tests;
                    }
                    if (segments_intersect(first,
                                           second,
                                           edge.first,
                                           edge.second,
                                           PredicatePolicy::AdaptiveExact,
                                           stats)) {
                        return true;
                    }
                }

                ++node_index;
            }
        }

        return false;
    }

    [[nodiscard]] IndexMetrics metrics() const noexcept override {
        IndexMetrics result;
        result.outline_edges = edges_.size();
        result.storage_bytes = sizeof(*this) +
                               edges_.capacity() * sizeof(EdgeRecord) +
                               nodes_.capacity() * sizeof(BvhNode);
        result.auxiliary_nodes = nodes_.size();
        result.stored_edge_references = edges_.size();
        return result;
    }

private:
    void append_outline(const Outline& outline) {
        if (outline.empty()) {
            return;
        }

        for (std::size_t index = 0; index < outline.size(); ++index) {
            const Point first = outline[index];
            const Point second = outline[(index + 1) % outline.size()];
            edges_.push_back({first, second, make_aabb(first, second)});
        }
    }

    std::size_t build_subtree(std::size_t begin, std::size_t end) {
        const std::size_t node_index = nodes_.size();
        nodes_.push_back({});

        Aabb node_bounds;
        double minimum_centroid_x = std::numeric_limits<double>::infinity();
        double minimum_centroid_y = std::numeric_limits<double>::infinity();
        double maximum_centroid_x = -std::numeric_limits<double>::infinity();
        double maximum_centroid_y = -std::numeric_limits<double>::infinity();

        for (std::size_t index = begin; index < end; ++index) {
            const EdgeRecord& edge = edges_[index];
            node_bounds.include(edge.bounds);

            const double centroid_x = centroid_coordinate(edge, true);
            const double centroid_y = centroid_coordinate(edge, false);
            minimum_centroid_x = std::min(minimum_centroid_x, centroid_x);
            minimum_centroid_y = std::min(minimum_centroid_y, centroid_y);
            maximum_centroid_x = std::max(maximum_centroid_x, centroid_x);
            maximum_centroid_y = std::max(maximum_centroid_y, centroid_y);
        }

        const std::size_t count = end - begin;
        if (count <= leaf_size_) {
            nodes_[node_index] = {node_bounds, begin, count};
            return node_index;
        }

        const double centroid_span_x = maximum_centroid_x - minimum_centroid_x;
        const double centroid_span_y = maximum_centroid_y - minimum_centroid_y;
        const bool split_on_x = centroid_span_x >= centroid_span_y;
        const std::size_t middle = begin + count / 2;

        std::nth_element(
            edges_.begin() + static_cast<std::ptrdiff_t>(begin),
            edges_.begin() + static_cast<std::ptrdiff_t>(middle),
            edges_.begin() + static_cast<std::ptrdiff_t>(end),
            [split_on_x](const EdgeRecord& left, const EdgeRecord& right) {
                return centroid_coordinate(left, split_on_x) <
                       centroid_coordinate(right, split_on_x);
            });

        build_subtree(begin, middle);
        build_subtree(middle, end);

        // Appending both children has completed this subtree. Its current end
        // is the escape index used when the interior bounds do not overlap.
        nodes_[node_index] = {node_bounds, nodes_.size(), 0};
        return node_index;
    }

    std::size_t leaf_size_;
    std::vector<EdgeRecord> edges_;
    std::vector<BvhNode> nodes_;
};

}  // namespace

std::unique_ptr<CollisionIndex> make_bvh_index(const Outline& first,
                                               const Outline& second,
                                               BvhOptions options) {
    return std::make_unique<BvhIndex>(first, second, options);
}

}  // namespace collision
