"""Build, export and render the two reference-inspired static obstacle characters.

Blender 4.5 LTS. Source axes: Z up, -Y facing the viewer. Exports: Y up.
"""
from pathlib import Path
import argparse
import json
import sys
import math
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT/'output'
QA = ROOT/'qa'
sys.path.insert(0, str(ROOT/'src'))

SPECS = {
    'black': {'file':'Engel_Atlama_SiyahKus_v01','role':'jump_over','root_name':'Obstacle_Jump_BlackBird'},
    'curly': {'file':'Engel_Kayma_KivircikKus_v01','role':'slide_under','root_name':'Obstacle_Slide_CurlyBird'},
}

def target(obj, point):
    obj.rotation_euler = (Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()

def clean():
    if bpy.context.object and bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.scene.unit_settings.scale_length=1

def geometry_stats(meshes):
    points=[]; triangles=vertices=0; materials=set()
    for obj in meshes:
        obj.data.calc_loop_triangles()
        vertices+=len(obj.data.vertices); triangles+=len(obj.data.loop_triangles)
        points.extend(obj.matrix_world@v.co for v in obj.data.vertices)
        materials.update(m.name for m in obj.data.materials if m)
    lo=[min(p[i] for p in points) for i in range(3)]
    hi=[max(p[i] for p in points) for i in range(3)]
    return {'mesh_count':len(meshes),'vertices':vertices,'triangles':triangles,
            'material_count':len(materials),'materials':sorted(materials),
            'bounds_blender_z_up':{'min':lo,'max':hi,'size':[hi[i]-lo[i] for i in range(3)]}}

def select(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]

def stage():
    scene=bpy.context.scene
    scene.render.engine='CYCLES'; scene.cycles.device='CPU'
    scene.cycles.samples=24; scene.cycles.use_denoising=True
    scene.cycles.max_bounces=5; scene.cycles.diffuse_bounces=3; scene.cycles.glossy_bounces=3
    scene.render.resolution_x=896; scene.render.resolution_y=1024
    scene.render.resolution_percentage=100; scene.render.use_persistent_data=True
    scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False
    scene.view_settings.view_transform='AgX'; scene.view_settings.look='AgX - Medium High Contrast'
    if not scene.world: scene.world=bpy.data.worlds.new('Studio_World')
    scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.72,.79,.88,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
    collection=bpy.data.collections.new('Preview_Studio_NOT_GAME_ASSET')
    scene.collection.children.link(collection)
    def move(obj):
        for coll in list(obj.users_collection): coll.objects.unlink(obj)
        collection.objects.link(obj)
    bpy.ops.mesh.primitive_plane_add(size=100,location=(0,0,-.008))
    floor=bpy.context.object; floor.name='Studio_Floor'; move(floor)
    mat=bpy.data.materials.new('Studio_Neutral'); mat.use_nodes=True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.68,.70,.71,1)
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.88
    floor.data.materials.append(mat)
    for name,pos,power,size,color in [
        ('Key',(-3.5,-5,6.5),850,4.5,(1,.93,.83)),
        ('Fill',(4,-2,4),500,4,(.81,.9,1)),
        ('Rim',(2,3.5,5),1000,3.5,(1,.94,.80)),
    ]:
        bpy.ops.object.light_add(type='AREA',location=pos)
        obj=bpy.context.object; obj.name='Studio_'+name; move(obj)
        obj.data.energy=power; obj.data.shape='DISK'; obj.data.size=size; obj.data.color=color
        target(obj,(0,0,1.3))
    bpy.ops.object.camera_add(location=(3.2,-7,3.4))
    camera=bpy.context.object; camera.name='Preview_Camera'; camera.data.type='ORTHO'; move(camera)
    scene.camera=camera
    return camera

def render_asset(kind, camera, stats):
    spec=SPECS[kind]; scene=bpy.context.scene
    bounds=stats['bounds_blender_z_up']; width,depth,height=bounds['size']
    center=(0,(bounds['min'][1]+bounds['max'][1])/2,height*.5)
    scale=max(height*1.20,width/scene.render.resolution_x*scene.render.resolution_y*1.20)
    camera.data.ortho_scale=scale
    for view,offset in [('hero',(3.1,-7,2.7)),('front',(0,-8,1.8)),('side',(8,-.2,1.9))]:
        distance=max(1.5,height)
        camera.location=Vector(center)+Vector(offset)*distance*.65
        target(camera,center)
        scene.render.filepath=str(OUT/(spec['file']+'_'+view+'.png'))
        bpy.ops.render.render(write_still=True)
        print('RENDERED',kind,view,flush=True)

def build_one(kind, renders=True):
    clean()
    if kind=='black':
        from model_black_bird import build_black_bird
        built=build_black_bird(str(OUT))
    else:
        from model_curly_bird import build_curly_bird
        built=build_curly_bird(str(OUT))
    root=built['root']; meshes=built['meshes']; spec=SPECS[kind]
    if len(meshes)>1:
        select(meshes); bpy.ops.object.join()
        meshes=[bpy.context.object]; meshes[0].name=spec['root_name']+'_Mesh'
    root.name=spec['root_name']
    root['obstacle_role']=spec['role']; root['units']='meters'; root['source_forward']='-Y'; root['source_up']='Z'
    if kind=='curly':
        root['opening_width']=1.55; root['opening_height']=1.66
        root['collider_note']='Use two side boxes and a header; never fill the central opening.'
    else: root['collider_note']='Low crouched static obstacle; jump over.'
    stats=geometry_stats(meshes)
    stats.update({'id':kind,'file_stem':spec['file'],'role':spec['role'],
                  'static_posed_mesh':True,'armature_count':0,'animation_count':0,
                  'blender_version':bpy.app.version_string,
                  'texture_files':[str(Path(p).relative_to(OUT)) for p in built.get('texture_paths',[])],
                  'reference_note':'Single reference image interpreted as a stylized 3D obstacle. Unseen back details are designed.'})
    (QA/(kind+'_build.json')).write_text(json.dumps(stats,indent=2))
    select([root,*meshes])
    bpy.ops.export_scene.gltf(filepath=str(OUT/(spec['file']+'.glb')),export_format='GLB',
        use_selection=True,export_yup=True,export_apply=True,export_materials='EXPORT',
        export_animations=False,export_skins=False,export_cameras=False,export_lights=False,export_extras=True)
    select([root,*meshes])
    bpy.ops.export_scene.fbx(filepath=str(OUT/(spec['file']+'.fbx')),use_selection=True,
        object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=True,bake_anim=False,
        path_mode='COPY',embed_textures=True,use_custom_props=True)
    camera=stage()
    h=stats['bounds_blender_z_up']['size'][2]
    camera.location=(3.1,-7,h+1); camera.data.ortho_scale=max(h*1.2,2)
    target(camera,(0,0,h*.5))
    select([root,*meshes]); bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(spec['file']+'.blend')))
    print('ASSET_READY',json.dumps(stats),flush=True)
    if renders: render_asset(kind,camera,stats)
    return stats

def collider_config(stats_by_kind):
    result={'schema_version':1,'units':'meters','axis_system':'Y up, X right, Z forward (Unity/glTF)',
            'origin':'floor under center of obstacle','scope':'Suggested local box colliders; actual controller dimensions and Unity playtesting remain required.',
            'assets':[]}
    for kind,stats in stats_by_kind.items():
        b=stats['bounds_blender_z_up']; lo=b['min']; hi=b['max']
        x0,x1=lo[0],hi[0]; y0,y1=lo[2],hi[2]; z0,z1=-hi[1],-lo[1]
        centerz=(z0+z1)*.5; depth=z1-z0
        asset={'id':kind,'file_stem':stats['file_stem'],'required_action':stats['role'],
               'visual_bounds_y_up':{'min':[x0,y0,z0],'max':[x1,y1,z1]},'boxes':[]}
        if kind=='black':
            asset['boxes']=[{'name':'Body','center':[(x0+x1)/2,(y0+y1)/2,centerz],
                             'size':[x1-x0,y1-y0,depth]}]
            asset['obstacle_top']=y1
        else:
            half=.775; opening=1.66
            asset['free_opening']={'width':2*half,'height':opening,'min':[-half,0,z0],'max':[half,opening,z1]}
            asset['boxes']=[
                {'name':'LeftLeg','center':[(x0-half)/2,opening/2,centerz],'size':[-half-x0,opening,depth]},
                {'name':'RightLeg','center':[(half+x1)/2,opening/2,centerz],'size':[x1-half,opening,depth]},
                {'name':'TorsoHeader','center':[(x0+x1)/2,(opening+y1)/2,centerz],'size':[x1-x0,y1-opening,depth]},
            ]
            asset['do_not_use_enclosing_box_or_convex_hull']=True
        asset['recommended_lane_spacing_min']=round(max(2.0,x1-x0+.2),2)
        result['assets'].append(asset)
    (OUT/'collider_layout.json').write_text(json.dumps(result,indent=2))
    return result

def main():
    argv=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    p=argparse.ArgumentParser(); p.add_argument('--only',choices=['all','black','curly'],default='all')
    p.add_argument('--skip-renders',action='store_true'); args=p.parse_args(argv)
    OUT.mkdir(parents=True,exist_ok=True); QA.mkdir(parents=True,exist_ok=True)
    for kind in (['black','curly'] if args.only=='all' else [args.only]): build_one(kind,not args.skip_renders)
    reports={kind:json.loads((QA/(kind+'_build.json')).read_text()) for kind in SPECS if (QA/(kind+'_build.json')).exists()}
    (OUT/'asset_manifest.json').write_text(json.dumps({'assets':reports},indent=2))
    collider_config(reports)
    print('OBSTACLE_BUILD_COMPLETE',flush=True)

if __name__=='__main__': main()
