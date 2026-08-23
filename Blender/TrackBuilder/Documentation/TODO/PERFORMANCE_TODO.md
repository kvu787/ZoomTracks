# TrackBuilder performance TODO

## Reduce barrier object and mesh counts

Explore batching barrier material segments instead of creating one Blender
Object and Mesh per segment. Any design should preserve Unity's ability to give
each barrier segment its own `BoxCollider`; likely options include one visual
mesh per loop plus lightweight collider proxies or importer-generated colliders.

Profile both ordinary tracks and tracks with thousands of segments before
choosing a batching design.

## Add dependency-aware caching

Start with a whole-build fingerprint that can skip an unchanged build. If finer
invalidation proves useful, consider caching:

- normally evaluated contact geometry and classification;
- fill triangulation;
- dense smooth-curve evaluation;
- width-dependent dense offsets;
- adaptive offset selection;
- material segmentation; and
- height-only extrusion.

Include curve control data, handles, weights, resolution, transforms, supported
evaluation state, materials, numeric parameters, and the Blender version in the
relevant cache keys.

## Reuse generated objects and datablocks

Explore creating replacement meshes transactionally and swapping them into
stable existing objects on repeated builds. Preserve rollback behavior and
handle segment-count changes, object-name collisions, shared data, and links
outside the generated collection.

## Batch temporary curve evaluation

Profile linking all high-resolution temporary curve copies before a single
view-layer update, then evaluating and removing them as a batch. Adopt this only
if the measured savings justify the additional lifecycle and rollback
complexity.
