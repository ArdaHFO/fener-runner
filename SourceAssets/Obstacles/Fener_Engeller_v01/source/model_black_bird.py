"""Reference-inspired black bird, posed in a deep squat as a jump obstacle.

Standalone Blender 4.5 builder. Z up, -Y front, metres, floor at Z=0.
No external mesh, procedural shader, rig or scene dependency.
"""
from pathlib import Path
import math
import bpy
import numpy as np
from mathutils import Vector


def _linear(v):
    return v / 12.92 if v < .04045 else ((v + .055) / 1.055) ** 2.4


def _color(hex_value):
    h = hex_value.lstrip('#')
    return tuple(_linear(int(h[i:i+2], 16) / 255) for i in (0, 2, 4)) + (1,)


def _material(name, color, roughness=.72):
    m = bpy.data.materials.new('BB_' + name)
    m.use_nodes = True
    m.diffuse_color = _color(color)
    bsdf = m.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = _color(color)
    bsdf.inputs['Roughness'].default_value = roughness
    return m


def _jersey_material(directory):
    size = 512
    y, x = np.mgrid[:size, :size]
    # A portable UV texture gives fourteen alternating vertical fabric panels.
    stripe = ((x * 14 // size) % 2) == 0
    # Generated Image.pixels are saved directly into this sRGB PNG. Supplying
    # shader-linear values here would darken the texture for a second time.
    yellow = np.asarray([239, 199, 37], dtype=np.float32) / 255.0  # #EFC725
    navy = np.asarray([20, 39, 72], dtype=np.float32) / 255.0     # #142748
    pixels = np.where(stripe[..., None], yellow, navy)
    # Very quiet fabric grain, not a runtime procedural shader.
    grain = 1.0 + .006 * np.sin(x * math.pi / 2) * np.cos(y * math.pi / 2)
    pixels = np.clip(pixels * grain[..., None], 0, 1)
    rgba = np.ones((size, size, 4), dtype=np.float32)
    rgba[..., :3] = pixels
    image = bpy.data.images.new('BB_Jersey_Stripes', size, size, alpha=True)
    image.pixels.foreach_set(rgba.ravel())
    path = directory / 'black_bird_jersey_basecolor.png'
    image.filepath_raw = str(path)
    image.file_format = 'PNG'
    image.save()
    image.pack()
    material = _material('Striped_Jersey', '#EFC725', .84)
    node = material.node_tree.nodes.new('ShaderNodeTexImage')
    node.image = image
    node.interpolation = 'Linear'
    material.node_tree.links.new(node.outputs['Color'], material.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    return material, path


class _Builder:
    def __init__(self, collection, root):
        self.collection, self.root = collection, root
        self.meshes = []

    def finish(self, obj, name, mat):
        obj.name = 'BB_' + name
        for c in list(obj.users_collection):
            c.objects.unlink(obj)
        self.collection.objects.link(obj)
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        obj.parent = self.root
        self.meshes.append(obj)
        return obj

    def sphere(self, name, center, radii, mat, direction=None, segments=28, rings=16):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=center)
        obj = bpy.context.object
        obj.scale = radii
        if direction:
            obj.rotation_mode = 'QUATERNION'
            obj.rotation_quaternion = Vector(direction).to_track_quat('Z', 'Y')
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        return self.finish(obj, name, mat)

    def mesh(self, name, vertices, faces, mat, uv=None):
        data = bpy.data.meshes.new('BB_' + name + '_Mesh')
        data.from_pydata(vertices, [], faces)
        data.update()
        obj = bpy.data.objects.new('BB_' + name, data)
        self.collection.objects.link(obj)
        if uv:
            layer = data.uv_layers.new(name='UVMap')
            for polygon in data.polygons:
                for loop_index in polygon.loop_indices:
                    layer.data[loop_index].uv = uv[data.loops[loop_index].vertex_index]
        return self.finish(obj, name, mat)

    def lathe(self, name, profile, mat, sectors=48):
        verts, faces, uv = [], [], []
        z0, z1 = profile[0][0], profile[-1][0]
        for z, rx, ry, cy in profile:
            for j in range(sectors+1):
                a = j / sectors * math.tau
                verts.append((rx*math.cos(a), cy+ry*math.sin(a), z))
                uv.append((j/sectors, (z-z0)/(z1-z0)))
        row = sectors+1
        for i in range(len(profile)-1):
            for j in range(sectors):
                a=i*row+j
                faces.append((a,a+1,a+row+1,a+row))
        faces.append(tuple(reversed(range(sectors))))
        faces.append(tuple((len(profile)-1)*row+j for j in range(sectors)))
        return self.mesh(name, verts, faces, mat, uv)

    def tube(self, name, centers, radii, mat, sides=14):
        verts, faces, uv = [], [], []
        pts = [Vector(p) for p in centers]
        for i, p in enumerate(pts):
            t = (pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]).normalized()
            reference = Vector((0,1,0)) if abs(t.y)<.94 else Vector((1,0,0))
            u = (reference-t*reference.dot(t)).normalized()
            v = t.cross(u).normalized()
            r = radii[i]
            depth,width = r if isinstance(r,(tuple,list)) else (r,r)
            for j in range(sides+1):
                a=j/sides*math.tau
                verts.append(tuple(p+u*(math.cos(a)*depth)+v*(math.sin(a)*width)))
                uv.append((j/sides,i/(len(pts)-1)))
        row=sides+1
        for i in range(len(pts)-1):
            for j in range(sides):
                a=i*row+j
                faces.append((a,a+1,a+row+1,a+row))
        faces.append(tuple(reversed(range(sides))))
        faces.append(tuple((len(pts)-1)*row+j for j in range(sides)))
        return self.mesh(name,verts,faces,mat,uv)

    def disk(self, name, x, y, z, radius, mat, sides=32):
        # Front-facing circular jersey patch, minimal depth layering.
        verts=[(x,y,z)]+[(x+radius*math.cos(j/sides*math.tau), y, z+radius*math.sin(j/sides*math.tau)) for j in range(sides)]
        faces=[(0,1+j,1+(j+1)%sides) for j in range(sides)]
        return self.mesh(name,verts,faces,mat)

    def beak(self, name, sections, mat):
        verts, faces = [], []
        sides=20
        for y,width,top,bottom in sections:
            for j in range(sides+1):
                angle=j/sides*math.tau
                verts.append((width*math.cos(angle),y,(top+bottom)*.5+math.sin(angle)*(top-bottom)*.5))
        row=sides+1
        for i in range(len(sections)-1):
            for j in range(sides):
                a=i*row+j
                faces.append((a,a+1,a+row+1,a+row))
        faces.append(tuple(reversed(range(sides))))
        faces.append(tuple((len(sections)-1)*row+j for j in range(sides)))
        return self.mesh(name,verts,faces,mat)


def build_black_bird(output_dir):
    """Build one reference-inspired stationary obstacle in its delivered squat pose."""
    out=Path(output_dir)
    texture_dir=out/'textures'
    texture_dir.mkdir(parents=True,exist_ok=True)
    collection=bpy.data.collections.new('BB_Black_Bird_Obstacle')
    bpy.context.scene.collection.children.link(collection)
    root=bpy.data.objects.new('Obstacle_Jump_BlackBird',None)
    collection.objects.link(root)
    root['obstacle_role']='JUMP_OVER'
    root['pose']='DEEP_CROUCH'
    root['front_axis_blender']='-Y'
    root['reference_note']='Black rounded bird, large eyes, charcoal beak, yellow/navy striped jersey; unseen back is a design interpretation.'
    b=_Builder(collection,root)
    mats={
        'feather':_material('Charcoal_Feathers','#1C2023',.68),
        'beak':_material('Charcoal_Beak','#3A4247',.39),
        'white':_material('Ivory_Eye_And_Crest','#F4F1E6',.36),
        'iris':_material('Brown_Iris','#493D29',.36),
        'pupil':_material('Deep_Pupil','#070A0D',.24),
        'navy':_material('Navy_Trim','#142748',.83),
        'yellow':_material('Golden_Trim','#EFC725',.83),
        'red':_material('Crest_Red','#A72C36',.73),
        'green':_material('Crest_Green','#306940',.76),
    }
    jersey,texture_path=_jersey_material(texture_dir)
    mats['jersey']=jersey

    # Crouching pelvis, prominent folded thighs and backward-folded shins.
    b.sphere('Low_Pelvis',(0,.025,.235),(.34,.275,.175),mats['feather'])
    for side,sign in [('L',1),('R',-1)]:
        b.sphere('Folded_Thigh_'+side,(.335*sign,-.055,.175),(.155,.175,.12),mats['feather'],segments=24,rings=14)
        b.tube('Bent_Shin_'+side,[(.385*sign,-.165,.17),(.43*sign,-.09,.115),(.455*sign,.005,.06)],[.039,.033,.028],mats['beak'])
        b.sphere('Foot_Pad_'+side,(.455*sign,-.04,.045),(.081,.105,.032),mats['feather'],segments=20,rings=12)
        for toe,dx in enumerate([-.075,0,.075]):
            b.tube('Toe_'+side+'_'+str(toe),[(.455*sign+dx*.35,-.09,.041),(.455*sign+dx*.75,-.19,.027),(.455*sign+dx,-.265+(abs(dx)*.30),.021)],[.023,.019,.008],mats['beak'],sides=10)
        b.tube('Back_Toe_'+side,[(.455*sign,.015,.042),(.47*sign,.105,.025),(.49*sign,.15,.019)],[.021,.015,.005],mats['beak'],sides=10)

    # The shirt is a full, round garment, not a compressed image or flattened slab.
    b.lathe('Round_Striped_Jersey',[
        (.205,.30,.24,.025),(.225,.385,.285,.025),(.29,.445,.315,.025),
        (.39,.47,.325,.025),(.49,.435,.30,.025),(.57,.365,.265,.025),
        (.63,.285,.222,.025),(.65,.235,.20,.025)
    ],jersey)
    b.lathe('Navy_Hem',[(.208,.30,.243,.025),(.221,.38,.287,.025),(.235,.394,.292,.025)],mats['navy'])
    b.lathe('Navy_Collar',[(.627,.257,.212,.025),(.65,.25,.209,.025),(.67,.229,.19,.025)],mats['navy'])

    for side,sign in [('L',1),('R',-1)]:
        b.tube('Short_Jersey_Sleeve_'+side,[(.30*sign,.015,.58),(.405*sign,-.005,.50),(.465*sign,-.025,.415)],[(.112,.12),(.108,.12),(.095,.105)],jersey,sides=20)
        b.tube('Sleeve_Navy_Edge_'+side,[(.455*sign,-.022,.43),(.476*sign,-.028,.405)],[(.096,.106),(.094,.104)],mats['navy'],sides=20)
        # Relaxed elbow outside the torso, forearm/wing resting toward the knee.
        b.tube('Folded_Wing_'+side,[(.465*sign,-.025,.414),(.535*sign,-.055,.32),(.49*sign,-.19,.24),(.415*sign,-.25,.225)],[(.075,.087),(.066,.08),(.051,.066),(.04,.052)],mats['feather'],sides=16)
        for k in range(3):
            sx=(.46-.024*k)*sign
            b.tube('Wing_Finger_'+side+'_'+str(k),[(sx,-.205,.245),((.43-.018*k)*sign,-.27,.22),((.42-.017*k)*sign,-.30,.181+.011*k)],[(.018,.024),(.014,.020),(.004,.007)],mats['feather'],sides=10)

    # Small fan tail peeks behind the low body, staying clear of the ground.
    for i in range(3):
        x=(i-1)*.07
        b.tube('Tail_Feather_'+str(i),[(x*.45,.25,.275),(x*.8,.36,.215),(x,.45,.15)],[.046,.043,.003],mats['feather'],sides=12)

    # Round black head and subtle cheek fullness reproduce the reference silhouette.
    b.sphere('Round_Black_Head',(0,-.025,.77),(.29,.24,.235),mats['feather'],segments=40,rings=24)
    for sign in [-1,1]:
        b.sphere('Cheek_'+str(sign),(.18*sign,-.155,.71),(.102,.11,.097),mats['feather'],segments=24,rings=14)
    for k,(x,tipx,z) in enumerate([(-.036,-.063,1.035),(0,.008,1.052),(.034,.069,1.032)]):
        b.tube('Crown_Feather_'+str(k),[(x,.006,.977),(x,.015,1.01),(tipx,.022,z)],[(.022,.029),(.017,.024),(.002,.002)],mats['feather'],sides=10)
    for side,sign in [('L',1),('R',-1)]:
        b.sphere('Eye_White_'+side,(.105*sign,-.247,.831),(.083,.048,.087),mats['white'],segments=28,rings=18)
        b.sphere('Iris_'+side,(.098*sign,-.291,.839),(.043,.012,.048),mats['iris'],segments=24,rings=14)
        b.sphere('Pupil_'+side,(.098*sign,-.302,.84),(.032,.006,.038),mats['pupil'],segments=24,rings=14)
        b.sphere('Eye_Catchlight_'+side,(.098*sign-.011,-.309,.855),(.011,.003,.014),mats['white'],segments=14,rings=10)
        b.tube('Soft_Brow_'+side,[(.036*sign,-.241,.898),(.102*sign,-.257,.921),(.175*sign,-.213,.897)],[.011,.015,.009],mats['feather'],sides=10)

    b.beak('Upper_Charcoal_Beak',[
        (-.238,.077,.794,.737),(-.29,.095,.791,.734),(-.36,.073,.774,.732),(-.438,.004,.741,.735)
    ],mats['beak'])
    b.beak('Mouth_Line',[
        (-.245,.07,.735,.727),(-.34,.065,.734,.726),(-.412,.004,.734,.731)
    ],mats['pupil'])
    b.beak('Lower_Charcoal_Beak',[
        (-.245,.064,.727,.706),(-.323,.059,.727,.709),(-.408,.003,.731,.724)
    ],mats['beak'])
    for sign in [-1,1]:
        b.sphere('Nostril_'+str(sign),(.035*sign,-.311,.788),(.009,.012,.003),mats['pupil'],segments=12,rings=8)

    # A small layered circular crest preserves the visible badge colours/silhouette.
    # It deliberately has no invented sponsor or unreadable pseudo-lettering.
    x,y,z=.217,-.227,.555
    b.disk('Crest_White_Ring',x,y,z,.053,mats['white'])
    b.disk('Crest_Navy_Rim',x,y-.001,z,.047,mats['navy'])
    b.disk('Crest_Red_Field',x,y-.002,z,.041,mats['red'])
    shield=[(-.029,.014),(.029,.014),(.026,-.021),(0,-.032),(-.026,-.021)]
    b.mesh('Crest_Yellow_Shield',[(x+px,y-.003,z+pz) for px,pz in shield],[(4,3,2,1,0)],mats['yellow'])
    lower=[(-.028,-.002),(.028,-.002),(.026,-.021),(0,-.032),(-.026,-.021)]
    b.mesh('Crest_Navy_Shield',[(x+px,y-.004,z+pz) for px,pz in lower],[(4,3,2,1,0)],mats['navy'])
    b.tube('Crest_Green_Stem',[(x,y-.006,z-.022),(x+.003,y-.006,z+.022)],[.0023,.0023],mats['green'],sides=8)
    for sign in [-1,1]:
        for level in [0,.012]:
            leaf=b.sphere('Crest_Leaf_'+str(sign)+'_'+str(level),(x+.009*sign,y-.008,z+level),(.011,.002,.005),mats['green'],segments=12,rings=8)
    b.tube('Chest_Gold_Stitch',[(-.22,-.23,.57),(-.20,-.244,.578),(-.178,-.25,.575)],[.004,.004,.002],mats['yellow'],sides=8)

    # Normalize floor exactly while retaining all object transforms at identity.
    minimum=min(v.co.z for obj in b.meshes for v in obj.data.vertices)
    for obj in b.meshes:
        for vertex in obj.data.vertices:
            vertex.co.z -= minimum
    points=[v.co for obj in b.meshes for v in obj.data.vertices]
    low=[min(v[i] for v in points) for i in range(3)]
    high=[max(v[i] for v in points) for i in range(3)]
    root['dimensions_m']=[high[i]-low[i] for i in range(3)]
    root['static_pose']=True
    root['reference_image']='image(20261031-112208).png'
    return {'root':root,'collection':collection,'meshes':b.meshes,'materials':list(mats.values()),
            'texture_paths':[str(texture_path)],'height':high[2], 'bounds':{'min':low,'max':high}}


if __name__=='__main__':
    import json,sys
    directory=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else '/tmp/black_bird_model_check'
    result=build_black_bird(directory)
    triangles=0
    for obj in result['meshes']:
        obj.data.calc_loop_triangles()
        triangles+=len(obj.data.loop_triangles)
    print('BB_MODEL_CHECK',json.dumps({'meshes':len(result['meshes']),'vertices':sum(len(o.data.vertices) for o in result['meshes']),
          'triangles':triangles,'materials':len(result['materials']),'bounds':result['bounds'],'texture_paths':result['texture_paths']}),flush=True)
