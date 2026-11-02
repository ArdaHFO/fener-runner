"""Reference-inspired curly-haired bird in a wide slide-under gate pose.

Static, editable geometry. Blender Z up, front -Y. The central passage is
deliberately free below Z=1.66 and inside abs(X)<0.775, at every depth.
No game, collider or animation code is created by this module.
"""
from pathlib import Path
import math
import random
import struct
import zlib

import bpy
import numpy as np
from mathutils import Vector


def _linear(v):
    return v / 12.92 if v < .04045 else ((v+.055)/1.055)**2.4


def _rgba(hex_color):
    value = hex_color.lstrip('#')
    return tuple(_linear(int(value[i:i+2], 16)/255) for i in (0, 2, 4)) + (1,)


def _material(name, color, roughness=.70):
    mat = bpy.data.materials.new('CB_'+name)
    mat.use_nodes = True
    mat.diffuse_color = _rgba(color)
    shader = mat.node_tree.nodes['Principled BSDF']
    shader.inputs['Base Color'].default_value = _rgba(color)
    shader.inputs['Roughness'].default_value = roughness
    return mat


def _write_png(path, pixels):
    h, w, _ = pixels.shape
    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag+data)&0xffffffff)
    raw = b''.join(b'\x00'+pixels[row].tobytes() for row in range(h))
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB',w,h,8,2,0,0,0))
                     + chunk(b'IDAT',zlib.compress(raw,6)) + chunk(b'IEND',b''))


def _jersey_material(folder):
    size = 1024
    yy, xx = np.mgrid[0:size, 0:size]
    u, v = xx/(size-1), 1-yy/(size-1)
    # Fourteen circumference bands give seven broad stripes across each view.
    dark = (np.floor(u*14+0.5).astype(int)%2)==0
    yellow = np.array([239,199,37],dtype=float)
    navy = np.array([20,39,72],dtype=float)
    rgb = np.where(dark[...,None],navy,yellow)
    knit = 1 + .012*np.sin(xx*math.tau/5)*np.sin(yy*math.tau/4)
    rgb = np.clip(rgb*knit[...,None],0,255)
    # Tiny round crest, based on the reference's visible white-rimmed badge.
    # Deliberately no fabricated sponsor lettering or unreadable pseudo text.
    bx, by = (u-.836)/.027, (v-.695)/.083
    rr = bx*bx+by*by
    rgb[rr<1.0] = (242,238,211)
    rgb[rr<.72] = (19,37,65)
    rgb[rr<.57] = (242,231,180)
    inside = rr<.44
    rgb[inside & (by>.22)] = (157,38,43)
    rgb[inside & (by<=.22)] = (230,185,42)
    rgb[inside & (by<=.22) & (bx<-.14)] = (22,45,78)
    rgb[inside & (by<=.22) & (bx>.14)] = (22,45,78)
    leaf = ((bx/.21)**2 + ((by+.03)/.45)**2 < 1) & (rr<.44)
    rgb[leaf] = (40,91,49)
    path = folder/'curly_bird_jersey_basecolor.png'
    _write_png(path, rgb.astype(np.uint8))
    image = bpy.data.images.load(str(path), check_existing=False)
    image.name = 'CB_Jersey_Stripes_And_Crest'
    image.colorspace_settings.name = 'sRGB'
    image.pack()
    mat = _material('Striped_Jersey', '#EFC725',.87)
    node = mat.node_tree.nodes.new('ShaderNodeTexImage')
    node.image = image; node.interpolation='Linear'
    mat.node_tree.links.new(node.outputs['Color'],mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    return mat, str(path)


class _Builder:
    def __init__(self, collection):
        self.collection = collection
        self.objects = []

    def finish(self, obj, name, mat):
        obj.name = 'CB_'+name
        for col in list(obj.users_collection): col.objects.unlink(obj)
        self.collection.objects.link(obj)
        obj.data.materials.clear(); obj.data.materials.append(mat)
        for face in obj.data.polygons: face.use_smooth=True
        self.objects.append(obj)
        return obj

    def sphere(self, name, center, radii, mat, segments=28, rings=16, direction=None):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=center)
        obj=bpy.context.object; obj.scale=radii
        if direction:
            obj.rotation_mode='QUATERNION'
            obj.rotation_quaternion=Vector(direction).to_track_quat('Z','Y')
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        return self.finish(obj,name,mat)

    def ico(self,name,center,radii,mat,subdivisions=2):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions,radius=1,location=center)
        obj=bpy.context.object; obj.scale=radii
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        return self.finish(obj,name,mat)

    def mesh(self,name,vertices,faces,mat,uv=None):
        mesh=bpy.data.meshes.new('CB_'+name+'_Mesh'); mesh.from_pydata(vertices,[],faces); mesh.update()
        obj=bpy.data.objects.new('CB_'+name,mesh); self.collection.objects.link(obj)
        if uv is not None:
            layer=mesh.uv_layers.new(name='UVMap')
            for poly in mesh.polygons:
                for index in poly.loop_indices: layer.data[index].uv=uv[mesh.loops[index].vertex_index]
        return self.finish(obj,name,mat)

    def lathe(self,name,profile,mat,sectors=64):
        verts=[]; faces=[]; uv=[]; low,high=profile[0][0],profile[-1][0]
        for z,rx,ry,cy in profile:
            for j in range(sectors+1):
                angle=math.tau*j/sectors
                # Small, regular shirt drapes without a heavyweight cloth sim.
                ripple=1+.006*math.cos(angle*14)
                verts.append((rx*math.cos(angle)*ripple,cy+ry*math.sin(angle)*ripple,z))
                uv.append((j/sectors,(z-low)/(high-low)))
        row=sectors+1
        for i in range(len(profile)-1):
            for j in range(sectors):
                a=i*row+j; faces.append((a,a+1,a+row+1,a+row))
        faces.append(tuple(reversed(range(sectors))))
        faces.append(tuple((len(profile)-1)*row+j for j in range(sectors)))
        return self.mesh(name,verts,faces,mat,uv)

    def tube(self,name,centers,radii,mat,sides=16,uvscale=(1,1)):
        points=[Vector(p) for p in centers]; verts=[]; faces=[]; uv=[]
        for i,p in enumerate(points):
            tangent=(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized()
            ref=Vector((0,1,0)) if abs(tangent.y)<.94 else Vector((1,0,0))
            u=(ref-tangent*ref.dot(tangent)).normalized(); v=tangent.cross(u).normalized()
            radius=radii[i]; depth,width=radius if isinstance(radius,(tuple,list)) else (radius,radius)
            for j in range(sides+1):
                a=math.tau*j/sides
                verts.append(tuple(p+u*(math.cos(a)*depth)+v*(math.sin(a)*width)))
                uv.append((j/sides*uvscale[0],i/(len(points)-1)*uvscale[1]))
        row=sides+1
        for i in range(len(points)-1):
            for j in range(sides):
                a=i*row+j; faces.append((a,a+1,a+row+1,a+row))
        faces.append(tuple(reversed(range(sides))))
        faces.append(tuple((len(points)-1)*row+j for j in range(sides)))
        return self.mesh(name,verts,faces,mat,uv)

    def beak(self,name,sections,mat):
        verts=[]; faces=[]; sides=24
        for y,width,top,bottom in sections:
            for j in range(sides+1):
                a=math.tau*j/sides
                verts.append((width*math.cos(a),y,(top+bottom)/2+math.sin(a)*(top-bottom)/2))
        row=sides+1
        for i in range(len(sections)-1):
            for j in range(sides):
                a=i*row+j; faces.append((a,a+1,a+row+1,a+row))
        faces.append(tuple(reversed(range(sides))))
        faces.append(tuple((len(sections)-1)*row+j for j in range(sides)))
        return self.mesh(name,verts,faces,mat)


def _hair(builder,materials):
    """Shoulder-length, layered wavy locks with real loose spiral curls.

    Each lock follows the scalp toward a shoulder instead of attaching a round
    ball to a little top loop. Thin overlapping locks retain a soft silhouette.
    """
    rng=random.Random(1907)
    center=Vector((0,.045,3.015)); radii=Vector((.555,.425,.565))
    def limit(angle):
        front=max(0,-math.sin(angle))
        return 2.70-1.75*front**4
    def surface(angle,phi):
        return center+Vector((radii.x*math.sin(phi)*math.cos(angle),
                              radii.y*math.sin(phi)*math.sin(angle),radii.z*math.cos(phi)))
    verts=[]; faces=[]; sectors=48; rows=15
    for row in range(rows+1):
        for j in range(sectors+1):
            a=math.tau*j/sectors
            phi=.001+(limit(a)-.001)*row/rows
            verts.append(tuple(surface(a,phi)))
    stride=sectors+1
    for i in range(rows):
        for j in range(sectors):
            k=i*stride+j; faces.append((k,k+stride,k+stride+1,k+1))
    builder.mesh('Curly_Hair_Base',verts,faces,materials['hair'])
    index=0
    def lock(angle,begin,end,turns,steps,layer,thickness):
        nonlocal index
        phase=rng.uniform(0,math.tau)
        points=[]; widths=[]
        swing=rng.uniform(.025,.055)
        for step in range(steps):
            t=step/(steps-1)
            a=angle+swing*math.sin(math.pi*t)
            phi=begin+(end-begin)*t
            normal=Vector((math.sin(phi)*math.cos(a),math.sin(phi)*math.sin(a),math.cos(phi))).normalized()
            meridian=Vector((radii.x*math.cos(phi)*math.cos(a),
                             radii.y*math.cos(phi)*math.sin(a),-radii.z*math.sin(phi))).normalized()
            across=normal.cross(meridian).normalized()
            radius=(.040+.009*math.sin(math.pi*t))*(.75+.25*math.sin(math.pi*t))
            curl=phase+turns*math.tau*t
            p=surface(a,phi)+normal*(.030+layer+radius*math.cos(curl))+across*(radius*math.sin(curl))
            # Side/back ringlets settle down toward the jersey's shoulders.
            if end>2.0:p.z-=.085*t*t
            points.append(tuple(p))
            widths.append(thickness*(.78+.22*math.sin(math.pi*t)))
        mat=materials['hair_highlight'] if index%3==0 else materials['hair']
        builder.tube(f'Flowing_Curl_Lock_{index:03d}',points,widths,mat,sides=5)
        index+=1
    # Overlapping crown locks soften the top, with an open forehead/fringe.
    for j in range(24):
        a=math.tau*j/24
        lock(a,.07,min(limit(a),1.52),2.5,23,.006,.025)
    # Long ringlets run from the upper sides down toward the shoulders.
    for j in range(30):
        a=math.tau*(j+.34)/30
        stop=limit(a)
        if stop<1.45:continue
        lock(a,.80,stop,3.8,32,.015,.027)
    # A second irregular layer fills seams without spherical hair clumps.
    for j in range(19):
        a=math.tau*(j+.70)/19
        stop=limit(a)
        if stop<1.75:continue
        lock(a,1.11,stop,3.0,27,.038,.024)


def build_curly_bird(output_dir):
    out=Path(output_dir); textures=out/'textures'; textures.mkdir(parents=True,exist_ok=True)
    collection=bpy.data.collections.new('CB_Curly_Bird_Obstacle')
    bpy.context.scene.collection.children.link(collection)
    root=bpy.data.objects.new('CB_Slide_CurlyBird',None); collection.objects.link(root)
    root.empty_display_type='PLAIN_AXES'; root.empty_display_size=.25
    m={
        'yellow':_material('Golden_Feathers','#E2B846',.83),
        'beak':_material('Warm_Beak_And_Feet','#DCA047',.67),
        'hair':_material('Deep_Curly_Hair','#0F0E0D',.84),
        'hair_highlight':_material('Warm_Curl_Ridges','#191714',.78),
        'white':_material('Warm_Eye_White','#F5EBD1',.48),
        'iris':_material('Brown_Iris','#70482A',.43),
        'pupil':_material('Glossy_Pupil','#171613',.26),
        'navy':_material('Navy_Collar_And_Trim','#142748',.85),
    }
    for hair_key in ['hair','hair_highlight']:
        m[hair_key].node_tree.nodes['Principled BSDF'].inputs['Specular IOR Level'].default_value=.18
    m['jersey'],texture_path=_jersey_material(textures)
    b=_Builder(collection)
    # Raised waist and broad, splayed stance leave a real central passage.
    b.lathe('Striped_Collared_Jersey',[
        (1.76,.54,.34,0),(1.79,.61,.385,0),(1.91,.66,.405,0),
        (2.11,.65,.405,0),(2.32,.59,.37,0),(2.46,.47,.30,0),
        (2.53,.31,.235,0)],m['jersey'])
    b.lathe('Jersey_Navy_Hem',[(1.76,.54,.342,0),(1.79,.613,.388,0),(1.805,.62,.39,0)],m['navy'])
    b.sphere('Neck', (0,0,2.50),(.285,.24,.25),m['yellow'],24,14)

    # Shirt collar, with a navy V and two folded triangular flaps.
    b.tube('Collar_Back_And_Sides',[( -.32,.02,2.50),(-.25,.16,2.55),(0,.22,2.57),(.25,.16,2.55),(.32,.02,2.50)],
           [.055,.055,.055,.055,.055],m['navy'],12)
    for sign,side in [(1,'L'),(-1,'R')]:
        b.mesh('Folded_Collar_'+side,[(sign*.30,-.16,2.52),(sign*.045,-.258,2.41),
                                      (sign*.16,-.313,2.31),(sign*.35,-.24,2.43)],[(0,1,2,3)],m['navy'])
        b.tube('Collar_Edge_'+side,[(sign*.29,-.17,2.525),(sign*.04,-.27,2.415),(0,-.31,2.34)],
               [.013,.012,.009],m['white'],8)

    for sign,side in [(1,'L'),(-1,'R')]:
        leg=b.tube('Spread_Upper_Leg_'+side,[(sign*.56,.01,1.94),(sign*.90,0,1.86),
                                           (sign*1.08,-.025,1.62),(sign*1.14,-.045,1.16)],
                   [(.21,.20),(.19,.19),(.17,.18),(.15,.15)],m['yellow'],24)
        # A conservative inner contour guarantees the specified rectangular gap.
        for v in leg.data.vertices:
            if v.co.z<1.68 and abs(v.co.x)<.805: v.co.x=sign*.805
        b.sphere('Knee_'+side,(sign*1.14,-.045,1.15),(.158,.155,.185),m['yellow'],24,14)
        shin=b.tube('Orange_Shin_'+side,[(sign*1.14,-.04,1.12),(sign*1.15,-.015,.79),
                                       (sign*1.16,.01,.43),(sign*1.17,0,.15)],
                    [.112,.095,.078,.09],m['beak'],20)
        b.sphere('Foot_Pad_'+side,(sign*1.17,-.10,.085),(.24,.255,.085),m['beak'],24,12)
        for toe in range(3):
            dx=(toe-1)*.135
            b.tube(f'Toe_{side}_{toe}',[(sign*1.17+dx*.65,-.19,.09),
                                      (sign*1.17+dx,-.34,.065),
                                      (sign*1.17+dx*1.15,-.49+(abs(toe-1)*.035),.048)],
                   [.070,.057,.022],m['beak'],12)
        # Short-sleeved shirt and bent wings: relaxed hands on the outer hips.
        b.tube('Jersey_Sleeve_'+side,[(sign*.43,0,2.38),(sign*.63,-.005,2.30),(sign*.79,-.02,2.16)],
               [(.21,.22),(.20,.21),(.165,.17)],m['jersey'],24,uvscale=(.75,.5))
        b.tube('Navy_Sleeve_Trim_'+side,[(sign*.765,-.017,2.185),(sign*.80,-.02,2.145)],
               [(.17,.175),(.168,.172)],m['navy'],20)
        b.tube('Bent_Wing_'+side,[(sign*.80,-.02,2.155),(sign*.95,-.05,2.02),
                                (sign*.89,-.18,1.965),(sign*.72,-.33,1.96)],
               [.145,.135,.115,.105],m['yellow'],20)
        b.sphere('Hand_On_Hip_'+side,(sign*.69,-.36,1.95),(.145,.105,.14),m['yellow'],24,14)
        for finger in range(3):
            x=sign*(.65+.055*finger)
            b.tube(f'Finger_{side}_{finger}',[(x,-.40,1.95),(x-sign*.025,-.43,1.865),
                                            (x-sign*.035,-.42,1.845)],
                   [.043,.034,.016],m['yellow'],10)

    # Broad oval face, visible cheeks, rounded orange beak, open expressive eyes.
    b.sphere('Face_Head',(0,-.025,2.985),(.495,.385,.535),m['yellow'],36,20)
    for sign,side in [(1,'L'),(-1,'R')]:
        b.sphere('Cheek_'+side,(sign*.285,-.323,2.805),(.19,.095,.16),m['yellow'],24,14)
        b.sphere('Eye_White_'+side,(sign*.181,-.386,3.06),(.133,.074,.163),m['white'],24,14)
        b.sphere('Brown_Iris_'+side,(sign*.174,-.453,3.052),(.072,.020,.096),m['iris'],24,16)
        b.sphere('Pupil_'+side,(sign*.170,-.470,3.052),(.044,.012,.067),m['pupil'],20,14)
        b.sphere('Eye_Highlight_'+side,(sign*.170-.016,-.483,3.083),(.016,.006,.022),m['white'],12,8)
        b.sphere('Eye_Tiny_Highlight_'+side,(sign*.170+.014,-.483,3.019),(.007,.004,.009),m['white'],10,6)
        brow=[]
        for i in range(13):
            t=i/12; x=sign*(.065+.25*t)
            brow.append((x,-.390+.025*t,3.24+.043*math.sin(math.pi*t)-.005*t))
        b.tube('Soft_Arched_Brow_'+side,brow,[.020+.007*math.sin(math.pi*i/12) for i in range(13)],m['hair'],8)
    b.sphere('Mouth_Line',(0,-.484,2.751),(.177,.07,.022),m['pupil'],24,12)
    b.beak('Rounded_Upper_Beak',[(-.33,.155,2.884,2.756),(-.47,.205,2.875,2.74),
                               (-.61,.15,2.832,2.74),(-.69,.018,2.785,2.758)],m['beak'])
    b.beak('Lower_Beak',[(-.35,.135,2.75,2.70),(-.49,.17,2.75,2.69),(-.63,.035,2.75,2.72)],m['beak'])
    for sign,side in [(1,'L'),(-1,'R')]:
        b.sphere('Nostril_'+side,(sign*.080,-.469,2.867),(.017,.013,.006),m['iris'],12,8)
    _hair(b,m)

    # Small curled feathers at the lower back stay above the full passage.
    for i in range(3):
        b.sphere(f'Tail_Feather_{i}',((i-1)*.12,.36,1.85),(.10,.24,.10),m['yellow'],20,12)

    for obj in b.objects:
        obj.parent=root
        obj['obstacle_type']='slide_under'
    root['obstacle_type']='slide_under'
    root['static_pose']='Wide stance with an open central slide corridor'
    root['forward_axis_blender']='-Y'
    root['nominal_clearance_width']=1.55
    root['nominal_clearance_height']=1.66
    root['reference_note']='Stylized interpretation preserving curly hair, face and striped collared jersey.'
    all_points=[v.co for obj in b.objects for v in obj.data.vertices]
    height=max(p.z for p in all_points)-min(p.z for p in all_points)
    return {'root':root,'collection':collection,'meshes':b.objects,'materials':list(m.values()),
            'texture_paths':[texture_path],'height':height,
            'clearance':{'width':1.55,'height':1.66}}


if __name__=='__main__':
    import sys,json
    target=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else '/tmp/curly_obstacle_smoke'
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    result=build_curly_bird(target)
    vertices=triangles=0
    for obj in result['meshes']:
        obj.data.calc_loop_triangles(); vertices+=len(obj.data.vertices); triangles+=len(obj.data.loop_triangles)
    points=[v.co for obj in result['meshes'] for v in obj.data.vertices]
    low=[min(p[i] for p in points) for i in range(3)]; high=[max(p[i] for p in points) for i in range(3)]
    print('CURLY_MODEL_STATS',json.dumps({'vertices':vertices,'triangles':triangles,'objects':len(result['meshes']),
                                        'materials':len(result['materials']),'bounds_min':low,'bounds_max':high,
                                        'clearance':result['clearance']}),flush=True)
