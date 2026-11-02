"""Real deform rig and named, in-place animation library for the Kartal mascot.

Blender 4.5. Front is -Y, Z is up. All action names are stable integration IDs.
The engine owns world movement and jump arcs; the root bone remains stationary.
"""
from __future__ import annotations

import math
import json
import bpy
from mathutils import Vector, Quaternion, Euler

FPS = 30
TAU = math.tau


def _clamp(value, lo=0.0, hi=1.0):
    return max(lo, min(hi, value))


def _smooth(value):
    value = _clamp(value)
    return value * value * (3.0 - 2.0 * value)


def _smoother(value):
    value = _clamp(value)
    return value ** 3 * (value * (value * 6.0 - 15.0) + 10.0)


def _curve(t, points):
    """C1 interpolation between explicit motion-beat values."""
    if t <= points[0][0]:
        return points[0][1]
    for (a, va), (b, vb) in zip(points, points[1:]):
        if t <= b:
            return va + (vb - va) * _smooth((t - a) / (b - a))
    return points[-1][1]


def _segment_distance(point, start, end):
    delta = end - start
    t = _clamp((point - start).dot(delta) / max(delta.length_squared, 1e-12))
    return (point - (start + t * delta)).length


def _bone_definitions():
    bones = [
        ('root', (0, 0, 0), (0, 0, .18), None),
        ('pelvis', (0, 0, .58), (0, 0, .84), 'root'),
        ('spine', (0, 0, .84), (0, 0, 1.05), 'pelvis'),
        ('chest', (0, 0, 1.05), (0, 0, 1.22), 'spine'),
        ('neck', (0, 0, 1.22), (0, -.015, 1.48), 'chest'),
        ('head', (0, -.015, 1.48), (0, -.015, 1.88), 'neck'),
        ('jaw', (0, -.34, 1.28), (0, -.50, 1.21), 'head'),
    ]
    for side, sign in [('L', 1), ('R', -1)]:
        bones.extend([
            (f'upper_arm.{side}', (.30 * sign, 0, 1.04), (.56 * sign, 0, .87), 'chest'),
            (f'forearm.{side}', (.56 * sign, 0, .87), (.73 * sign, -.015, .70), f'upper_arm.{side}'),
            (f'hand.{side}', (.73 * sign, -.015, .70), (.80 * sign, -.04, .64), f'forearm.{side}'),
            (f'thigh.{side}', (.17 * sign, 0, .58), (.18 * sign, -.015, .30), 'pelvis'),
            (f'shin.{side}', (.18 * sign, -.015, .30), (.18 * sign, 0, .12), f'thigh.{side}'),
            (f'foot.{side}', (.18 * sign, 0, .12), (.18 * sign, -.20, .065), f'shin.{side}'),
        ])
    return bones


def build_rig(meshes):
    """Create the deform armature and bind every supplied mesh with weights."""
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    data = bpy.data.armatures.new('Kartal_Skeleton')
    rig = bpy.data.objects.new('Kartal_Rig', data)
    collection = meshes[0].users_collection[0] if meshes else bpy.context.collection
    collection.objects.link(rig)
    rig.show_in_front = True
    rig.data.display_type = 'OCTAHEDRAL'
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    definitions = _bone_definitions()
    for name, head, tail, parent in definitions:
        bone = data.edit_bones.new(name)
        bone.head, bone.tail = head, tail
        # Consistent forward-facing local roll; no IK constraints survive export.
        bone.align_roll(Vector((0, -1, 0)))
        bone.use_deform = True
        if parent:
            bone.parent = data.edit_bones[parent]
            bone.use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    rig['character_id'] = 'kartal_runner_v01'
    rig['rig_type'] = 'Generic'
    rig['forward_axis_blender'] = '-Y'
    rig['root_motion'] = 'IN_PLACE: world movement and jump height belong to the game controller'
    rig['animation_fps'] = FPS
    for pose_bone in rig.pose.bones:
        pose_bone.rotation_mode = 'QUATERNION'

    arm_landmarks = {}
    for side, sign in [('L', 1), ('R', -1)]:
        arm_landmarks[side] = (
            Vector((.30 * sign, 0, 1.04)), Vector((.56 * sign, 0, .87)),
            Vector((.73 * sign, -.015, .70)),
        )

    for obj in meshes:
        if obj.type != 'MESH':
            continue
        world_matrix = obj.matrix_world.copy()
        requested = str(obj.get('bind_bone', 'spine'))
        bone_name = requested if requested in rig.data.bones else 'spine'
        segment = str(obj.get('skin_segment', ''))
        # Each output mesh has a valid deform modifier and normalized weights.
        for group in list(obj.vertex_groups):
            obj.vertex_groups.remove(group)
        groups = {}

        def weight(index, name, value):
            if value <= .00001:
                return
            group = groups.get(name)
            if group is None:
                group = groups[name] = obj.vertex_groups.new(name=name)
            group.add([index], float(value), 'REPLACE')

        for vertex in obj.data.vertices:
            point = world_matrix @ vertex.co
            if segment in {'arm.L', 'arm.R'}:
                side = segment[-1]
                shoulder, elbow, wrist = arm_landmarks[side]
                # Project onto the shoulder-wrist centerline; a broad elbow
                # blend avoids a rigid seam in the knitted sleeves.
                axis = wrist - shoulder
                along = (point - shoulder).dot(axis) / axis.length_squared
                elbow_t = (elbow - shoulder).dot(axis) / axis.length_squared
                fore = _smooth((along - (elbow_t - .16)) / .32)
                near_shoulder = 1.0 - _smooth((along + .04) / .20)
                chest = .20 * near_shoulder
                weight(vertex.index, 'chest', chest)
                weight(vertex.index, f'upper_arm.{side}', (1.0 - chest) * (1.0 - fore))
                weight(vertex.index, f'forearm.{side}', (1.0 - chest) * fore)
            elif segment in {'leg.L', 'leg.R'}:
                side = segment[-1]
                upper = _smooth((point.z - .23) / .15)
                weight(vertex.index, f'thigh.{side}', upper)
                weight(vertex.index, f'shin.{side}', 1.0 - upper)
            elif bone_name in {'spine', 'chest', 'pelvis'} and 'collar' not in obj.name.lower():
                # The sweater/belly bends gradually with the torso.
                z = point.z
                if z < .79:
                    blend = _smooth((z - .57) / .22)
                    weight(vertex.index, 'pelvis', 1.0 - blend)
                    weight(vertex.index, 'spine', blend)
                else:
                    blend = _smooth((z - .88) / .24)
                    weight(vertex.index, 'spine', 1.0 - blend)
                    weight(vertex.index, 'chest', blend)
            else:
                weight(vertex.index, bone_name, 1.0)

        obj.parent = rig
        obj.matrix_world = world_matrix
        modifier = obj.modifiers.get('Kartal_Deform') or obj.modifiers.new('Kartal_Deform', 'ARMATURE')
        modifier.object = rig
        modifier.use_vertex_groups = True
        modifier.use_bone_envelopes = False
        modifier.use_deform_preserve_volume = False  # Linear blend skinning matches game engines.
        obj['rigged'] = True
        obj['requested_bind_bone'] = requested
    bpy.context.view_layer.update()
    return rig


def reset_pose(rig):
    for bone in rig.pose.bones:
        bone.location = (0, 0, 0)
        bone.rotation_mode = 'QUATERNION'
        bone.rotation_quaternion = (1, 0, 0, 0)
        bone.scale = (1, 1, 1)
    rig.location = (0, 0, 0)
    rig.rotation_euler = (0, 0, 0)
    rig.scale = (1, 1, 1)


def _neutral():
    pose = {}
    for side, sign in [('L', 1), ('R', -1)]:
        pose[f'upper_arm.{side}'] = {'rot': (-.04, .47 * sign, 0)}
        pose[f'forearm.{side}'] = {'rot': (-.11, .04 * sign, 0)}
        pose[f'hand.{side}'] = {'rot': (.02, .02 * sign, 0)}
    return pose


def _leg_ik(pose, side, ankle_y, ankle_z, pelvis_z, foot_pitch=0.0):
    """Analytic two-link sagittal solution, baked to ordinary joint rotations."""
    upper = math.hypot(.015, .28)
    lower = math.hypot(.015, .18)
    dy, dz = ankle_y, ankle_z - (.58 + pelvis_z)
    distance = _clamp(math.hypot(dy, dz), abs(upper - lower) + .002, upper + lower - .001)
    aim = math.atan2(dy, -dz)
    alpha = math.acos(_clamp((upper * upper + distance * distance - lower * lower) / (2 * upper * distance), -1, 1))
    bend = math.acos(_clamp((distance * distance - upper * upper - lower * lower) / (2 * upper * lower), -1, 1))
    thigh_rest = math.atan2(-.015, .28)
    shin_rest = math.atan2(.015, .18)
    thigh_rotation = aim - alpha - thigh_rest
    shin_rotation = bend - (shin_rest - thigh_rest)
    pose[f'thigh.{side}'] = {'rot': (thigh_rotation, 0, 0)}
    pose[f'shin.{side}'] = {'rot': (shin_rotation, 0, 0)}
    pose[f'foot.{side}'] = {'rot': (foot_pitch - thigh_rotation - shin_rotation, 0, 0)}


def _idle(t):
    pose = _neutral()
    s = math.sin(TAU * t)
    pose['pelvis'] = {'loc': (0, 0, .004 * s)}
    pose['spine'] = {'rot': (.012 * s, 0, .009 * s)}
    pose['chest'] = {'scale': (1 + .008 * s, 1 + .010 * s, 1 + .005 * s)}
    pose['neck'] = {'rot': (-.009 * s, 0, -.008 * s)}
    pose['head'] = {'rot': (.010 * math.sin(TAU * t + .5), .012 * s, .023 * s)}
    pose['jaw'] = {'rot': (-.018 * (1.0 + s), 0, 0)}
    return pose


def _run(t):
    pose = _neutral()
    phase = TAU * t
    s = math.sin(phase)
    bob = -.045 + .028 * (.5 - .5 * math.cos(phase * 2))
    pose['pelvis'] = {'loc': (0, 0, bob), 'rot': (0, 0, .035 * s)}
    pose['spine'] = {'rot': (.10, 0, -.025 * s)}
    pose['chest'] = {'rot': (.025, 0, -.035 * s)}
    pose['neck'] = {'rot': (-.06, 0, .012 * s)}
    pose['head'] = {'rot': (-.065 + .010 * math.sin(phase * 2), .012 * s, .013 * s)}
    for side, sign, offset in [('L', 1, 0), ('R', -1, .5)]:
        u = (t + offset) % 1.0
        if u < .55:
            v = u / .55
            ankle_y = -.13 + .26 * v
            ankle_z = .12
            foot_pitch = 0.0
        else:
            v = (u - .55) / .45
            ankle_y = .13 - .26 * _smooth(v)
            ankle_z = .12 + .115 * math.sin(math.pi * v)
            foot_pitch = -.24 * math.sin(math.pi * v)
        _leg_ik(pose, side, ankle_y, ankle_z, bob, foot_pitch)
        swing = math.sin(TAU * (t + offset))
        pose[f'upper_arm.{side}'] = {'rot': (.46 * swing - .05, sign * .39, sign * .025)}
        pose[f'forearm.{side}'] = {'rot': (-.35 - .14 * max(0, -swing), .02 * sign, 0)}
        pose[f'hand.{side}'] = {'rot': (-.07, 0, .04 * sign * swing)}
    return pose


def _jump(t):
    pose = _neutral()
    air = _curve(t, [(0, 0), (.16, .22), (.36, 1), (.65, 1), (.84, .30), (1, 0)])
    pelvis_z = _curve(t, [(0, 0), (.10, -.045), (.24, .025), (.62, .025), (.86, -.065), (1, 0)])
    pose['pelvis'] = {'loc': (0, 0, pelvis_z)}
    pose['spine'] = {'rot': (.14 * air, 0, 0)}
    pose['chest'] = {'rot': (-.03 * air, 0, 0)}
    pose['head'] = {'rot': (-.07 * air, 0, 0)}
    for side, sign in [('L', 1), ('R', -1)]:
        pose[f'thigh.{side}'] = {'rot': (-.68 * air, 0, sign * .055 * air)}
        pose[f'shin.{side}'] = {'rot': (1.10 * air, 0, 0)}
        pose[f'foot.{side}'] = {'rot': (-.42 * air, 0, 0)}
        pose[f'upper_arm.{side}'] = {'rot': (-.50 * air, sign * (.47 - .43 * air), 0)}
        pose[f'forearm.{side}'] = {'rot': (-.11 - .18 * air, 0, 0)}
    return pose


def _slide(t):
    pose = _neutral()
    duck = _curve(t, [(0, 0), (.22, 1), (.70, 1), (1, 0)])
    pelvis_z = -.31 * duck
    pose['pelvis'] = {'loc': (0, .025 * duck, pelvis_z)}
    pose['spine'] = {'rot': (.93 * duck, 0, 0)}
    pose['chest'] = {'rot': (.08 * duck, 0, 0)}
    pose['neck'] = {'rot': (-.30 * duck, 0, 0)}
    pose['head'] = {'rot': (-.37 * duck, 0, 0)}
    for side, sign in [('L', 1), ('R', -1)]:
        _leg_ik(pose, side, -.32 * duck, .12, pelvis_z)
        pose[f'upper_arm.{side}'] = {'rot': (.58 * duck - .04, sign * (.47 + .12 * duck), 0)}
        pose[f'forearm.{side}'] = {'rot': (-.11 - .30 * duck, 0, 0)}
    return pose


def _run_turn(t):
    pose = _run(t)
    flourish = math.sin(math.pi * t) ** 2
    spin = TAU * _smoother(t)
    pose['pelvis']['rot'] = (0, 0, spin + .02 * math.sin(TAU * t))
    pose['pelvis']['loc'] = (0, 0, pose['pelvis']['loc'][2] + .105 * flourish)
    pose['spine'] = {'rot': (.075 * (1 - flourish), -.06 * math.sin(TAU * t), 0)}
    pose['chest'] = {'rot': (.015, 0, -.05 * math.sin(TAU * t))}
    pose['head'] = {'rot': (-.04, 0, -.12 * math.sin(TAU * t))}
    for side, sign in [('L', 1), ('R', -1)]:
        arm = pose[f'upper_arm.{side}']['rot']
        pose[f'upper_arm.{side}']['rot'] = (arm[0] * (1 - .65 * flourish), sign * (.39 - .50 * flourish), 0)
        pose[f'forearm.{side}']['rot'] = (-.22 + .11 * flourish, 0, 0)
    return pose


def _jump_flip(t):
    pose = _neutral()
    tuck = _curve(t, [(0, 0), (.16, .15), (.36, 1), (.65, 1), (.88, .12), (1, 0)])
    rotation = TAU * _smoother(_clamp((t - .12) / .76))
    pelvis_z = _curve(t, [(0, 0), (.10, -.04), (.28, .015), (.72, .015), (.90, -.04), (1, 0)])
    pose['pelvis'] = {'loc': (0, 0, pelvis_z), 'rot': (rotation, 0, 0)}
    pose['spine'] = {'rot': (.32 * tuck, 0, 0)}
    pose['chest'] = {'rot': (.10 * tuck, 0, 0)}
    pose['neck'] = {'rot': (.05 * tuck, 0, 0)}
    pose['head'] = {'rot': (-.15 * tuck, 0, 0)}
    for side, sign in [('L', 1), ('R', -1)]:
        pose[f'thigh.{side}'] = {'rot': (-1.02 * tuck, 0, sign * .06 * tuck)}
        pose[f'shin.{side}'] = {'rot': (1.65 * tuck, 0, 0)}
        pose[f'foot.{side}'] = {'rot': (-.62 * tuck, 0, 0)}
        pose[f'upper_arm.{side}'] = {'rot': (-.68 * tuck, sign * (.47 + .15 * tuck), 0)}
        pose[f'forearm.{side}'] = {'rot': (-.11 - .73 * tuck, 0, 0)}
    return pose


def _celebrate(t):
    pose = _neutral()
    reach = _curve(t, [(0, 0), (.24, 1), (.68, 1), (1, 0)])
    snap = math.sin(TAU * _clamp((t - .24) / .44)) * .025 if .24 < t < .68 else 0.0
    pose['pelvis'] = {'rot': (0, -.035 * reach, -.10 * reach)}
    pose['spine'] = {'rot': (-.035 * reach, .04 * reach, -.10 * reach)}
    pose['head'] = {'rot': (-.08 * reach, -.08 * reach, -.16 * reach + snap)}
    pose['jaw'] = {'rot': (-.22 * reach, 0, 0)}
    # Extend one wing like the reference gesture; the other remains relaxed.
    pose['upper_arm.L'] = {'rot': (-.13 * reach, .47 - 1.12 * reach, -.12 * reach)}
    pose['forearm.L'] = {'rot': (-.11 + .11 * reach, .04 - .18 * reach, 0)}
    pose['hand.L'] = {'rot': (0, .08 * reach, -.10 * reach)}
    pose['upper_arm.R'] = {'rot': (-.04, -.47 - .12 * reach, 0)}
    return pose


CLIPS = [
    ('Idle', 2.0, True, _idle, 'Relaxed breathing and small head movement.'),
    ('Run', .8, True, _run, 'Short quick stride, stabilized head and contact-aware feet.'),
    ('Jump', .9, False, _jump, 'Air pose and soft recovery; controller supplies vertical travel.'),
    ('Slide', .8, False, _slide, 'Tucked seated slide with folded legs and lowered head.'),
    ('RunTurn', 1.0, False, _run_turn, 'One complete 360-degree yaw flourish while the root stays in place.'),
    ('JumpFlip', 1.0, False, _jump_flip, 'One complete forward flip; controller supplies the jump arc.'),
    ('CelebratePoint', 1.5, False, _celebrate, 'Confident pointing gesture inspired by the reference.'),
]


def _action_curves(action):
    """Support layered 4.4+ actions and legacy actions without double yielding."""
    seen = set()
    for layer in getattr(action, 'layers', []):
        for strip in layer.strips:
            for bag in getattr(strip, 'channelbags', []):
                for curve in bag.fcurves:
                    key = curve.as_pointer()
                    if key not in seen:
                        seen.add(key)
                        yield curve
    if not seen:
        for curve in getattr(action, 'fcurves', []):
            yield curve


def _assign_action(rig, action):
    anim = rig.animation_data_create()
    anim.action = action
    if hasattr(action, 'slots'):
        if not action.slots:
            action.slots.new(id_type='OBJECT', name=rig.name)
        if hasattr(anim, 'action_slot'):
            anim.action_slot = action.slots[0]
    return anim


def set_active_action(rig, action_or_name):
    """Prepare one action for render/preview; all stored NLA tracks stay muted."""
    action = bpy.data.actions[action_or_name] if isinstance(action_or_name, str) else action_or_name
    anim = rig.animation_data_create()
    for track in anim.nla_tracks:
        track.mute = True
        track.is_solo = False
    anim.action = None
    reset_pose(rig)
    _assign_action(rig, action)
    anim.action_blend_type = 'REPLACE'
    anim.action_extrapolation = 'HOLD'
    anim.action_influence = 1.0
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start = int(action.frame_range[0])
    scene.frame_end = int(action.frame_range[1])
    scene.frame_set(scene.frame_start)
    bpy.context.view_layer.update()
    return action


def create_animations(rig):
    """Bake a complete seven-clip library into actual bone quaternion channels."""
    scene = bpy.context.scene
    scene.render.fps = FPS
    anim = rig.animation_data_create()
    for track in list(anim.nla_tracks):
        anim.nla_tracks.remove(track)
    anim.action = None
    basis = {bone.name: bone.bone.matrix_local.to_quaternion() for bone in rig.pose.bones}
    actions = []
    metadata = []

    for name, seconds, loop, pose_function, description in CLIPS:
        action = bpy.data.actions.new(name=name)
        action.use_fake_user = True
        action['loop'] = loop
        action['duration_seconds'] = seconds
        action['fps'] = FPS
        action['in_place'] = True
        action['description'] = description
        if name in {'Jump', 'JumpFlip'}:
            action['controller_jump_arc_required'] = True
        _assign_action(rig, action)
        frame_count = round(seconds * FPS)
        previous_quaternions = {}
        for index in range(frame_count + 1):
            t = index / frame_count
            pose = pose_function(t)
            frame = 1 + index
            reset_pose(rig)
            for bone in rig.pose.bones:
                spec = pose.get(bone.name, {})
                rest = basis[bone.name]
                world_q = Euler(spec.get('rot', (0, 0, 0)), 'XYZ').to_quaternion()
                local_q = rest.inverted() @ world_q @ rest
                local_q.normalize()
                if bone.name in previous_quaternions:
                    local_q.make_compatible(previous_quaternions[bone.name])
                previous_quaternions[bone.name] = local_q.copy()
                bone.rotation_quaternion = local_q
                bone.location = rest.inverted() @ Vector(spec.get('loc', (0, 0, 0)))
                bone.scale = spec.get('scale', (1, 1, 1))
                bone.keyframe_insert(data_path='rotation_quaternion', frame=frame, group=bone.name)
                bone.keyframe_insert(data_path='location', frame=frame, group=bone.name)
                if bone.name == 'chest':
                    bone.keyframe_insert(data_path='scale', frame=frame, group=bone.name)
        for curve in _action_curves(action):
            for key in curve.keyframe_points:
                key.interpolation = 'LINEAR'
        action.use_frame_range = True
        action.frame_start = 1
        action.frame_end = frame_count + 1
        track = anim.nla_tracks.new()
        track.name = name
        strip = track.strips.new(name, 1, action)
        if hasattr(strip, 'action_slot') and action.slots:
            strip.action_slot = action.slots[0]
        strip.extrapolation = 'NOTHING'
        strip.blend_type = 'REPLACE'
        strip.use_auto_blend = False
        strip.influence = 1.0
        track.mute = True
        actions.append(action)
        metadata.append({'name': name, 'seconds': seconds, 'loop': loop,
                         'frames': [1, frame_count + 1], 'in_place': True,
                         'description': description})
    rig['animation_library'] = json.dumps(metadata, ensure_ascii=False)
    set_active_action(rig, actions[0])
    return actions


def validate_rig(rig, meshes, actions):
    """Meaningful structural checks without claiming visual approval."""
    result = {'bone_count': len(rig.data.bones), 'mesh_count': 0,
              'weighted_vertices': 0, 'actions': [], 'root_motion_max': 0.0}
    for obj in meshes:
        if obj.type != 'MESH':
            continue
        result['mesh_count'] += 1
        if not any(mod.type == 'ARMATURE' and mod.object == rig for mod in obj.modifiers):
            raise AssertionError(f'{obj.name}: missing armature deformation')
        for vertex in obj.data.vertices:
            total = sum(group.weight for group in vertex.groups)
            if abs(total - 1.0) > 1e-4:
                raise AssertionError(f'{obj.name}: vertex {vertex.index} weight sum {total}')
            if len(vertex.groups) > 4:
                raise AssertionError(f'{obj.name}: more than 4 skin influences')
            result['weighted_vertices'] += 1
    for action in actions:
        set_active_action(rig, action)
        for frame in [action.frame_range[0], sum(action.frame_range) / 2, action.frame_range[1]]:
            integer = int(frame)
            bpy.context.scene.frame_set(integer, subframe=frame - integer)
            root = rig.pose.bones['root']
            root_motion = max(root.location.length, root.rotation_quaternion.angle)
            result['root_motion_max'] = max(result['root_motion_max'], root_motion)
            if root_motion > 1e-6:
                raise AssertionError(f'{action.name}: unexpected root motion')
        curves = list(_action_curves(action))
        if not curves:
            raise AssertionError(f'{action.name}: empty action')
        result['actions'].append({'name': action.name, 'channels': len(curves),
                                  'seconds': (action.frame_range[1] - action.frame_range[0]) / FPS,
                                  'loop': bool(action['loop'])})
    set_active_action(rig, actions[0])
    return result
