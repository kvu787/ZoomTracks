Write a Python script for Blender 4.5.12 that does the following the below.
Use InputSample.blend as an example input. Do not modify it.
Generate several blend files with example inputs that use the script to generate outputs.

# Track Builder design

CW/CCW meaning:
Viewed from global +Z looking toward the XY plane

## Script inputs

W and H are input params to the script and must be finite and greater than 0.
W can be "oversized" - that is fine. do not try validate this.

## Inputs

- A .blend file
- A collection named "Input"
- The collection recursively contains 2 or more "outline objects"
- An outline object is:
  - A mesh or curve object
    - for curve objects the outline is equivalent to evaluating the curve at its provided resolution (ex: `bpy.data.curves["NurbsCurve"].splines[0].resolution_u`)
  - Purely two dimensional (all Z-coordinates must be zero)
  - Contains no faces
  - Is a closed loop of edges
  - Does not intersect itself or any other outline objects
- There are 3 types of outline objects:
  - Ground outline: Required, exactly one
  - Outer track outline: Required, exactly one
  - Inner track outline: Optional, zero or more
- The script must use the geometry of the outline objects to classify each one as ground, outer, or inner
- The input outline objects must contain exactly 1 or 2 levels of nesting
  - Level 1: The Ground outline must enclose the outer track outline
  - Level 2: If there are inner track outlines enclosed in the outer track outline
- each outline object must have exactly one material assigned to it

### Evaluation

The input outlines must be converted to dependency-graph-evaluated geometry, including modifiers.

### Coordinate space

All inputs must be evaluated into world space prior to validation.
The resulting world space meshes must have all vertices be Z=0.

### Validation rules

All inputs and parameters must be validated before deleting an existing Output collection. This preserves the last valid output when new input is invalid.

All objects (recursively) in Input collection must satisfy the "outline object" definition
Exactly one connected loop per object
No loose vertices, zero-length edges, branches, or duplicate consecutive points
Exactly one cyclic spline for curves
No touching boundaries
No inner outline nested inside another inner outline
A small floating-point tolerance for Z, intersections, and containment

## What the script does

Given this input, the script must generate flat meshes (called "fill meshes") that represent the ground, track and enclosed
islands, and rectangular prism meshes that form the inner and outer barriers of the track.

The fill meshes must be properly triangulated.
Each fill must be filled in properly, meaning that there are no overlapping faces and no faces that cross the outline boundary.
Each fill mesh must be assigned the material that came from its outline input object.

Each output object must be placed in a collection called "Output".

All ground, track, and island triangle normals to face global +Z

Other notes:
- The Input collection and objects must not be modified
- Write code that throws descriptive exceptions that validates input preconditions
- Each time the script is run, delete the entire Output collection if it exists, so it can be rebuilt based on the input

## Outputs

Ground mesh:
- This is a flat mesh that fills in the ground outline but excludes the outer and inner track outlines

Track mesh:
- This is a flat mesh that fills in the outer track outline but excludes the inner track outlines

Island meshes
- These are flat meshes that fill in the inner track outlines

Barrier meshes:
- Do not assign a material to the barrier meshes
  - If blender assigns a default material to the barrier meshes, that's fine
- Unlike everything else, these are not pure 2D
- They are perfect rectangular prisms with a Z-height of exactly H, which must be a script input
- For each outer track edge, create rectangular prism that follows the edge exactly, then extrudes into the ground away from the track by script input of W(idth) meters, and then extrudes a Z-height of H
- For each inner track edge, create rectangular prism that follows the edge exactly, then extrudes into island mesh away from the track by script input of W(idth) meters, and then extrudes a Z-height of H
- Ensure the following for each barrier mesh:
  - Origin position is the center of the geometry, but with Z-height of 0
  - face Normals are all properly pointing outwards
  - object local z-axis matches the global z-axis
  - object local y-axis is perfectly aligned with the object faces and points away from the track mesh and perpendicular to the track edge
  - object local x-axis is perfectly aligned with the object faces and points parallel along the track edge
    - this should result in the local x-axes pointing in a clockwise fashion for the outer track outline and CCW fashion for inner track outline
- The barriers may overlap each other, which is fine
- The base of each barrier object lies on global Z = 0, and the object extends to global Z = H

Barrier corner filler meshes:
- Do not assign a material to the these meshes
  - If blender assigns a default material to the barrier meshes, that's fine
- There may be triangular "gaps" between barriers:
  - The script must create separate mesh objects to fill these in exactly
- separate triangular prisms spanning Z = 0...H
- outward normals
- created only where adjacent edge prisms leave an uncovered region.
- Origin position is the center of the geometry, but with Z-height of 0
- Local axes
  - Local Z matches global Z
  - Local Y points from the filler’s XY centroid toward the shared original outline vertex that produced the corner.
