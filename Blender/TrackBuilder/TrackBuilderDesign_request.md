Write a Python script for Blender 4.5.12 that does the following the below.
Use InputSample.blend as an example input. Do not modify it.
Generate several blend files with example inputs that use the script to generate outputs.

# Track Builder design

## Inputs

- A .blend file
- A collection named "Input"
- The collection contains 1 or more "outline objects"
- An outline object is:
  - A mesh or curve object
  - Purely two dimensional (all Z-coordinates should be zero)
  - Contains no faces
  - Is a closed loop of edges
  - Does not intersect itself or any other outline objects
- There are 3 types of outline objects:
  - Ground outline: Required, exactly one
  - Outer track outline: Required, exactly one
  - Inner track outline: Optional, zero or more
- The collection must contain exactly one "outer outline object"
- The collection contains 0 or more "inner outline objects" that are enclosed by the "outer outline object"
- The input outline objects should contain exactly 1 or 2 levels of nesting
  - Level 1: The Ground outline should enclose the outer track outline
  - Level 2: If there are inner track outlines enclosed in the outer track outline
- each outline object must have exactly one material assigned to it

## What the script does

Given this input, the script must generate flat meshes that represent the ground, track and enclosed
islands, and rectangular cube meshes that form the inner and outer barriers of the track.

The outline meshes must be properly triangulated.
Each outline must be filled in properly, meaning that there are no overlapping faces and no faces that cross the outline boundary.
Each outline mesh should be assigned the material that came from its outline input object.

Each output object must be placed in a collection called "Output".

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
- They perfect rectangular prisms with a Z-height of exactly H, which must be a script input
- For each outer track edge, create rectangular prism that follows the edge exactly, then extrudes into the ground away from the track by script input of W(idth) meters, and then extrudes a Z-height of H
- For each inner track edge, create rectangular prism that follows the edge exactly, then extrudes into island mesh away from the track by script input of W(idth) meters, and then extrudes a Z-height of H
- Ensure the following for each barrier mesh:
  - Origin position is the center of the geometry, but with Z-height of 0
  - face Normals are all properly pointing outwards
  - object local z-axis matches the global z-axis
  - object local y-axis is perfectly aligned with the object faces and points away from the track mesh and perpendicular to the track edge
  - object local y-axis is perfectly aligned with the object faces and points parallel along the track edge and in a clockwise fashion
