"""Clean Blender FBX re-import of the two static obstacle assets.

blender --background --factory-startup --python-exit-code 1 --python src/validate_obstacle_fbx.py -- --output output --report qa/fbx_roundtrip.json
"""
import argparse,json,math,sys
from pathlib import Path
import bpy

STEMS=['Engel_Atlama_SiyahKus_v01','Engel_Kayma_KivircikKus_v01']

def inspect(path, expected=None):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    report={'file':str(path),'passed':False,'errors':[],'warnings':[]}
    errors=report['errors']
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False,use_image_search=False)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.context.view_layer.update()
    points=[];records=[];triangles=0;signed_volume=0.0
    for o in meshes:
        o.data.calc_loop_triangles()
        world=[o.matrix_world@v.co for v in o.data.vertices]
        points.extend(world)
        triangles+=len(o.data.loop_triangles)
        normals=[o.matrix_world.to_3x3().inverted().transposed()@v.normal for v in o.data.vertices]
        normal_error=max((abs(v.length-1.0) for v in normals),default=0)
        if normal_error>.01:errors.append(o.name+': invalid normal lengths')
        volume=sum(world[t.vertices[0]].dot(world[t.vertices[1]].cross(world[t.vertices[2]]))/6 for t in o.data.loop_triangles)
        signed_volume+=volume
        records.append({'name':o.name,'vertices':len(o.data.vertices),'triangles':len(o.data.loop_triangles),
                        'material_slots':len(o.material_slots),'normal_max_length_error':normal_error,
                        'signed_triangle_volume_m3':volume})
    if not meshes or not triangles:errors.append('no mesh geometry')
    if any(not math.isfinite(v) for p in points for v in p):errors.append('nonfinite world geometry')
    if signed_volume<=0:errors.append('aggregate signed mesh volume is not outward-positive')
    lo=[min(p[i] for p in points)for i in range(3)]
    hi=[max(p[i] for p in points)for i in range(3)]
    report.update({'blender_version':bpy.app.version_string,'file_bytes':path.stat().st_size,
        'mesh_count':len(meshes),'meshes':records,'triangles':triangles,
        'vertex_count':sum(len(o.data.vertices)for o in meshes),'bounds_blender_z_up':{'min':lo,'max':hi},
        'armature_count':sum(o.type=='ARMATURE' for o in bpy.context.scene.objects),
        'scope':'Static FBX integrity after Blender re-import; not a Unity/device test.'})
    if expected:
        if triangles!=expected['triangles_instanced']:errors.append('triangle count differs from GLB')
        expected_lo,expected_hi=expected['bounds']['min'],expected['bounds']['max']
        glb_equivalent_lo=[lo[0],lo[2],-hi[1]]
        glb_equivalent_hi=[hi[0],hi[2],-lo[1]]
        error=max(abs(a-b) for a,b in zip(glb_equivalent_lo+glb_equivalent_hi,expected_lo+expected_hi))
        report['bounds_max_error_vs_glb']=error
        if error>.001:errors.append('FBX bounds differ from GLB by >1mm')
    report['passed']=not errors
    return report

def main():
    p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--report',type=Path,required=True)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--'in sys.argv else [])
    report={'passed':False,'assets':[]}
    for stem,qaname in zip(STEMS,['BlackBird_validation.json','CurlyBird_validation.json']):
        expected=json.loads((args.output/qaname).read_text())
        report['assets'].append(inspect((args.output/(stem+'.fbx')).resolve(),expected))
    report['passed']=all(a['passed'] for a in report['assets'])
    args.report.write_text(json.dumps(report,indent=2))
    print('OBSTACLE_FBX_CHECK',json.dumps(report),flush=True)
    if not report['passed']:raise RuntimeError('Static FBX verification failed')

if __name__=='__main__':main()
