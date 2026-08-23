#pragma once

#include "collision/exact_predicates.hpp"
#include "collision/geometry.hpp"

#include <cstddef>
#include <memory>
#include <string_view>

namespace collision {

struct IndexMetrics {
    std::size_t outline_edges = 0;
    std::size_t storage_bytes = 0;
    std::size_t auxiliary_nodes = 0;
    std::size_t stored_edge_references = 0;
    std::size_t overflow_edges = 0;
};

class CollisionIndex {
public:
    virtual ~CollisionIndex() = default;

    [[nodiscard]] virtual std::string_view name() const noexcept = 0;
    [[nodiscard]] virtual bool intersects(const QueryPerimeter& query,
                                          QueryStats* stats = nullptr) const = 0;
    [[nodiscard]] virtual IndexMetrics metrics() const noexcept = 0;
};

struct BvhOptions {
    std::size_t leaf_size = 8;
};

struct GridOptions {
    // Approximately this many cells are built per outline edge before aspect
    // ratio adjustment. Values <= 0 are replaced by 1.
    double target_cells_per_edge = 1.0;
    // Edges whose AABB would occupy more cells are kept in a small overflow
    // list and tested for every query edge. This bounds preprocessing/storage.
    std::size_t max_cells_per_edge = 256;
    std::size_t max_axis_cells = 4096;
};

std::unique_ptr<CollisionIndex> make_linear_index(
    const Outline& first,
    const Outline& second,
    PredicatePolicy policy = PredicatePolicy::AdaptiveExact);

std::unique_ptr<CollisionIndex> make_bvh_index(
    const Outline& first,
    const Outline& second,
    BvhOptions options = {});

std::unique_ptr<CollisionIndex> make_uniform_grid_index(
    const Outline& first,
    const Outline& second,
    GridOptions options = {});

}  // namespace collision
