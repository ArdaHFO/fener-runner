"""Find semantic source meshes violating the planned slide corridor."""
from pathlib import Path
import sys,json
import bpy
ROOT=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(ROOT/'src'))
from model_curly_bird import build_curly_bird
from validate_obstacles import clipped_projection
bpy.ops.wm.read_factory_settings(use_empty=True)
built=build_curly_bird(str(ROOT/'qa'/'source_audit'))
bpy.context.view_layer.update()
results=[]
for obj in built['meshes']:
    obj.data.calc_loop_triangles()
    bad=[]
    for tri in obj.data.loop_triangles:
        world=[obj.matrix_world@obj.data.vertices[i].co for i in tri.vertices]
        points=[(p.x,p.z,-p.y) for p in world]
        clipped=clipped_projection(points,-.775+.0001,.775-.0001,.0001,1.66-.0001)
        if clipped:bad.append({'tri':tri.index,'points':points,'clipped':clipped})
    if bad:results.append({'object':obj.name,'intruding_triangles':len(bad),'examples':bad[:4]})
report={'passed':not results,'meshes':results,'total_intruding_triangles':sum(r['intruding_triangles'] for r in results)}
(ROOT/'qa'/'source_corridor_audit.json').write_text(json.dumps(report,indent=2))
print('SOURCE_CORRIDOR_AUDIT',json.dumps(report),flush=True)
