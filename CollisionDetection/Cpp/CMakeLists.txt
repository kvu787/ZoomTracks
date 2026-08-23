cmake_minimum_required(VERSION 3.20)
project(outline_rectangle_collision LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_library(collision STATIC
    src/exact_predicates.cpp
    src/linear_index.cpp
    src/bvh_index.cpp
    src/grid_index.cpp
)
target_include_directories(collision PUBLIC include)

if(MSVC)
    target_compile_options(collision PRIVATE /W4 /permissive- /fp:strict)
else()
    target_compile_options(collision PRIVATE
        -Wall -Wextra -Wpedantic
        -fno-fast-math -ffp-contract=off -frounding-math)
endif()

add_executable(collision_tests tests/test_collision.cpp)
target_link_libraries(collision_tests PRIVATE collision)

add_executable(collision_benchmark bench/benchmark.cpp)
target_link_libraries(collision_benchmark PRIVATE collision)

enable_testing()
add_test(NAME collision_tests COMMAND collision_tests)
