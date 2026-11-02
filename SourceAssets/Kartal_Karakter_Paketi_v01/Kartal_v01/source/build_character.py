"""Build, export and render the actual Kartal runner asset in Blender 4.5."""
from pathlib import Path
import sys, json, math, argparse
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
OUT = ROOT / 'output'
QA = ROOT / 'qa'
sys.path.insert(0, str(HERE))
from model_character import build_character
from rig_animations import build_rig, create_animations, set_active_action


def target(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def select_asset(meshes, rig):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [rig, *meshes]: obj.select_set(True)
    bpy.context.view_layer.objects.active = rig


def reset_pose(rig):
    rig.animation_data.action = None
    for track in rig.animation_data.nla_tracks: track.mute = True
    for bone in rig.pose.bones:
        bone.location = (0, 0, 0)
        bone.rotation_quaternion = (1, 0, 0, 0)
        bone.rotation_euler = (0, 0, 0)
        bone.scale = (1, 1, 1)
    rig.location = (0, 0, 0)
    rig.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()


def setup_stage():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 40
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 6
    scene.cycles.diffuse_bounces = 3
    scene.cycles.glossy_bounces = 3
    scene.render.resolution_x = 896
    scene.render.resolution_y = 896
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    scene.render.fps = 30
    scene.render.use_persistent_data = True
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'AgX - Medium High Contrast'
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.77, 0.81, 0.88, 1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.42
    stage = bpy.data.collections.new('Preview_Studio_NOT_GAME_ASSET')
    scene.collection.children.link(stage)
    def move_to_stage(obj):
        for collection in list(obj.users_collection): collection.objects.unlink(obj)
        stage.objects.link(obj)
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.018))
    ground = bpy.context.object; ground.name = 'Studio_Floor'
    move_to_stage(ground)
    mat = bpy.data.materials.new('Studio_Warm_Ivory')
    mat.use_nodes = True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.72, 0.71, 0.65, 1)
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.86
    ground.data.materials.append(mat)
    for name, position, power, size, tint in [
        ('Key_Softbox', (-3.6, -4.7, 5.8), 520, 4.0, (1.0, .89, .73)),
        ('Fill_Softbox', (3.7, -2.0, 3.7), 250, 3.4, (.82, .90, 1.0)),
        ('Rim_Softbox', (2.0, 3.7, 4.5), 650, 3.0, (1.0, .93, .79)),
    ]:
        bpy.ops.object.light_add(type='AREA', location=position)
        light = bpy.context.object; light.name = name
        light.data.energy = power; light.data.shape = 'DISK'; light.data.size = size; light.data.color = tint
        target(light, (0, 0, 1)); move_to_stage(light)
    bpy.ops.object.camera_add(location=(3.4, -6.2, 2.75))
    camera = bpy.context.object; camera.name = 'Preview_Camera'; camera.data.type = 'ORTHO'; camera.data.ortho_scale = 2.62
    target(camera, (0, -.04, 1.01)); move_to_stage(camera)
    scene.camera = camera
    return camera


def mesh_stats(meshes):
    triangles = vertices = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        vertices += len(obj.data.vertices)
    return {'mesh_objects': len(meshes), 'vertices': vertices, 'triangles': triangles,
            'material_slots': sum(len(obj.data.materials) for obj in meshes)}


def export_assets(meshes, rig, actions):
    reset_pose(rig)
    select_asset(meshes, rig)
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = max(int(a.frame_range[1]) for a in actions)
    bpy.ops.export_scene.gltf(
        filepath=str(OUT / 'Kartal_Runner_v01.glb'), export_format='GLB',
        use_selection=True, export_yup=True, export_apply=False,
        export_animations=True, export_animation_mode='ACTIONS',
        export_force_sampling=True, export_frame_step=1,
        export_skins=True, export_influence_nb=4, export_all_influences=False,
        export_leaf_bone=False, export_rest_position_armature=True,
        export_materials='EXPORT', export_cameras=False, export_lights=False,
        export_extras=True,
    )
    reset_pose(rig)
    select_asset(meshes, rig)
    bpy.ops.export_scene.fbx(
        filepath=str(OUT / 'Kartal_Runner_v01.fbx'), use_selection=True,
        object_types={'ARMATURE', 'MESH'}, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
        use_mesh_modifiers=True, add_leaf_bones=False, armature_nodetype='NULL',
        bake_anim=True, bake_anim_use_all_bones=True,
        bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, path_mode='COPY', embed_textures=True,
    )


def render_stills(rig, camera, action_map):
    scene = bpy.context.scene
    set_active_action(rig, action_map['Idle'])
    scene.frame_set(10)
    for name, position in [
        ('hero', (3.2, -6.3, 2.65)),
        ('front', (0, -7.0, 2.05)),
        ('side', (7.0, 0, 2.05)),
        ('back', (0, 7.0, 2.05)),
    ]:
        camera.location = position; target(camera, (0, -.025, 1.02))
        camera.data.ortho_scale = 2.58
        scene.render.filepath = str(OUT / f'Kartal_{name}.png')
        bpy.ops.render.render(write_still=True)
        print('RENDERED', name, flush=True)
    set_active_action(rig, action_map['Run'])
    scene.frame_set(7)
    camera.location = (3.8, -6.5, 2.5); target(camera, (0, -.03, 1.0))
    scene.render.filepath = str(OUT / 'Kartal_run_pose.png')
    bpy.ops.render.render(write_still=True)


def render_motion(rig, camera, action_map):
    scene = bpy.context.scene
    frames_dir = QA / 'motion_frames'; frames_dir.mkdir(parents=True, exist_ok=True)
    scene.render.resolution_x = 448; scene.render.resolution_y = 560
    scene.cycles.samples = 8
    segments = [('Run', 1.6), ('RunTurn', 1.0), ('Run', .8), ('JumpFlip', 1.0),
                ('Run', .8), ('Jump', .9), ('Slide', .8), ('CelebratePoint', 1.5)]
    fps = 24; timing = []; sequence = []
    camera.location = (3.5, -6.5, 2.5); target(camera, (0, 0, 1.20)); camera.data.ortho_scale = 3.00
    def capture():
        return {b.name: (b.location.copy(), b.rotation_quaternion.copy(), b.scale.copy()) for b in rig.pose.bones}
    def blend(a, b, factor):
        return {name: (a[name][0].lerp(b[name][0], factor),
                       a[name][1].slerp(b[name][1], factor),
                       a[name][2].lerp(b[name][2], factor)) for name in a}
    for name, seconds in segments:
        if name not in action_map: continue
        action = action_map[name]
        set_active_action(rig, action)
        start, end = action.frame_range
        count = max(1, round(seconds * fps))
        poses = []
        for index in range(count):
            t = index / max(1, count - 1)
            if name == 'Run':
                clip_frame = start + ((t * seconds * 30) % max(1, end - start))
            else: clip_frame = start + t * (end - start)
            scene.frame_set(int(clip_frame), subframe=clip_frame % 1)
            # Controller arcs are preview-only; the exported root stays in place.
            lift = (.92 if name == 'JumpFlip' else .56 if name == 'Jump' else 0) * math.sin(math.pi*t)
            poses.append({'pose': capture(), 'lift': lift, 'clip': name})
        if sequence:
            previous = sequence[-1]
            for step in range(1, 5):
                u = step / 5; alpha = u*u*(3-2*u)
                sequence.append({'pose': blend(previous['pose'], poses[0]['pose'], alpha),
                                 'lift': previous['lift']*(1-alpha)+poses[0]['lift']*alpha,
                                 'clip': name, 'crossfade': True})
        first = len(sequence)+1
        sequence.extend(poses)
        timing.append({'clip': name, 'first_frame': first, 'last_frame': len(sequence), 'seconds': count/fps})
    # Render the same rig using sampled clip poses and explicit short crossfades.
    # This demonstrates an animation-controller blend, not an extra exported clip.
    rig.animation_data.action = None
    for track in rig.animation_data.nla_tracks: track.mute = True
    for frame_out, frame in enumerate(sequence, 1):
        scene.frame_set(frame_out)
        for name, (location, rotation, scale) in frame['pose'].items():
            bone = rig.pose.bones[name]
            bone.location = location; bone.rotation_quaternion = rotation; bone.scale = scale
        rig.location.z = frame['lift']
        bpy.context.view_layer.update()
        scene.render.filepath = str(frames_dir / f'{frame_out:04d}.png')
        bpy.ops.render.render(write_still=True)
        if frame_out % 24 == 0: print('MOTION_PROGRESS', frame_out, len(sequence), flush=True)
    rig.location.z = 0
    (QA / 'motion_timing.json').write_text(json.dumps({'fps': fps, 'frame_count': len(sequence),
        'crossfade_seconds': 4/fps, 'controller_jump_arc': True, 'segments': timing}, indent=2))


def main():
    argv = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    parser = argparse.ArgumentParser(); parser.add_argument('--motion', action='store_true'); parser.add_argument('--skip-stills', action='store_true')
    args = parser.parse_args(argv)
    OUT.mkdir(parents=True, exist_ok=True); QA.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    bpy.context.scene.unit_settings.system = 'METRIC'; bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.scene.render.fps = 30
    built = build_character(str(OUT))
    meshes = built['meshes']
    rig = build_rig(meshes)
    # One skinned renderer with shared materials keeps the mobile asset manageable.
    if len(meshes) > 1:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in meshes: obj.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.join()
        combined = bpy.context.object
        combined.name = 'Kartal_Mesh'
        meshes = [combined]
    actions = create_animations(rig)
    if isinstance(actions, dict): actions = list(actions.values())
    action_map = {a.name: a for a in actions}
    camera = setup_stage()
    select_asset(meshes, rig)
    stats = mesh_stats(meshes)
    stats.update({'bone_count': len(rig.data.bones), 'blender_version': bpy.app.version_string,
                  'clips': [{'name': a.name, 'frames': list(a.frame_range), 'duration_seconds': (a.frame_range[1]-a.frame_range[0])/30} for a in actions]})
    (OUT / 'asset_manifest.json').write_text(json.dumps(stats, indent=2))
    print('ASSET_STATS', json.dumps(stats), flush=True)
    export_assets(meshes, rig, actions)
    set_active_action(rig, action_map['Idle']); bpy.context.scene.frame_set(1)
    select_asset(meshes, rig)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Kartal_Runner_v01.blend'))
    if not args.skip_stills: render_stills(rig, camera, action_map)
    if args.motion: render_motion(rig, camera, action_map)
    print('KARTAL_BUILD_COMPLETE', flush=True)


if __name__ == '__main__': main()
