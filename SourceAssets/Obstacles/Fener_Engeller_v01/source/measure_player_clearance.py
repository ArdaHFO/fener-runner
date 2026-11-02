"""Measure source-player poses. Open the player's .blend before this script.

blender --background Kartal_Runner_v01.blend --python source/measure_player_clearance.py
"""
from pathlib import Path
import sys, json
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parent.parent
(ROOT/'qa').mkdir(parents=True,exist_ok=True)

def set_active_action(rig, name):
    action=bpy.data.actions[name]
    anim=rig.animation_data_create()
    anim.action=None
    for track in anim.nla_tracks:track.mute=True
    for bone in rig.pose.bones:
        bone.location=(0,0,0);bone.rotation_mode='QUATERNION'
        bone.rotation_quaternion=(1,0,0,0);bone.scale=(1,1,1)
    rig.location=(0,0,0);rig.rotation_euler=(0,0,0);rig.scale=(1,1,1)
    anim.action=action
    if hasattr(action,'slots') and action.slots:anim.action_slot=action.slots[0]
    return action

rig=bpy.data.objects['Kartal_Rig']
mesh=bpy.data.objects['Kartal_Mesh']

def bounds(action, frame):
    set_active_action(rig, action)
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    dep=bpy.context.evaluated_depsgraph_get()
    evaluated=mesh.evaluated_get(dep)
    points=[evaluated.matrix_world@v.co for v in evaluated.data.vertices]
    lo=[min(p[i] for p in points) for i in range(3)]
    hi=[max(p[i] for p in points) for i in range(3)]
    layers=[]
    for zmin,zmax in [(0,.3),(.3,.6),(.6,.9),(.9,1.2),(1.2,1.5),(1.5,2.1)]:
        selected=[p for p in points if zmin<=p.z<=zmax]
        if selected: layers.append({'z':[zmin,zmax],'x_min':min(p.x for p in selected),'x_max':max(p.x for p in selected),'y_min':min(p.y for p in selected),'y_max':max(p.y for p in selected)})
    return {'action':action,'frame':frame,'min':lo,'max':hi,'size':[hi[i]-lo[i] for i in range(3)],'layers':layers}

result={'coordinate_system':'Blender metres, X lateral, Y travel, Z up; forward -Y',
        'poses':[bounds(name,frame) for name,frame in [('Idle',1),('Run',1),('Run',7),('Run',13),('Run',19),('Slide',7),('Slide',12),('Slide',17),('Jump',14),('JumpFlip',16)]]}
slide=[r for r in result['poses'] if r['action']=='Slide']
result['slide_hold_envelope']={'x_min':min(p['min'][0] for p in slide),'x_max':max(p['max'][0] for p in slide),'z_min':min(p['min'][2] for p in slide),'z_max':max(p['max'][2] for p in slide),'y_min':min(p['min'][1] for p in slide),'y_max':max(p['max'][1] for p in slide)}
import math
profiles={}
for name in ['Jump','JumpFlip']:
    action=set_active_action(rig,name)
    start,end=action.frame_range
    samples=[]
    for index in range(121):
        t=index/120
        frame=start+t*(end-start)
        bpy.context.scene.frame_set(int(frame),subframe=frame%1)
        bpy.context.view_layer.update()
        evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        points=[evaluated.matrix_world@v.co for v in evaluated.data.vertices]
        min_z=min(p.z for p in points)
        samples.append({'t':t,'relative_min_z':min_z,'relative_max_z':max(p.z for p in points),
                        'depth':max(p.y for p in points)-min(p.y for p in points),
                        'min_z_with_h_2_05_sine_arc':min_z+2.05*math.sin(math.pi*t),
                        'min_z_with_h_2_10_sine_arc':min_z+2.10*math.sin(math.pi*t)})
    crossing=[s for s in samples if .35<=s['t']<=.65]
    profiles[name]={'samples':samples,
        'assumed_obstacle_top':1.0,'clearance_margin':.10,'assumed_overlap_t':[.35,.65],
        'required_sine_arc_h_over_crossing':max((1.10-s['relative_min_z'])/math.sin(math.pi*s['t']) for s in crossing),
        'minimum_crossing_z_h2_05':min(s['min_z_with_h_2_05_sine_arc'] for s in crossing),
        'minimum_crossing_z_h2_10':min(s['min_z_with_h_2_10_sine_arc'] for s in crossing)}
result['jump_profiles']=profiles
action=set_active_action(rig,'Slide')
start,end=action.frame_range
slide_samples=[]
for index in range(121):
    t=index/120;frame=start+t*(end-start)
    bpy.context.scene.frame_set(int(frame),subframe=frame%1)
    bpy.context.view_layer.update()
    evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    points=[evaluated.matrix_world@v.co for v in evaluated.data.vertices]
    xlo,xhi=min(p.x for p in points),max(p.x for p in points)
    zhi=max(p.z for p in points)
    slide_samples.append({'t':t,'seconds':t*.8,'x_min':xlo,'x_max':xhi,'z_max':zhi,
                          'fits_1_55_by_1_66':xlo>=-.775 and xhi<=.775 and zhi<=1.66})
fits=[s for s in slide_samples if s['fits_1_55_by_1_66']]
result['slide_motion_profile']={'samples':slide_samples,
    'sampled_fit_interval_normalized':[fits[0]['t'],fits[-1]['t']],
    'sampled_fit_interval_seconds':[fits[0]['seconds'],fits[-1]['seconds']],
    'note':'Sampled source action only; transition blending and world crossing timing are not included.'}
(ROOT/'qa'/'player_clearance.json').write_text(json.dumps(result,indent=2))
print('PLAYER_CLEARANCE',json.dumps({'slide_hold_envelope':result['slide_hold_envelope'], 'jump_summary':{name:{k:v for k,v in p.items() if k!='samples'} for name,p in profiles.items()},'slide_fit_interval_seconds':result['slide_motion_profile']['sampled_fit_interval_seconds']}),flush=True)
