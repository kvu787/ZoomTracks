#include "collision/algorithms.hpp"

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <memory>
#include <stdexcept>
#include <string_view>
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

std::size_t choose_axis_count(double desired_cell_count,
                              double this_extent,
                              double other_extent,
                              std::size_t maximum) noexcept {
    if (maximum <= 1U || this_extent <= 0.0) {
        return 1U;
    }
    if (other_extent <= 0.0) {
        const double count = std::ceil(desired_cell_count);
        if (!std::isfinite(count) ||
            count >= static_cast<double>(maximum)) {
            return maximum;
        }
        return std::max<std::size_t>(1U, static_cast<std::size_t>(count));
    }

    // nx / ny ~= width / height and nx * ny ~= desired_cell_count.
    // Float-coordinate extents and their ratio fit comfortably in double.
    const double ideal =
        std::sqrt(desired_cell_count * (this_extent / other_extent));
    if (!std::isfinite(ideal) || ideal >= static_cast<double>(maximum)) {
        return maximum;
    }
    if (ideal <= 1.0) {
        return 1U;
    }
    return static_cast<std::size_t>(std::ceil(ideal));
}

bool product_exceeds(std::size_t first,
                     std::size_t second,
                     std::size_t limit) noexcept {
    return second != 0U && first > limit / second;
}

struct QueryScratch {
    std::vector<std::uint64_t> marks;
    std::uint64_t generation = 0;

    void ensure_size(std::size_t size) {
        if (marks.size() < size) {
            marks.resize(size, 0U);
        }
    }

    std::uint64_t next_generation() {
        if (generation == std::numeric_limits<std::uint64_t>::max()) {
            std::fill(marks.begin(), marks.end(), 0U);
            generation = 1U;
        } else {
            ++generation;
        }
        return generation;
    }
};

QueryScratch& query_scratch() {
    // Per-thread scratch keeps const queries data-race-free while avoiding an
    // O(N) clear (and normally an allocation) on every query.
    thread_local QueryScratch scratch;
    return scratch;
}

class UniformGridIndex final : public CollisionIndex {
public:
    UniformGridIndex(const Outline& first,
                     const Outline& second,
                     GridOptions options) {
        const std::size_t outline_edge_count = first.size() + second.size();
        segments_.reserve(outline_edge_count);
        append_outline(first, segments_, domain_);
        append_outline(second, segments_, domain_);

        const std::size_t maximum_axis_cells =
            std::max<std::size_t>(1U, options.max_axis_cells);
        double cells_per_edge = options.target_cells_per_edge;
        if (!(cells_per_edge > 0.0) || std::isnan(cells_per_edge)) {
            cells_per_edge = 1.0;
        }

        const double maximum_axis_as_double =
            static_cast<double>(maximum_axis_cells);
        const double maximum_total_cells =
            maximum_axis_as_double * maximum_axis_as_double;
        double desired_cell_count =
            cells_per_edge * static_cast<double>(segments_.size());
        if (!std::isfinite(desired_cell_count)) {
            desired_cell_count = maximum_total_cells;
        }
        desired_cell_count =
            std::clamp(desired_cell_count, 1.0, maximum_total_cells);

        domain_min_x_ = binary32_to_double_exact(domain_.min_x);
        domain_min_y_ = binary32_to_double_exact(domain_.min_y);
        extent_x_ = binary32_to_double_exact(domain_.max_x) - domain_min_x_;
        extent_y_ = binary32_to_double_exact(domain_.max_y) - domain_min_y_;

        x_cell_count_ = choose_axis_count(desired_cell_count,
                                         extent_x_,
                                         extent_y_,
                                         maximum_axis_cells);
        y_cell_count_ = choose_axis_count(desired_cell_count,
                                         extent_y_,
                                         extent_x_,
                                         maximum_axis_cells);

        if (product_exceeds(x_cell_count_,
                            y_cell_count_,
                            std::numeric_limits<std::size_t>::max())) {
            throw std::length_error("uniform-grid cell count overflows size_t");
        }
        const std::size_t cell_count = x_cell_count_ * y_cell_count_;
        if (cell_count == std::numeric_limits<std::size_t>::max() ||
            cell_count + 1U > cell_offsets_.max_size()) {
            throw std::length_error("uniform-grid cell count is too large");
        }

        // Counts are accumulated at index cell+1 so the same allocation can
        // become the CSR offset array after a prefix sum.
        cell_offsets_.assign(cell_count + 1U, 0U);

        for (std::size_t edge_id = 0; edge_id < segments_.size(); ++edge_id) {
            const CellRange range = cell_range_unchecked(segments_[edge_id].bounds);
            const std::size_t width = range.max_x - range.min_x + 1U;
            const std::size_t height = range.max_y - range.min_y + 1U;
            if (product_exceeds(width,
                                height,
                                options.max_cells_per_edge)) {
                overflow_edge_ids_.push_back(edge_id);
                continue;
            }

            for (std::size_t y = range.min_y; y <= range.max_y; ++y) {
                const std::size_t row = y * x_cell_count_;
                for (std::size_t x = range.min_x; x <= range.max_x; ++x) {
                    const std::size_t cell = row + x;
                    if (cell_offsets_[cell + 1U] ==
                        std::numeric_limits<std::size_t>::max()) {
                        throw std::length_error(
                            "uniform-grid reference count overflows size_t");
                    }
                    ++cell_offsets_[cell + 1U];
                }
            }
        }

        for (std::size_t cell = 1; cell < cell_offsets_.size(); ++cell) {
            if (cell_offsets_[cell] >
                std::numeric_limits<std::size_t>::max() -
                    cell_offsets_[cell - 1U]) {
                throw std::length_error(
                    "uniform-grid reference count overflows size_t");
            }
            cell_offsets_[cell] += cell_offsets_[cell - 1U];
        }
        if (cell_offsets_.back() > cell_edge_ids_.max_size()) {
            throw std::length_error("uniform-grid reference array is too large");
        }
        cell_edge_ids_.resize(cell_offsets_.back());

        std::vector<std::size_t> write_offsets(cell_offsets_.begin(),
                                               cell_offsets_.end() - 1);
        for (std::size_t edge_id = 0; edge_id < segments_.size(); ++edge_id) {
            const CellRange range = cell_range_unchecked(segments_[edge_id].bounds);
            const std::size_t width = range.max_x - range.min_x + 1U;
            const std::size_t height = range.max_y - range.min_y + 1U;
            if (product_exceeds(width,
                                height,
                                options.max_cells_per_edge)) {
                continue;
            }

            for (std::size_t y = range.min_y; y <= range.max_y; ++y) {
                const std::size_t row = y * x_cell_count_;
                for (std::size_t x = range.min_x; x <= range.max_x; ++x) {
                    const std::size_t cell = row + x;
                    cell_edge_ids_[write_offsets[cell]++] = edge_id;
                }
            }
        }
    }

    [[nodiscard]] std::string_view name() const noexcept override {
        return "UniformGridAdaptiveExact";
    }

    [[nodiscard]] bool intersects(const QueryPerimeter& query,
                                  QueryStats* stats) const override {
        QueryScratch& scratch = query_scratch();
        scratch.ensure_size(segments_.size());

        for (std::size_t query_edge = 0; query_edge < 4U; ++query_edge) {
            const Point a = query.vertices[query_edge];
            const Point b = query.vertices[(query_edge + 1U) % 4U];
            const Aabb query_bounds = make_aabb(a, b);

            CellRange range{};
            if (!clipped_cell_range(query_bounds, range)) {
                continue;
            }

            const std::uint64_t generation = scratch.next_generation();
            const auto test_edge = [&](std::size_t edge_id) {
                if (scratch.marks[edge_id] == generation) {
                    if (stats != nullptr) {
                        ++stats->duplicate_references;
                    }
                    return false;
                }
                scratch.marks[edge_id] = generation;

                const StoredSegment& segment = segments_[edge_id];
                if (stats != nullptr) {
                    ++stats->broad_phase_tests;
                }
                if (!overlaps(query_bounds, segment.bounds)) {
                    return false;
                }
                if (stats != nullptr) {
                    ++stats->candidate_segment_tests;
                }
                return segments_intersect(a,
                                          b,
                                          segment.a,
                                          segment.b,
                                          PredicatePolicy::AdaptiveExact,
                                          stats);
            };

            // Overflow edges are deliberately absent from all cells.
            for (const std::size_t edge_id : overflow_edge_ids_) {
                if (test_edge(edge_id)) {
                    return true;
                }
            }

            for (std::size_t y = range.min_y; y <= range.max_y; ++y) {
                const std::size_t row = y * x_cell_count_;
                for (std::size_t x = range.min_x; x <= range.max_x; ++x) {
                    const std::size_t cell = row + x;
                    if (stats != nullptr) {
                        ++stats->cells_visited;
                    }
                    for (std::size_t offset = cell_offsets_[cell];
                         offset < cell_offsets_[cell + 1U];
                         ++offset) {
                        if (test_edge(cell_edge_ids_[offset])) {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    [[nodiscard]] IndexMetrics metrics() const noexcept override {
        return {
            segments_.size(),
            sizeof(*this) +
                segments_.capacity() * sizeof(StoredSegment) +
                cell_offsets_.capacity() * sizeof(std::size_t) +
                cell_edge_ids_.capacity() * sizeof(std::size_t) +
                overflow_edge_ids_.capacity() * sizeof(std::size_t),
            x_cell_count_ * y_cell_count_,
            cell_edge_ids_.size() + overflow_edge_ids_.size(),
            overflow_edge_ids_.size(),
        };
    }

private:
    struct CellRange {
        std::size_t min_x = 0;
        std::size_t min_y = 0;
        std::size_t max_x = 0;
        std::size_t max_y = 0;
    };

    [[nodiscard]] std::size_t map_x(float coordinate) const noexcept {
        return map_axis(binary32_to_double_exact(coordinate),
                        domain_min_x_,
                        extent_x_,
                        x_cell_count_);
    }

    [[nodiscard]] std::size_t map_y(float coordinate) const noexcept {
        return map_axis(binary32_to_double_exact(coordinate),
                        domain_min_y_,
                        extent_y_,
                        y_cell_count_);
    }

    [[nodiscard]] static std::size_t map_axis(double coordinate,
                                              double minimum,
                                              double extent,
                                              std::size_t cell_count) noexcept {
        if (cell_count <= 1U || extent <= 0.0) {
            return 0U;
        }

        // Divide before multiplying: for finite float inputs the ratio is in
        // [0,1] after clipping, so neither this calculation nor its integer
        // conversion can overflow.
        const double normalized = (coordinate - minimum) / extent;
        if (normalized <= 0.0) {
            return 0U;
        }
        if (normalized >= 1.0) {
            return cell_count - 1U;
        }
        const double scaled = normalized * static_cast<double>(cell_count);
        if (scaled >= static_cast<double>(cell_count - 1U)) {
            return cell_count - 1U;
        }
        return static_cast<std::size_t>(scaled);
    }

    [[nodiscard]] CellRange cell_range_unchecked(const Aabb& bounds) const
        noexcept {
        return {
            map_x(bounds.min_x),
            map_y(bounds.min_y),
            map_x(bounds.max_x),
            map_y(bounds.max_y),
        };
    }

    [[nodiscard]] bool clipped_cell_range(const Aabb& bounds,
                                          CellRange& result) const noexcept {
        if (!overlaps(bounds, domain_)) {
            return false;
        }

        const Aabb clipped = {
            exact_max(bounds.min_x, domain_.min_x),
            exact_max(bounds.min_y, domain_.min_y),
            exact_min(bounds.max_x, domain_.max_x),
            exact_min(bounds.max_y, domain_.max_y),
        };
        result = cell_range_unchecked(clipped);
        return true;
    }

    std::vector<StoredSegment> segments_;
    std::vector<std::size_t> cell_offsets_;
    std::vector<std::size_t> cell_edge_ids_;
    std::vector<std::size_t> overflow_edge_ids_;
    Aabb domain_;
    double domain_min_x_ = 0.0;
    double domain_min_y_ = 0.0;
    double extent_x_ = 0.0;
    double extent_y_ = 0.0;
    std::size_t x_cell_count_ = 1U;
    std::size_t y_cell_count_ = 1U;
};

}  // namespace

std::unique_ptr<CollisionIndex> make_uniform_grid_index(
    const Outline& first,
    const Outline& second,
    GridOptions options) {
    return std::make_unique<UniformGridIndex>(first, second, options);
}

}  // namespace collision
