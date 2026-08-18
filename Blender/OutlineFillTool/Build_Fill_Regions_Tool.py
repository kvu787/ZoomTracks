"""Build or synchronize a live planar-fill tool for any number of outlines.

Run this file from Blender's Scripting workspace, or from a command line:

    blender --background input.blend --python Build_Fill_Regions_Tool.py

The Geometry Nodes group has two inputs: a target outline object and a collection
containing every outline. One generated mesh object is created per collection member.
Re-run this script after adding or removing objects from ``LIVE FILL INPUTS`` to sync
the output-object count. Shape edits and transforms update live without re-running it.
"""

from pathlib import Path
import colorsys

import bpy


NODE_GROUP_NAME = "Live Planar Fill - Outline Collection"
INPUT_COLLECTION_NAME = "LIVE FILL INPUTS"
OUTPUT_COLLECTION_NAME = "LIVE FILL OUTPUTS"
MODIFIER_NAME = "Live Planar Fill"
README_TEXT_NAME = "README - Live Planar Fill"
BUILDER_TEXT_NAME = "Build Live Planar Fill Tool.py"


def new_node(nodes, node_type, name, label, x, y, width=180):
    node = nodes.new(node_type)
    node.name = name
    node.label = label
    node.location = (x, y)
    node.width = width
    return node


def socket_by_identifier(sockets, identifier):
    for socket in sockets:
        if socket.identifier == identifier:
            return socket
    raise KeyError(identifier)


def remove_old_outputs():
    collection = bpy.data.collections.get(OUTPUT_COLLECTION_NAME)
    if collection is None:
        return
    for obj in list(collection.objects):
        data = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)
    bpy.data.collections.remove(collection)
    # Clean output meshes orphaned by earlier versions of this builder.
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0 and mesh.name.startswith("Filled "):
            bpy.data.meshes.remove(mesh)


def supported_outline_objects(objects):
    return [obj for obj in objects if obj.type in {"CURVE", "MESH"}]


def get_or_create_input_collection():
    collection = bpy.data.collections.get(INPUT_COLLECTION_NAME)
    if collection is not None:
        sources = supported_outline_objects(collection.objects)
        if not sources:
            raise RuntimeError(f"'{INPUT_COLLECTION_NAME}' contains no Curve or Mesh outlines")
        return collection, sorted(sources, key=lambda obj: obj.name.casefold())

    output_collection = bpy.data.collections.get(OUTPUT_COLLECTION_NAME)
    output_objects = set(output_collection.objects) if output_collection else set()
    sources = supported_outline_objects(
        obj for obj in bpy.context.scene.objects if obj not in output_objects
    )
    if not sources:
        raise RuntimeError("No Curve or Mesh outline objects were found")

    collection = bpy.data.collections.new(INPUT_COLLECTION_NAME)
    bpy.context.scene.collection.children.link(collection)
    collection.color_tag = "COLOR_05"
    for obj in sources:
        if obj.name not in collection.objects:
            collection.objects.link(obj)
    return collection, sorted(sources, key=lambda obj: obj.name.casefold())


def normalize_and_fill(nodes, links, geometry_socket, prefix, x, y):
    """Accept a Curve or mesh boundary and return one filled triangle mesh."""
    separate = new_node(
        nodes,
        "GeometryNodeSeparateComponents",
        f"{prefix} Separate Components",
        "ACCEPT CURVE OR MESH",
        x,
        y,
        185,
    )
    links.new(geometry_socket, separate.inputs["Geometry"])

    edge_neighbors = new_node(
        nodes,
        "GeometryNodeInputMeshEdgeNeighbors",
        f"{prefix} Edge Face Count",
        "BOUNDARY / LOOSE EDGES",
        x,
        y - 190,
        175,
    )
    boundary_compare = new_node(
        nodes,
        "FunctionNodeCompare",
        f"{prefix} Boundary Edge Test",
        "FACE COUNT < 2",
        x + 210,
        y - 175,
        155,
    )
    boundary_compare.data_type = "INT"
    boundary_compare.operation = "LESS_THAN"
    socket_by_identifier(boundary_compare.inputs, "B_INT").default_value = 2
    links.new(
        edge_neighbors.outputs["Face Count"],
        socket_by_identifier(boundary_compare.inputs, "A_INT"),
    )

    mesh_to_curve = new_node(
        nodes,
        "GeometryNodeMeshToCurve",
        f"{prefix} Mesh to Curve",
        "MESH BOUNDARY TO CURVE",
        x + 210,
        y,
        175,
    )
    links.new(separate.outputs["Mesh"], mesh_to_curve.inputs["Mesh"])
    links.new(boundary_compare.outputs["Result"], mesh_to_curve.inputs["Selection"])

    join_curves = new_node(
        nodes,
        "GeometryNodeJoinGeometry",
        f"{prefix} Normalize Curve",
        "NORMALIZED CURVE",
        x + 420,
        y,
        165,
    )
    links.new(separate.outputs["Curve"], join_curves.inputs["Geometry"])
    links.new(mesh_to_curve.outputs["Curve"], join_curves.inputs["Geometry"])

    fill = new_node(
        nodes,
        "GeometryNodeFillCurve",
        f"{prefix} Fill",
        "FILL OUTLINE",
        x + 620,
        y,
        165,
    )
    fill.mode = "TRIANGLES"
    links.new(join_curves.outputs["Geometry"], fill.inputs["Curve"])
    return fill.outputs["Mesh"]


def measure_area(nodes, links, mesh_socket, prefix, x, y):
    face_area = new_node(
        nodes,
        "GeometryNodeInputMeshFaceArea",
        f"{prefix} Face Area",
        "FACE AREA",
        x,
        y - 95,
        130,
    )
    statistic = new_node(
        nodes,
        "GeometryNodeAttributeStatistic",
        f"{prefix} Total Area",
        "TOTAL AREA",
        x + 165,
        y,
        175,
    )
    statistic.data_type = "FLOAT"
    statistic.domain = "FACE"
    statistic.inputs["Selection"].default_value = True
    links.new(mesh_socket, statistic.inputs["Geometry"])
    links.new(face_area.outputs["Area"], statistic.inputs["Attribute"])
    return statistic.outputs["Sum"]


def make_closed_solid(nodes, links, mesh_socket, prefix, x, y, bottom_z, height):
    """Create a temporary watertight solid from a flat fill."""
    lower = new_node(
        nodes,
        "GeometryNodeTransform",
        f"{prefix} Lower",
        f"LOWER TO Z={bottom_z:g}",
        x,
        y,
        155,
    )
    lower.inputs["Translation"].default_value = (0.0, 0.0, bottom_z)
    links.new(mesh_socket, lower.inputs["Geometry"])

    extrude = new_node(
        nodes,
        "GeometryNodeExtrudeMesh",
        f"{prefix} Extrude",
        "EXTRUDE TEMPORARY SOLID",
        x + 185,
        y,
        175,
    )
    extrude.mode = "FACES"
    extrude.inputs["Selection"].default_value = True
    extrude.inputs["Offset Scale"].default_value = height
    extrude.inputs["Individual"].default_value = False
    links.new(lower.outputs["Geometry"], extrude.inputs["Mesh"])

    bottom = new_node(
        nodes,
        "GeometryNodeFlipFaces",
        f"{prefix} Bottom Cap",
        "BOTTOM CAP",
        x + 185,
        y - 125,
        155,
    )
    links.new(lower.outputs["Geometry"], bottom.inputs["Mesh"])

    join = new_node(
        nodes,
        "GeometryNodeJoinGeometry",
        f"{prefix} Close",
        "CLOSE SOLID",
        x + 395,
        y,
        155,
    )
    links.new(extrude.outputs["Mesh"], join.inputs["Geometry"])
    links.new(bottom.outputs["Mesh"], join.inputs["Geometry"])

    merge = new_node(
        nodes,
        "GeometryNodeMergeByDistance",
        f"{prefix} Weld",
        "WELD SOLID",
        x + 580,
        y,
        155,
    )
    merge.inputs["Selection"].default_value = True
    merge.inputs["Distance"].default_value = 0.00001
    links.new(join.outputs["Geometry"], merge.inputs["Geometry"])
    return merge.outputs["Geometry"]


def build_node_group():
    old = bpy.data.node_groups.get(NODE_GROUP_NAME)
    if old is not None:
        bpy.data.node_groups.remove(old, do_unlink=True)

    # Remove the obsolete fixed-three node group from the prior version if present.
    obsolete = bpy.data.node_groups.get("Live Planar Fill - 3 Outlines")
    if obsolete is not None and obsolete.users == 0:
        bpy.data.node_groups.remove(obsolete)

    group = bpy.data.node_groups.new(NODE_GROUP_NAME, "GeometryNodeTree")
    group.description = (
        "Fill one target outline and subtract every smaller outline from an arbitrary-size collection"
    )
    group.color_tag = "GEOMETRY"
    group.is_modifier = True

    output_socket = group.interface.new_socket(
        name="Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry"
    )
    output_socket.attribute_domain = "POINT"
    target_socket = group.interface.new_socket(
        name="Target Outline", in_out="INPUT", socket_type="NodeSocketObject"
    )
    target_socket.description = "The outline represented by this output object"
    collection_socket = group.interface.new_socket(
        name="Outline Collection", in_out="INPUT", socket_type="NodeSocketCollection"
    )
    collection_socket.description = "Collection containing every closed planar outline"

    nodes = group.nodes
    links = group.links
    group_input = new_node(nodes, "NodeGroupInput", "Inputs", "ARBITRARY-SIZE INPUT", -1700, 320, 210)
    group_output = new_node(nodes, "NodeGroupOutput", "Output", "FLAT TARGET MESH", 1580, 300, 190)
    group_output.is_active_output = True

    target_info = new_node(
        nodes,
        "GeometryNodeObjectInfo",
        "Target Object",
        "TARGET OUTLINE",
        -1460,
        610,
        175,
    )
    target_info.transform_space = "RELATIVE"
    target_info.inputs["As Instance"].default_value = False
    links.new(group_input.outputs["Target Outline"], target_info.inputs["Object"])
    target_fill = normalize_and_fill(
        nodes, links, target_info.outputs["Geometry"], "Target", -1230, 610
    )
    target_area = measure_area(nodes, links, target_fill, "Target", -390, 455)
    target_solid = make_closed_solid(
        nodes, links, target_fill, "Target", -370, 710, -0.5, 1.0
    )

    collection_info = new_node(
        nodes,
        "GeometryNodeCollectionInfo",
        "Outline Collection",
        "ALL OUTLINES AS INSTANCES",
        -1460,
        -250,
        205,
    )
    collection_info.transform_space = "RELATIVE"
    collection_info.inputs["Separate Children"].default_value = True
    collection_info.inputs["Reset Children"].default_value = False
    links.new(group_input.outputs["Outline Collection"], collection_info.inputs["Collection"])

    foreach_input = new_node(
        nodes,
        "GeometryNodeForeachGeometryElementInput",
        "For Each Outline Input",
        "FOR EACH OUTLINE",
        -1190,
        -250,
        180,
    )
    foreach_output = new_node(
        nodes,
        "GeometryNodeForeachGeometryElementOutput",
        "For Each Outline Output",
        "JOIN SMALLER CUTTERS",
        540,
        -250,
        190,
    )
    foreach_input.pair_with_output(foreach_output)
    foreach_output.domain = "INSTANCE"
    foreach_input.inputs["Selection"].default_value = True
    links.new(collection_info.outputs["Instances"], foreach_input.inputs["Geometry"])

    realize = new_node(
        nodes,
        "GeometryNodeRealizeInstances",
        "Realize Current Outline",
        "CURRENT OUTLINE GEOMETRY",
        -970,
        -250,
        190,
    )
    realize.inputs["Selection"].default_value = True
    realize.inputs["Realize All"].default_value = True
    links.new(foreach_input.outputs["Element"], realize.inputs["Geometry"])
    candidate_fill = normalize_and_fill(
        nodes, links, realize.outputs["Geometry"], "Candidate", -750, -250
    )
    candidate_area = measure_area(nodes, links, candidate_fill, "Candidate", 80, -405)
    candidate_solid = make_closed_solid(
        nodes, links, candidate_fill, "Candidate", 40, -125, -1.0, 2.0
    )

    smaller = new_node(
        nodes,
        "FunctionNodeCompare",
        "Candidate Is Smaller",
        "CANDIDATE AREA < TARGET AREA",
        265,
        -485,
        205,
    )
    smaller.data_type = "FLOAT"
    smaller.operation = "LESS_THAN"
    links.new(candidate_area, socket_by_identifier(smaller.inputs, "A"))
    links.new(target_area, socket_by_identifier(smaller.inputs, "B"))

    use_cutter = new_node(
        nodes,
        "GeometryNodeSwitch",
        "Use Smaller Cutter",
        "SKIP TARGET / CONTAINERS",
        445,
        -465,
        190,
    )
    use_cutter.input_type = "GEOMETRY"
    links.new(smaller.outputs["Result"], use_cutter.inputs["Switch"])
    links.new(candidate_solid, use_cutter.inputs["True"])
    # The default generation item joins the generated geometry across all iterations.
    links.new(use_cutter.outputs["Output"], foreach_output.inputs[1])

    boolean = new_node(
        nodes,
        "GeometryNodeMeshBoolean",
        "Subtract All Smaller Outlines",
        "EXACT DIFFERENCE",
        790,
        560,
        215,
    )
    boolean.operation = "DIFFERENCE"
    boolean.solver = "EXACT"
    boolean.inputs["Self Intersection"].default_value = True
    boolean.inputs["Hole Tolerant"].default_value = True
    links.new(target_solid, boolean.inputs["Mesh 1"])
    # Output 0 is the zone's main pass-through geometry. Generation_0 is the
    # geometry accumulated from the per-outline generation item above.
    links.new(foreach_output.outputs["Generation_0"], boolean.inputs["Mesh 2"])

    position = new_node(
        nodes,
        "GeometryNodeInputPosition",
        "Boolean Position",
        "POSITION",
        790,
        310,
        125,
    )
    position_z = new_node(
        nodes,
        "ShaderNodeSeparateXYZ",
        "Boolean Position Z",
        "POSITION Z",
        945,
        310,
        125,
    )
    links.new(position.outputs["Position"], position_z.inputs["Vector"])
    top_test = new_node(
        nodes,
        "FunctionNodeCompare",
        "Top Face Test",
        "Z > 0.25",
        1095,
        310,
        145,
    )
    top_test.data_type = "FLOAT"
    top_test.operation = "GREATER_THAN"
    socket_by_identifier(top_test.inputs, "B").default_value = 0.25
    links.new(position_z.outputs["Z"], socket_by_identifier(top_test.inputs, "A"))

    keep_top = new_node(
        nodes,
        "GeometryNodeSeparateGeometry",
        "Keep Top Surface",
        "KEEP FLAT TOP",
        1050,
        560,
        170,
    )
    keep_top.domain = "FACE"
    links.new(boolean.outputs["Mesh"], keep_top.inputs["Geometry"])
    links.new(top_test.outputs["Result"], keep_top.inputs["Selection"])

    flatten = new_node(
        nodes,
        "GeometryNodeTransform",
        "Return To Plane",
        "RETURN TO Z=0",
        1290,
        560,
        165,
    )
    flatten.inputs["Translation"].default_value = (0.0, 0.0, -0.5)
    links.new(keep_top.outputs["Selection"], flatten.inputs["Geometry"])
    links.new(flatten.outputs["Geometry"], group_output.inputs["Geometry"])
    return group


def material_color(index):
    hue = (0.58 + index * 0.61803398875) % 1.0
    red, green, blue = colorsys.hsv_to_rgb(hue, 0.72, 0.88)
    return red, green, blue, 0.72


def create_material(name, color):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = color
    material.roughness = 0.7
    return material


def create_output_objects(group, input_collection, sources):
    collection = bpy.data.collections.new(OUTPUT_COLLECTION_NAME)
    bpy.context.scene.collection.children.link(collection)
    collection.color_tag = "COLOR_04"
    input_sockets = {
        item.name: item
        for item in group.interface.items_tree
        if getattr(item, "item_type", None) == "SOCKET" and item.in_out == "INPUT"
    }

    outputs = []
    for index, source in enumerate(sources):
        mesh = bpy.data.meshes.new(f"Filled {source.name} Mesh")
        obj = bpy.data.objects.new(f"Filled - {source.name}", mesh)
        collection.objects.link(obj)
        obj["Live Fill Result For"] = source.name
        obj["Instructions"] = (
            f"Uses every outline in {INPUT_COLLECTION_NAME}; edit source shapes for live updates."
        )
        color = material_color(index)
        obj.color = color
        obj.show_name = True
        mesh.materials.append(create_material(f"Live Fill - {source.name}", color))

        modifier = obj.modifiers.new(MODIFIER_NAME, "NODES")
        modifier.node_group = group
        modifier[input_sockets["Target Outline"].identifier] = source
        modifier[input_sockets["Outline Collection"].identifier] = input_collection
        outputs.append(obj)
    return outputs


def add_documentation(input_collection, sources):
    readme = bpy.data.texts.get(README_TEXT_NAME) or bpy.data.texts.new(README_TEXT_NAME)
    readme.clear()
    readme.write(
        "LIVE PLANAR FILL - ARBITRARY OUTLINE COUNT\n"
        "==========================================\n\n"
        f"Input collection: {INPUT_COLLECTION_NAME}\n"
        f"Current outline count: {len(sources)}\n\n"
        "Edit any source control point, vertex, or object transform and all affected\n"
        "mesh outputs update immediately. Each output is its target outline's fill,\n"
        "minus every smaller outline geometrically inside it. Smaller disjoint outlines\n"
        "do not affect it. Curves and mesh-edge outlines can be mixed.\n\n"
        "CHANGING THE NUMBER OF OUTLINES\n"
        "--------------------------------\n"
        f"1. Link or move any number of closed outline objects into '{INPUT_COLLECTION_NAME}'.\n"
        "2. Re-run the embedded 'Build Live Planar Fill Tool.py' text block.\n"
        "   This synchronizes one output Mesh object per input.\n\n"
        "You can also duplicate an existing output object and change its modifier's\n"
        "Target Outline field; the Outline Collection input has no fixed object limit.\n\n"
        "CONSTRAINTS\n"
        "-----------\n"
        "All source loops must be closed, non-self-intersecting, mutually non-crossing,\n"
        "and planar at Z=0. Convex and concave shapes and arbitrary nesting are supported.\n\n"
        "Current inputs:\n"
        + "\n".join(f"  - {obj.name}" for obj in sources)
        + "\n"
    )

    source_path = Path(__file__)
    if source_path.exists():
        builder = bpy.data.texts.get(BUILDER_TEXT_NAME) or bpy.data.texts.new(BUILDER_TEXT_NAME)
        builder.clear()
        builder.write(source_path.read_text(encoding="utf-8"))


def configure_scene_for_editing(sources):
    for source in sources:
        source.show_in_front = True
        source.show_name = True
        source.color = (0.02, 0.02, 0.02, 1.0)
        if source.type == "CURVE":
            source.data.fill_mode = "NONE"
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    sources[0].select_set(True)
    bpy.context.view_layer.objects.active = sources[0]


def main():
    input_collection, sources = get_or_create_input_collection()
    remove_old_outputs()
    group = build_node_group()
    outputs = create_output_objects(group, input_collection, sources)
    add_documentation(input_collection, sources)
    configure_scene_for_editing(sources)
    bpy.context.scene["Live Planar Fill Tool"] = (
        f"{len(sources)} inputs in {INPUT_COLLECTION_NAME}; re-run the embedded builder to sync count."
    )
    bpy.context.scene.update_tag()
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
    print("LIVE_FILL_TOOL_SYNCHRONIZED")
    print("Input count:", len(sources))
    print("Inputs:", ", ".join(obj.name for obj in sources))
    print("Outputs:", ", ".join(obj.name for obj in outputs))


if __name__ == "__main__":
    main()
