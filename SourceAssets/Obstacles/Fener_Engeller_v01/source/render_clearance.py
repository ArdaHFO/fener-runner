"""Show the actual exported obstacles beside the existing player's action poses.

This is a Blender geometry illustration, not an engine/phone collision test.
"""
from pathlib import Path
import sys
import argparse
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(ROOT/'src'))
from build_obstacles import clean, stage, target, OUT, SPECS

def load_player(path, action_name):
    with bpy.data.libraries.load(str(path),link=False) as (source,dest):
        dest.objects=['Kartal_Rig','Kartal_Mesh']
    for obj in dest.objects:
        if obj is not None: bpy.context.scene.collection.objects.link(obj)
    rig=next(obj for obj in dest.objects if obj and obj.type=='ARMATURE')
    anim=rig.animation_data
    action=None
    for track in anim.nla_tracks:
        track.mute=True; track.is_solo=False
        for strip in track.strips:
            if strip.action.name.split('.')[0]==action_name: action=strip.action
    if action is None: raise RuntimeError('Player action not found: '+action_name)
    anim.action=None
    for bone in rig.pose.bones:
        bone.location=(0,0,0); bone.rotation_mode='QUATERNION'; bone.rotation_quaternion=(1,0,0,0); bone.scale=(1,1,1)
    anim.action=action
    if action.slots: anim.action_slot=action.slots[0]
    anim.action_blend_type='REPLACE'; anim.action_influence=1
    return rig,action

def main():
    argv=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    p=argparse.ArgumentParser(); p.add_argument('--player-blend',type=Path,required=True); args=p.parse_args(argv)
    for kind,action_name,preview_name in [('curly','Slide','Kayma_Gecis_Onizleme'),('black','JumpFlip','Atlama_Gecis_Onizleme')]:
        clean()
        bpy.ops.import_scene.gltf(filepath=str(OUT/(SPECS[kind]['file']+'.glb')))
        rig,action=load_player(args.player_blend,action_name)
        midpoint=sum(action.frame_range)*.5
        bpy.context.scene.frame_set(int(midpoint),subframe=midpoint%1)
        rig.location=(0,-.02,0 if kind=='curly' else 2.1)
        camera=stage()
        scene=bpy.context.scene
        scene.cycles.samples=24; scene.render.resolution_x=1024; scene.render.resolution_y=1152
        camera.location=(3.6,-9,4.2); target(camera,(0,0,1.78)); camera.data.ortho_scale=4.35
        scene.render.filepath=str(OUT/(preview_name+'.png'))
        bpy.context.view_layer.update(); bpy.ops.render.render(write_still=True)
        print('CLEARANCE_PREVIEW_READY',kind,flush=True)

if __name__=='__main__': main()
