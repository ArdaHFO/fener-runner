"""Re-import Kartal's exported FBX into an empty Blender process and inspect it.

Run with Blender 4.5:
  blender --background --factory-startup --python-exit-code 1 \
    --python src/validate_fbx.py -- output/Kartal_Runner_v01.fbx \
    --report qa/fbx_roundtrip.json

This checks export integrity in Blender, not compatibility with any game engine.
"""

import argparse
import json
import math
from pathlib import Path
import sys

import bpy


EXPECTED_CLIPS = {"Idle", "Run", "Jump", "Slide", "RunTurn", "JumpFlip", "CelebratePoint"}


def action_fcurves(action):
    curves = []
    if hasattr(action, "layers"):
        for layer in action.layers:
            for strip in layer.strips:
                if hasattr(strip, "channelbags"):
                    for bag in strip.channelbags:
                        curves.extend(list(bag.fcurves))
    if not curves and hasattr(action, "fcurves"):
        curves = list(action.fcurves)
    return curves


def inspect(source):
    report = {
        "file": str(source), "blender_version": bpy.app.version_string,
        "method": "Factory-empty Blender process imports the exported FBX, verifies armature/skin and normalized vertex weights, then assigns every imported bone action and evaluates real bone transforms across its frame range.",
        "passed": False, "errors": [], "warnings": [],
    }
    errors = report["errors"]
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(source), use_anim=True, use_image_search=False)
        rigs = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
        meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
        if len(rigs) != 1:
            raise ValueError(f"Expected one armature, found {len(rigs)}")
        rig = rigs[0]
        bone_names = [bone.name for bone in rig.data.bones]
        if len(bone_names) != 19:
            errors.append(f"Expected 19 bones, found {len(bone_names)}")
        if not meshes:
            errors.append("No imported mesh")
        mesh_summaries = []
        for obj in meshes:
            modifiers = [modifier for modifier in obj.modifiers if modifier.type == "ARMATURE"]
            valid_modifier = any(modifier.object == rig and modifier.show_viewport for modifier in modifiers)
            if not valid_modifier:
                errors.append(obj.name + ": no active skin modifier targeting imported rig")
            groups = {group.index: group.name for group in obj.vertex_groups}
            bad, weighted, max_error = 0, 0, 0.
            for vertex in obj.data.vertices:
                weights = [group.weight for group in vertex.groups if groups.get(group.group) in bone_names]
                if weights:
                    weighted += 1
                error = abs(sum(weights) - 1.)
                max_error = max(max_error, error)
                if error > .01 or any(not math.isfinite(w) or w < 0 or w > 1.000001 for w in weights):
                    bad += 1
            if bad:
                errors.append(f"{obj.name}: {bad} vertices have invalid/missing bone weights")
            mesh_summaries.append({
                "name": obj.name, "vertex_count": len(obj.data.vertices), "weighted_vertices": weighted,
                "armature_modifier_count": len(modifiers), "skin_modifier_targets_rig": valid_modifier,
                "max_weight_sum_error": max_error, "material_slots": len(obj.material_slots),
            })

        actions = [action for action in bpy.data.actions
                   if any(curve.data_path.startswith("pose.bones[") for curve in action_fcurves(action))]
        names, animation_summaries = [], []
        rig.animation_data_create()
        rig.animation_data.use_nla = False
        for action in actions:
            rig.animation_data.action = action
            if hasattr(action, "slots") and len(action.slots):
                rig.animation_data.action_slot = action.slots[0]
            start, end = map(float, action.frame_range)
            if end <= start:
                errors.append(action.name + ": empty animation interval")
            frames = [start + (end - start) * index / 30. for index in range(31)]
            baseline, moving, nonfinite = None, set(), 0
            max_delta, root_delta = 0., 0.
            for frame in frames:
                whole = math.floor(frame)
                bpy.context.scene.frame_set(whole, subframe=frame - whole)
                bpy.context.view_layer.update()
                values = {bone.name: tuple(value for row in bone.matrix_basis for value in row)
                          for bone in rig.pose.bones}
                if baseline is None:
                    baseline = values
                for name, current in values.items():
                    if any(not math.isfinite(value) for value in current):
                        nonfinite += 1
                    delta = max(abs(a - b) for a, b in zip(baseline[name], current))
                    max_delta = max(max_delta, delta)
                    if delta > 1e-5:
                        moving.add(name)
                    if name == "root":
                        root_delta = max(root_delta, delta)
            if not moving:
                errors.append(action.name + ": no evaluated moving bone transforms")
            if nonfinite:
                errors.append(action.name + ": non-finite bone transforms")
            curves = action_fcurves(action)
            clip_name = action.name.rsplit("|", 1)[-1]
            names.append(clip_name)
            animation_summaries.append({
                "action_name": action.name, "clip_name": clip_name, "frame_range": [start, end],
                "duration_seconds": (end - start) * bpy.context.scene.render.fps_base / bpy.context.scene.render.fps,
                "fcurve_count": len(curves), "keyframes": sum(len(curve.keyframe_points) for curve in curves),
                "evaluated_samples": len(frames), "moving_bones": sorted(moving),
                "max_bone_matrix_delta": max_delta, "root_matrix_delta": root_delta,
                "action_slots": [slot.identifier for slot in action.slots] if hasattr(action, "slots") else [],
            })
        missing = sorted(EXPECTED_CLIPS - set(names))
        duplicates = sorted({name for name in names if names.count(name) > 1})
        if missing:
            errors.append("Missing FBX animation stacks: " + ", ".join(missing))
        if duplicates:
            errors.append("Duplicate imported bone action names: " + ", ".join(duplicates))
        if len(actions) != len(EXPECTED_CLIPS):
            errors.append(f"Expected seven bone actions, found {len(actions)}")
        report.update({
            "file_bytes": source.stat().st_size, "armature_count": len(rigs), "bone_count": len(bone_names),
            "bone_names": bone_names, "root_bones": [bone.name for bone in rig.data.bones if bone.parent is None],
            "mesh_count": len(meshes), "meshes": mesh_summaries,
            "animation_stack_count": len(actions), "animations": animation_summaries,
            "scene_fps": bpy.context.scene.render.fps / bpy.context.scene.render.fps_base,
            "scope": "Blender FBX re-import integrity only; no game-engine or physical-device compatibility claim.",
        })
    except Exception as exc:
        errors.append(f"{type(exc).__name__}: {exc}")
    report["passed"] = not errors
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("asset", type=Path)
    parser.add_argument("--report", type=Path, required=True)
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = parser.parse_args(arguments)
    report = inspect(args.asset.resolve())
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print("FBX_ROUNDTRIP_RESULT " + json.dumps({
        "passed": report["passed"], "errors": report["errors"], "bones": report.get("bone_count"),
        "meshes": report.get("mesh_count"), "animation_stacks": report.get("animation_stack_count"),
        "report": str(args.report.resolve()),
    }))
    if not report["passed"]:
        raise RuntimeError("FBX round-trip checks failed: " + str(report["errors"]))


if __name__ == "__main__":
    main()
