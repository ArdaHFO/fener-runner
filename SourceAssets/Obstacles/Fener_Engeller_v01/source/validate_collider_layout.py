"""Validate the proposed static Unity box layout against measured GLB geometry.

This does not instantiate Unity colliders or simulate gameplay.
"""
from pathlib import Path
import argparse,json,math

ROOT=Path(__file__).resolve().parent.parent

def box_bounds(box):
    c,s=box['center'],box['size']
    if len(c)!=3 or len(s)!=3 or any(not math.isfinite(v) for v in c+s) or min(s)<=0:
        raise ValueError('invalid box center or size')
    return [c[i]-s[i]/2 for i in range(3)],[c[i]+s[i]/2 for i in range(3)]

def near(a,b):return max(abs(x-y)for x,y in zip(a,b))<=1e-5

def inspect(output,player_path,jump_path):
    config=json.loads((output/'collider_layout.json').read_text())
    player=json.loads(player_path.read_text());jump=json.loads(jump_path.read_text())
    report={'passed':False,'errors':[],'assets':[],
            'scope':'Geometry/layout feasibility only. Unity collision components, controller size, timing, lane transitions and device performance are not tested.'}
    errors=report['errors']
    if config.get('units')!='meters':errors.append('units must be meters')
    assets=config.get('assets',[])
    if {a['id']for a in assets}!={'black','curly'}:errors.append('expected exactly black/curly assets')
    for asset in assets:
        key=asset['id'];local_errors=[]
        validation=json.loads((output/('BlackBird_validation.json'if key=='black'else'CurlyBird_validation.json')).read_text())
        lo,hi=validation['bounds']['min'],validation['bounds']['max']
        stated=asset['visual_bounds_y_up']
        if not near(stated['min'],lo)or not near(stated['max'],hi):local_errors.append('stated visual bounds are stale/different from final GLB')
        boxes={b['name']:box_bounds(b)for b in asset['boxes']}
        rec={'id':key,'box_count':len(boxes),'boxes':{name:{'min':a,'max':b}for name,(a,b)in boxes.items()}}
        if key=='black':
            if set(boxes)!={'Body'}:local_errors.append('black should have one Body box')
            else:
                blo,bhi=boxes['Body']
                if not near(blo,lo)or not near(bhi,hi):local_errors.append('black Body does not match conservative visual bounds')
            if abs(asset['obstacle_top']-hi[1])>1e-5:local_errors.append('black obstacle_top differs from GLB')
            rec['jump_geometry_recommendation']=jump
        else:
            opening=asset['free_opening'];w,h=opening['width'],opening['height'];half=w/2
            if set(boxes)!={'LeftLeg','RightLeg','TorsoHeader'}:local_errors.append('curly needs two side boxes plus header')
            expected={
                'LeftLeg':([lo[0],lo[1],lo[2]],[-half,h,hi[2]]),
                'RightLeg':([half,lo[1],lo[2]],[hi[0],h,hi[2]]),
                'TorsoHeader':([lo[0],h,lo[2]],[hi[0],hi[1],hi[2]])}
            for name,(a,b)in expected.items():
                if name not in boxes:continue
                actual_lo,actual_hi=boxes[name]
                if not near(actual_lo,a)or not near(actual_hi,b):local_errors.append(name+' differs from conservative gate partition')
                if actual_hi[0]>-half+1e-5 and actual_lo[0]<half-1e-5 and actual_lo[1]<h-1e-5 and actual_hi[1]>1e-5:
                    local_errors.append(name+' closes the slide corridor')
            if not near(opening['min'],[-half,0,lo[2]])or not near(opening['max'],[half,h,hi[2]]):local_errors.append('opening depth/bounds differ from final GLB')
            if not asset.get('do_not_use_enclosing_box_or_convex_hull'):local_errors.append('missing instruction to preserve the arch in collision geometry')
            gate=validation['slide_corridor']
            if not gate['passed']or not gate['slide_hold_geometry_fits']:local_errors.append('final GLB does not satisfy opening/player geometry')
            rec['free_width']=w;rec['free_height']=h
            rec['slide_hold_margins']={k:gate[k]for k in ['left_margin','right_margin','top_margin']}
            rec['source_slide_sampled_fit_interval_seconds']=player['slide_motion_profile']['sampled_fit_interval_seconds']
            rec['suggested_initial_slide_capsule']={'max_height':1.55,'max_diameter':1.30,'units':'meters at source scale','engine_tested':False}
        rec['passed']=not local_errors;rec['errors']=local_errors
        errors.extend(key+': '+e for e in local_errors);report['assets'].append(rec)
    report['passed']=not errors
    return report

def main():
    p=argparse.ArgumentParser();p.add_argument('--output',type=Path,default=ROOT/'output')
    p.add_argument('--player-report',type=Path,default=ROOT/'qa'/'player_clearance.json')
    p.add_argument('--jump-report',type=Path,default=ROOT/'qa'/'jump_clearance_recommendation.json')
    p.add_argument('--report',type=Path,default=ROOT/'output'/'Collision_validation.json')
    a=p.parse_args();r=inspect(a.output,a.player_report,a.jump_report)
    a.report.write_text(json.dumps(r,indent=2));print(json.dumps(r,indent=2))
    raise SystemExit(0 if r['passed'] else 1)

if __name__=='__main__':main()
