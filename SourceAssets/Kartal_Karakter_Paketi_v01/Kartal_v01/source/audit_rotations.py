#!/usr/bin/env python3
"""Verify that exported GLB spin/flip clips retain a full sampled 360-degree turn.

Usage:
  python src/audit_rotations.py output/Kartal_Runner_v01.glb \
      --merge-report output/asset_validation.json

This inspects exported binary quaternion samples. It does not inspect the
generator's formulas or claim to evaluate subjective animation smoothness.
"""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

from validate_glb import GLB


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def normalize(q):
    length = math.sqrt(dot(q, q))
    if not math.isfinite(length) or length < 1e-12:
        raise ValueError("rotation sample has no finite quaternion direction")
    return tuple(value / length for value in q)


def conjugate(q):
    return (-q[0], -q[1], -q[2], q[3])


def multiply(a, b):
    x, y, z, w = a
    X, Y, Z, W = b
    return (w*X + x*W + y*Z - z*Y,
            w*Y - x*Z + y*W + z*X,
            w*Z + x*Y - y*X + z*W,
            w*W - x*X - y*Y - z*Z)


def audit(path, clips=("RunTurn", "JumpFlip"), joint="pelvis"):
    report = {
        "file": str(Path(path).resolve()),
        "method": "Read exported joint quaternions, normalize, align each quaternion to the preceding hemisphere, compose successive relative rotations, then sum signed angles along the measured rotation axis. Requires one 360-degree turn, consistent axis, endpoint closure and each sample step below 180 degrees.",
        "clips": [], "errors": [], "passed": False,
    }
    try:
        asset = GLB(path)
        doc = asset.doc
        matches = [i for i, node in enumerate(doc.get("nodes", [])) if node.get("name") == joint]
        if len(matches) != 1:
            raise ValueError(f"expected one node named {joint!r}, found {len(matches)}")
        node_index = matches[0]
        for name in clips:
            animations = [a for a in doc.get("animations", []) if a.get("name") == name]
            if len(animations) != 1:
                raise ValueError(f"expected one clip named {name!r}, found {len(animations)}")
            animation = animations[0]
            channels = [c for c in animation.get("channels", [])
                        if c.get("target") == {"node": node_index, "path": "rotation"}]
            if len(channels) != 1:
                raise ValueError(f"{name} does not contain exactly one {joint} rotation channel")
            sampler = animation["samplers"][channels[0]["sampler"]]
            if sampler.get("interpolation", "LINEAR") != "LINEAR":
                raise ValueError(f"{name} uses an unsupported rotation interpolation; expected LINEAR")
            times = [v[0] for v in asset.accessor(sampler["input"])]
            raw = [normalize(q) for q in asset.accessor(sampler["output"])]
            if len(raw) < 3 or len(raw) != len(times):
                raise ValueError(f"{name} has too few or mismatched rotation samples")
            if any(b <= a for a, b in zip(times, times[1:])):
                raise ValueError(f"{name} has non-increasing sample times")
            samples, sign_corrections = [raw[0]], 0
            for q in raw[1:]:
                if dot(samples[-1], q) < 0:
                    q = tuple(-v for v in q)
                    sign_corrections += 1
                samples.append(q)

            increments = []
            for a, b in zip(samples, samples[1:]):
                q = normalize(multiply(conjugate(a), b))
                magnitude = math.sqrt(dot(q[:3], q[:3]))
                angle = 2 * math.atan2(magnitude, q[3])
                if magnitude > 1e-10:
                    increments.append((angle, tuple(v / magnitude for v in q[:3])))
            axis_sum = [sum(angle * axis[i] for angle, axis in increments) for i in range(3)]
            magnitude = math.sqrt(dot(axis_sum, axis_sum))
            if not increments or magnitude < 1e-8:
                raise ValueError(f"{name} has no measurable net rotation axis")
            axis = tuple(v / magnitude for v in axis_sum)
            travel = [0.]
            for angle, increment_axis in increments:
                travel.append(travel[-1] + angle * dot(axis, increment_axis))
            signed = math.degrees(travel[-1])
            absolute = math.degrees(sum(angle for angle, _ in increments))
            largest_step = math.degrees(max(angle for angle, _ in increments))
            alignment = min(abs(dot(axis, direction)) for _, direction in increments)
            closure = math.degrees(2 * math.acos(min(1., abs(dot(samples[0], samples[-1])))))
            passed = (abs(signed - 360) < .01 and abs(absolute - 360) < .01
                      and largest_step < 180 and alignment > .9999 and closure < .01)
            report["clips"].append({
                "clip": name, "joint": joint, "samples": len(samples),
                "duration_seconds": times[-1] - times[0], "interpolation": "LINEAR",
                "signed_rotation_degrees": signed, "absolute_rotation_degrees": absolute,
                "unwrapped_range_degrees": [math.degrees(min(travel)), math.degrees(max(travel))],
                "dominant_joint_local_axis": axis,
                "minimum_increment_axis_alignment": alignment,
                "largest_rotation_step_degrees": largest_step,
                "endpoint_orientation_difference_degrees": closure,
                "quaternion_hemisphere_sign_corrections": sign_corrections,
                "passed": passed,
            })
            if not passed:
                report["errors"].append(f"{name} did not retain one continuous full 360-degree turn")
    except Exception as exc:
        report["errors"].append(f"{type(exc).__name__}: {exc}")
    report["passed"] = not report["errors"] and len(report["clips"]) == len(clips)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("asset", type=Path)
    parser.add_argument("--clip", action="append", help="Repeat for expected full-turn clips; default RunTurn and JumpFlip")
    parser.add_argument("--joint", default="pelvis")
    parser.add_argument("--report", type=Path, help="Write a standalone rotation audit")
    parser.add_argument("--merge-report", type=Path, help="Add rotation_travel_validation to an existing structural QA report")
    args = parser.parse_args()
    report = audit(args.asset, args.clip or ("RunTurn", "JumpFlip"), args.joint)
    encoded = json.dumps(report, indent=2, allow_nan=False) + "\n"
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(encoded, encoding="utf-8")
    if args.merge_report:
        combined = json.loads(args.merge_report.read_text(encoding="utf-8"))
        combined["rotation_travel_validation"] = report
        if not report["passed"]:
            combined.setdefault("errors", []).extend(report["errors"])
            combined["passed"] = False
        args.merge_report.write_text(json.dumps(combined, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(encoded, end="")
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
