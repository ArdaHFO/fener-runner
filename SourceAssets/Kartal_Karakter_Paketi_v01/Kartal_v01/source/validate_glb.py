#!/usr/bin/env python3
"""Inspect an exported skinned GLB independently of Blender (standard library only).

Usage: python validate_glb.py asset.glb --report qa.json --expect-clip Run
Exit status 0 means the structural/animation checks passed; it is not a claim
about artistic likeness, animation appeal, or performance on a physical phone.
"""

from __future__ import annotations

import argparse
import base64
import json
import math
from pathlib import Path
import struct
import sys


COMPONENTS = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2),
              5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
COUNTS = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4,
          "MAT2": 4, "MAT3": 9, "MAT4": 16}
IDENTITY = [1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1.]


def multiply(a, b):
    return [sum(a[k * 4 + row] * b[col * 4 + k] for k in range(4))
            for col in range(4) for row in range(4)]


def point(matrix, xyz):
    return [sum(matrix[k * 4 + row] * xyz[k] for k in range(3)) + matrix[12 + row]
            for row in range(3)]


def local_matrix(node):
    if "matrix" in node:
        if len(node["matrix"]) != 16:
            raise ValueError("node matrix must contain 16 numbers")
        return list(node["matrix"])
    x, y, z, w = node.get("rotation", [0, 0, 0, 1])
    sx, sy, sz = node.get("scale", [1, 1, 1])
    tx, ty, tz = node.get("translation", [0, 0, 0])
    return [(1 - 2*y*y - 2*z*z)*sx, (2*x*y + 2*z*w)*sx, (2*x*z - 2*y*w)*sx, 0,
            (2*x*y - 2*z*w)*sy, (1 - 2*x*x - 2*z*z)*sy, (2*y*z + 2*x*w)*sy, 0,
            (2*x*z + 2*y*w)*sz, (2*y*z - 2*x*w)*sz, (1 - 2*x*x - 2*y*y)*sz, 0,
            tx, ty, tz, 1]


class GLB:
    def __init__(self, path):
        self.path = Path(path)
        raw = self.path.read_bytes()
        self.size = len(raw)
        if len(raw) < 20:
            raise ValueError("file is shorter than a GLB header and JSON chunk")
        magic, version, length = struct.unpack_from("<III", raw)
        if magic != 0x46546C67 or version != 2 or length != len(raw):
            raise ValueError("invalid GLB magic, version, or declared length")
        chunks = []
        offset = 12
        while offset < len(raw):
            if offset + 8 > len(raw):
                raise ValueError("truncated GLB chunk header")
            size, kind = struct.unpack_from("<II", raw, offset)
            offset += 8
            if size % 4 or offset + size > len(raw):
                raise ValueError("misaligned or truncated GLB chunk")
            chunks.append((kind, raw[offset:offset + size]))
            offset += size
        if not chunks or chunks[0][0] != 0x4E4F534A:
            raise ValueError("the first GLB chunk is not JSON")
        if sum(kind == 0x4E4F534A for kind, _ in chunks) != 1:
            raise ValueError("GLB contains more than one JSON chunk")
        self.doc = json.loads(chunks[0][1].rstrip(b"\0 \t\r\n"))
        binary = [data for kind, data in chunks if kind == 0x004E4942]
        if len(binary) > 1:
            raise ValueError("GLB contains more than one binary chunk")
        self.buffers = []
        for index, buffer in enumerate(self.doc.get("buffers", [])):
            uri = buffer.get("uri")
            if uri:
                if not uri.startswith("data:"):
                    raise ValueError(f"buffer {index} has an external URI")
                head, payload = uri.split(",", 1)
                if ";base64" not in head:
                    raise ValueError("only base64 embedded buffer URIs are supported")
                data = base64.b64decode(payload, validate=True)
            else:
                if index != 0 or not binary:
                    raise ValueError(f"buffer {index} has no embedded binary data")
                data = binary[0]
            declared = buffer.get("byteLength", -1)
            if declared < 0 or declared > len(data) or len(data) - declared > 3:
                raise ValueError(f"buffer {index} length does not match its data")
            self.buffers.append(data[:declared])
        self.cache = {}

    def read_view(self, index):
        if not isinstance(index, int) or not 0 <= index < len(self.doc.get("bufferViews", [])):
            raise ValueError(f"invalid bufferView index {index}")
        view = self.doc["bufferViews"][index]
        if not isinstance(view.get("buffer", 0), int) or not 0 <= view.get("buffer", 0) < len(self.buffers):
            raise ValueError(f"bufferView {index} has an invalid buffer index")
        data = self.buffers[view.get("buffer", 0)]
        start, length = view.get("byteOffset", 0), view["byteLength"]
        if start < 0 or length < 0 or start + length > len(data):
            raise ValueError(f"bufferView {index} lies outside its buffer")
        return memoryview(data)[start:start + length], view

    def unpack_values(self, view_index, offset, count, component, kind, normalize=False):
        data, view = self.read_view(view_index)
        fmt, size = COMPONENTS[component]
        dimension = COUNTS[kind]
        if kind.startswith("MAT"):
            rows = int(kind[-1])
            column_stride = ((rows * size + 3) // 4) * 4
            offsets = [col * column_stride + row * size for col in range(rows) for row in range(rows)]
            element_size = rows * column_stride
        else:
            offsets, element_size = [size * i for i in range(dimension)], size * dimension
        stride = view.get("byteStride", element_size)
        if stride < element_size or offset < 0 or count < 0:
            raise ValueError("invalid accessor stride, offset, or count")
        if count and offset + (count - 1) * stride + element_size > len(data):
            raise ValueError("accessor overruns its bufferView")
        output = []
        normalizers = {5120: 127, 5121: 255, 5122: 32767, 5123: 65535}
        for item in range(count):
            values = [struct.unpack_from("<" + fmt, data, offset + item * stride + p)[0] for p in offsets]
            if normalize and component in normalizers:
                denominator = normalizers[component]
                values = [max(-1., v / denominator) if component in (5120, 5122) else v / denominator for v in values]
            if any(not math.isfinite(v) for v in values):
                raise ValueError("accessor contains a non-finite number")
            output.append(tuple(values))
        return output

    def accessor(self, index):
        if not isinstance(index, int) or not 0 <= index < len(self.doc.get("accessors", [])):
            raise ValueError(f"invalid accessor index {index}")
        if index in self.cache:
            return self.cache[index]
        accessor = self.doc["accessors"][index]
        count, component, kind = accessor["count"], accessor["componentType"], accessor["type"]
        if kind not in COUNTS or component not in COMPONENTS:
            raise ValueError(f"unsupported accessor {index} type/component")
        if "bufferView" in accessor:
            values = self.unpack_values(accessor["bufferView"], accessor.get("byteOffset", 0), count,
                                        component, kind, accessor.get("normalized", False))
        else:
            values = [(0,) * COUNTS[kind] for _ in range(count)]
        sparse = accessor.get("sparse")
        if sparse:
            sidx, sval = sparse["indices"], sparse["values"]
            indices = self.unpack_values(sidx["bufferView"], sidx.get("byteOffset", 0), sparse["count"],
                                         sidx["componentType"], "SCALAR")
            replacements = self.unpack_values(sval["bufferView"], sval.get("byteOffset", 0), sparse["count"],
                                              component, kind, accessor.get("normalized", False))
            previous = -1
            for (idx,), replacement in zip(indices, replacements):
                if idx <= previous or idx >= count:
                    raise ValueError(f"accessor {index} has invalid sparse indices")
                values[idx] = replacement
                previous = idx
        self.cache[index] = values
        return values


def inspect(path, expected_clips=(), require_in_place=False):
    report = {"file": str(Path(path).resolve()), "passed": False, "errors": [], "warnings": []}
    errors, warnings = report["errors"], report["warnings"]
    try:
        asset = GLB(path)
        doc = asset.doc
        if str(doc.get("asset", {}).get("version")) != "2.0":
            errors.append("asset.version is not 2.0")
        # JSON itself can contain NaN/Infinity even when its binary buffers do not.
        def finite_json(value, label="document"):
            if isinstance(value, float) and not math.isfinite(value):
                errors.append(f"non-finite JSON number at {label}")
            elif isinstance(value, dict):
                for key, item in value.items(): finite_json(item, f"{label}.{key}")
            elif isinstance(value, list):
                for i, item in enumerate(value): finite_json(item, f"{label}[{i}]")
        finite_json(doc)
        for idx in range(len(doc.get("bufferViews", []))): asset.read_view(idx)
        for idx in range(len(doc.get("accessors", []))): asset.accessor(idx)

        nodes, meshes, skins = doc.get("nodes", []), doc.get("meshes", []), doc.get("skins", [])
        parents = {}
        for idx, node in enumerate(nodes):
            if "matrix" in node and any(key in node for key in ("translation", "rotation", "scale")):
                errors.append(f"node {idx} specifies both matrix and TRS")
            rotation = node.get("rotation", [0, 0, 0, 1])
            if abs(sum(v*v for v in rotation) - 1.) > .005:
                errors.append(f"node {idx} has a non-unit quaternion")
            for child in node.get("children", []):
                if not isinstance(child, int) or not 0 <= child < len(nodes):
                    errors.append(f"node {idx} refers to an invalid child")
                elif child in parents:
                    errors.append(f"node {child} has multiple parents")
                else: parents[child] = idx
        matrices, active = {}, set()
        def world(index):
            if index in matrices: return matrices[index]
            if index in active: raise ValueError("the node hierarchy contains a cycle")
            active.add(index)
            result = local_matrix(nodes[index])
            if index in parents: result = multiply(world(parents[index]), result)
            active.remove(index)
            matrices[index] = result
            return result
        for idx in range(len(nodes)): world(idx)

        if not skins: errors.append("no skin found: this is not a skinned character")
        joint_nodes, skin_summaries, skin_matrices = set(), [], {}
        for skin_idx, skin in enumerate(skins):
            joints = skin.get("joints", [])
            if not joints or len(joints) != len(set(joints)):
                errors.append(f"skin {skin_idx} has empty or duplicate joints")
            if any(not isinstance(j, int) or not 0 <= j < len(nodes) for j in joints):
                raise ValueError(f"skin {skin_idx} contains invalid node indices")
            joint_nodes.update(joints)
            inverse = asset.accessor(skin["inverseBindMatrices"]) if "inverseBindMatrices" in skin else [IDENTITY] * len(joints)
            if len(inverse) != len(joints) or any(len(m) != 16 for m in inverse):
                raise ValueError(f"skin {skin_idx} inverse-bind matrix count/type is wrong")
            skin_matrices[skin_idx] = [multiply(world(j), inverse[i]) for i, j in enumerate(joints)]
            roots = [j for j in joints if parents.get(j) not in joints]
            skeleton = skin.get("skeleton")
            if skeleton is not None:
                for joint in joints:
                    chain, current = set(), joint
                    while current is not None:
                        chain.add(current); current = parents.get(current)
                    if skeleton not in chain:
                        errors.append(f"skin {skin_idx} skeleton node is not an ancestor of joint {joint}")
            skin_summaries.append({"name": skin.get("name", f"skin_{skin_idx}"), "joint_count": len(joints),
                                   "root_names": [nodes[j].get("name", str(j)) for j in roots],
                                   "joint_names": [nodes[j].get("name", str(j)) for j in joints]})

        total_vertices, triangles, primitives, skinned_vertices = 0, 0, 0, 0
        max_weight_error, unskinned, weighted_primitive_count = 0., [], 0
        aabb_min, aabb_max = None, None
        for node_idx, node in enumerate(nodes):
            if "mesh" not in node: continue
            mesh_idx = node["mesh"]
            if not isinstance(mesh_idx, int) or not 0 <= mesh_idx < len(meshes):
                raise ValueError(f"node {node_idx} has an invalid mesh index")
            mesh = meshes[mesh_idx]
            skin_idx = node.get("skin")
            if skin_idx is None: unskinned.append(node.get("name", str(node_idx)))
            elif not 0 <= skin_idx < len(skins): raise ValueError(f"node {node_idx} references invalid skin")
            for primitive_idx, primitive in enumerate(mesh.get("primitives", [])):
                attrs = primitive.get("attributes", {})
                if "POSITION" not in attrs: raise ValueError(f"mesh {mesh_idx} has no POSITION attribute")
                positions = asset.accessor(attrs["POSITION"])
                count = len(positions)
                if not count or any(len(v) != 3 for v in positions):
                    raise ValueError(f"mesh {mesh_idx} has empty or invalid positions")
                for semantic, accessor_index in attrs.items():
                    if len(asset.accessor(accessor_index)) != count:
                        errors.append(f"mesh {mesh_idx} {semantic} count differs from POSITION")
                indices = asset.accessor(primitive["indices"]) if "indices" in primitive else [(i,) for i in range(count)]
                if any(len(row) != 1 or not isinstance(row[0], int) or row[0] < 0 or row[0] >= count for row in indices):
                    errors.append(f"mesh {mesh_idx} contains an out-of-range vertex index")
                material = primitive.get("material")
                if material is not None and (not isinstance(material, int) or not 0 <= material < len(doc.get("materials", []))):
                    errors.append(f"mesh {mesh_idx} refers to an invalid material")
                mode = primitive.get("mode", 4)
                tri_count = len(indices) // 3 if mode == 4 else max(0, len(indices)-2) if mode in (5, 6) else 0
                if mode == 4 and len(indices) % 3: errors.append(f"mesh {mesh_idx} TRIANGLES count is not divisible by three")
                if mode not in (4, 5, 6): warnings.append(f"mesh {mesh_idx} includes non-triangle primitive mode {mode}")
                primitives += 1; total_vertices += count; triangles += tri_count
                weight_sets = []
                if skin_idx is not None:
                    if "JOINTS_0" not in attrs or "WEIGHTS_0" not in attrs:
                        errors.append(f"skinned mesh {mesh_idx} lacks joint indices or weights")
                    else:
                        set_idx = 0
                        while f"JOINTS_{set_idx}" in attrs or f"WEIGHTS_{set_idx}" in attrs:
                            jk, wk = f"JOINTS_{set_idx}", f"WEIGHTS_{set_idx}"
                            if jk not in attrs or wk not in attrs: raise ValueError("unpaired joint/weight attribute")
                            if any(doc["accessors"][attrs[key]]["type"] != "VEC4" for key in (jk, wk)):
                                raise ValueError("joint and weight attributes must be VEC4")
                            weight_sets.append((asset.accessor(attrs[jk]), asset.accessor(attrs[wk])))
                            set_idx += 1
                        weighted_primitive_count += 1; skinned_vertices += count
                for i, position in enumerate(positions):
                    if weight_sets:
                        influences = [(joint, weight) for js, ws in weight_sets for joint, weight in zip(js[i], ws[i])]
                        weight_sum = sum(weight for _, weight in influences)
                        err = abs(weight_sum - 1.)
                        max_weight_error = max(max_weight_error, err)
                        if err > .01 or any(weight < 0 or weight > 1.000001 for _, weight in influences):
                            errors.append(f"mesh {mesh_idx} vertex {i} has invalid skin weights (sum {weight_sum:.6g})")
                            if sum("skin weights" in text for text in errors) > 20: raise ValueError("too many invalid skin weights")
                        if any(not isinstance(joint, int) or not 0 <= joint < len(skins[skin_idx]["joints"]) for joint, _ in influences):
                            raise ValueError(f"mesh {mesh_idx} vertex {i} has invalid skin joint indices")
                        transformed = [0., 0., 0.]
                        for joint, weight in influences:
                            if weight:
                                mapped = point(skin_matrices[skin_idx][joint], position)
                                for axis in range(3): transformed[axis] += weight * mapped[axis]
                    else: transformed = point(world(node_idx), position)
                    if any(not math.isfinite(v) for v in transformed): raise ValueError("bind-pose world bounds contain non-finite numbers")
                    if aabb_min is None: aabb_min, aabb_max = transformed[:], transformed[:]
                    else:
                        aabb_min = [min(a, b) for a, b in zip(aabb_min, transformed)]
                        aabb_max = [max(a, b) for a, b in zip(aabb_max, transformed)]
        if not weighted_primitive_count: errors.append("no mesh primitive has usable skin weights")
        if unskinned: warnings.append(f"{len(unskinned)} mesh nodes are not skinned; verify any bone-parented details intentionally remain attached")
        if triangles > 40000: warnings.append("triangle count exceeds 40,000; profile and optimize for the target phone")
        if primitives > 24: warnings.append("more than 24 mesh primitives may cause many draw calls; consider joining meshes/materials")

        animation_summaries, names, any_animated_bone = [], set(), False
        for anim_idx, animation in enumerate(doc.get("animations", [])):
            name = animation.get("name", f"animation_{anim_idx}")
            if name in names: errors.append(f"duplicate animation name {name}")
            names.add(name)
            seen, animated, moving, times_all, keys, root_motion = set(), set(), set(), [], 0, []
            for channel in animation.get("channels", []):
                target = channel.get("target", {})
                node_idx, path_name = target.get("node"), target.get("path")
                if node_idx is None or not 0 <= node_idx < len(nodes): raise ValueError(f"{name}: invalid animation target")
                if path_name not in ("translation", "rotation", "scale", "weights"): raise ValueError(f"{name}: unsupported target path")
                if (node_idx, path_name) in seen: errors.append(f"{name}: duplicate channel for node {node_idx} {path_name}")
                seen.add((node_idx, path_name)); animated.add(node_idx)
                sampler = animation["samplers"][channel["sampler"]]
                if doc["accessors"][sampler["input"]]["type"] != "SCALAR":
                    raise ValueError(f"{name}: animation input times must be SCALAR")
                times = [v[0] for v in asset.accessor(sampler["input"])]
                values = asset.accessor(sampler["output"])
                interpolation = sampler.get("interpolation", "LINEAR")
                if interpolation not in ("LINEAR", "STEP", "CUBICSPLINE"): raise ValueError(f"{name}: unknown interpolation")
                if not times or times[0] < 0 or any(b <= a for a, b in zip(times, times[1:])):
                    raise ValueError(f"{name}: animation times must be nonnegative and strictly increasing")
                stride = 3 if interpolation == "CUBICSPLINE" else 1
                if path_name != "weights" and len(values) != len(times) * stride:
                    raise ValueError(f"{name}: keyframe input/output counts differ")
                samples = values[1::3] if interpolation == "CUBICSPLINE" else values
                dim = 4 if path_name == "rotation" else 3
                if path_name != "weights" and any(len(v) != dim for v in samples):
                    raise ValueError(f"{name}: {path_name} has incorrect vector size")
                if path_name == "rotation":
                    if any(abs(sum(x*x for x in q)-1.) > .01 for q in samples): errors.append(f"{name}: animation contains a non-unit quaternion")
                    changes = any(abs(abs(sum(a*b for a, b in zip(samples[0], q)))-1.) > 1e-5 for q in samples[1:])
                else: changes = any(any(abs(a-b) > 1e-6 for a, b in zip(samples[0], v)) for v in samples[1:])
                if changes: moving.add(node_idx)
                if changes and node_idx in joint_nodes: any_animated_bone = True
                if path_name == "translation" and node_idx in joint_nodes and parents.get(node_idx) not in joint_nodes:
                    variation = max((math.dist(samples[0], v) for v in samples), default=0.)
                    root_motion.append({"bone": nodes[node_idx].get("name", str(node_idx)), "max_translation_delta": variation})
                    if require_in_place and variation > 1e-5: errors.append(f"{name}: skeleton root translation changes; expected in-place animation")
                times_all.extend(times); keys += len(times)
            duration = max(times_all)-min(times_all) if times_all else 0.
            if not animation.get("channels") or duration <= 0: errors.append(f"{name}: animation is empty or has zero duration")
            if not moving: warnings.append(f"{name}: all animation values are constant")
            animation_summaries.append({"name": name, "duration_seconds": round(duration, 6), "channel_count": len(seen),
                                        "keyframes": keys, "target_node_count": len(animated), "moving_joint_count": len(moving & joint_nodes),
                                        "root_motion": root_motion})
        if not animation_summaries: errors.append("no animation clips found")
        elif not any_animated_bone: errors.append("animations contain no changing bone transforms")
        for expected in expected_clips:
            if expected not in names: errors.append(f"expected clip is missing: {expected}")

        image_summaries = []
        for idx, image in enumerate(doc.get("images", [])):
            uri, image_data = image.get("uri"), None
            if uri:
                if not uri.startswith("data:"): errors.append(f"image {idx} uses an external URI")
                else: image_data = base64.b64decode(uri.split(",", 1)[1])
            elif "bufferView" in image: image_data = bytes(asset.read_view(image["bufferView"])[0])
            else: errors.append(f"image {idx} has no data")
            row = {"name": image.get("name", f"image_{idx}"), "mime_type": image.get("mimeType"), "embedded_bytes": len(image_data or b"")}
            if image_data and image_data.startswith(b"\x89PNG\r\n\x1a\n") and len(image_data) >= 24:
                row["width"], row["height"] = struct.unpack_from(">II", image_data, 16)
            image_summaries.append(row)
        report.update({"file_bytes": asset.size, "generator": doc.get("asset", {}).get("generator"),
                       "node_count": len(nodes), "mesh_count": len(meshes), "primitive_instances": primitives,
                       "vertices_instanced": total_vertices, "triangles_instanced": triangles,
                       "skinned_vertices_instanced": skinned_vertices, "max_weight_sum_error": max_weight_error,
                       "skin_count": len(skins), "skins": skin_summaries,
                       "material_count": len(doc.get("materials", [])), "texture_count": len(doc.get("textures", [])),
                       "images": image_summaries, "animations": animation_summaries,
                       "bind_pose_scene_bounds": {"min": aabb_min, "max": aabb_max,
                                                   "size": [b-a for a, b in zip(aabb_min, aabb_max)] if aabb_min else None},
                       "unskinned_mesh_nodes": unskinned, "extensions_required": doc.get("extensionsRequired", []),
                       "scope": "Structural and binary validation only; appearance, motion quality and device performance require separate inspection."})
    except Exception as exc:
        errors.append(f"{type(exc).__name__}: {exc}")
    report["passed"] = not errors
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("asset", type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--expect-clip", action="append", default=[])
    parser.add_argument("--require-in-place", action="store_true")
    args = parser.parse_args()
    report = inspect(args.asset, args.expect_clip, args.require_in_place)
    encoded = json.dumps(report, indent=2, ensure_ascii=False, allow_nan=False) + "\n"
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(encoded, encoding="utf-8")
    print(encoded, end="")
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
