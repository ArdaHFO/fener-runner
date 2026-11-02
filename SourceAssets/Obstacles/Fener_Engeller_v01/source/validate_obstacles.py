#!/usr/bin/env python3
"""Inspect static obstacle GLBs and a slide-under corridor independently of Blender.

Usage: python validate_obstacles.py asset.glb --gate-width 1.55 --gate-height 1.66 --report qa.json
Exit status 0 means the structural/geometry checks passed; it is not a claim
about artistic likeness, animation appeal, or performance on a physical phone.
"""

from __future__ import annotations

import argparse
import base64
import json
import math
from pathlib import Path
import struct
import sys


COMPONENTS = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2),
              5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
COUNTS = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4,
          "MAT2": 4, "MAT3": 9, "MAT4": 16}
IDENTITY = [1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1.]


def multiply(a, b):
    return [sum(a[k * 4 + row] * b[col * 4 + k] for k in range(4))
            for col in range(4) for row in range(4)]


def point(matrix, xyz):
    return [sum(matrix[k * 4 + row] * xyz[k] for k in range(3)) + matrix[12 + row]
            for row in range(3)]


def local_matrix(node):
    if "matrix" in node:
        if len(node["matrix"]) != 16:
            raise ValueError("node matrix must contain 16 numbers")
        return list(node["matrix"])
    x, y, z, w = node.get("rotation", [0, 0, 0, 1])
    sx, sy, sz = node.get("scale", [1, 1, 1])
    tx, ty, tz = node.get("translation", [0, 0, 0])
    return [(1 - 2*y*y - 2*z*z)*sx, (2*x*y + 2*z*w)*sx, (2*x*z - 2*y*w)*sx, 0,
            (2*x*y - 2*z*w)*sy, (1 - 2*x*x - 2*z*z)*sy, (2*y*z + 2*x*w)*sy, 0,
            (2*x*z + 2*y*w)*sz, (2*y*z - 2*x*w)*sz, (1 - 2*x*x - 2*y*y)*sz, 0,
            tx, ty, tz, 1]


class GLB:
    def __init__(self, path):
        self.path = Path(path)
        raw = self.path.read_bytes()
        self.size = len(raw)
        if len(raw) < 20:
            raise ValueError("file is shorter than a GLB header and JSON chunk")
        magic, version, length = struct.unpack_from("<III", raw)
        if magic != 0x46546C67 or version != 2 or length != len(raw):
            raise ValueError("invalid GLB magic, version, or declared length")
        chunks = []
        offset = 12
        while offset < len(raw):
            if offset + 8 > len(raw):
                raise ValueError("truncated GLB chunk header")
            size, kind = struct.unpack_from("<II", raw, offset)
            offset += 8
            if size % 4 or offset + size > len(raw):
                raise ValueError("misaligned or truncated GLB chunk")
            chunks.append((kind, raw[offset:offset + size]))
            offset += size
        if not chunks or chunks[0][0] != 0x4E4F534A:
            raise ValueError("the first GLB chunk is not JSON")
        if sum(kind == 0x4E4F534A for kind, _ in chunks) != 1:
            raise ValueError("GLB contains more than one JSON chunk")
        self.doc = json.loads(chunks[0][1].rstrip(b"\0 \t\r\n"))
        binary = [data for kind, data in chunks if kind == 0x004E4942]
        if len(binary) > 1:
            raise ValueError("GLB contains more than one binary chunk")
        self.buffers = []
        for index, buffer in enumerate(self.doc.get("buffers", [])):
            uri = buffer.get("uri")
            if uri:
                if not uri.startswith("data:"):
                    raise ValueError(f"buffer {index} has an external URI")
                head, payload = uri.split(",", 1)
                if ";base64" not in head:
                    raise ValueError("only base64 embedded buffer URIs are supported")
                data = base64.b64decode(payload, validate=True)
            else:
                if index != 0 or not binary:
                    raise ValueError(f"buffer {index} has no embedded binary data")
                data = binary[0]
            declared = buffer.get("byteLength", -1)
            if declared < 0 or declared > len(data) or len(data) - declared > 3:
                raise ValueError(f"buffer {index} length does not match its data")
            self.buffers.append(data[:declared])
        self.cache = {}

    def read_view(self, index):
        if not isinstance(index, int) or not 0 <= index < len(self.doc.get("bufferViews", [])):
            raise ValueError(f"invalid bufferView index {index}")
        view = self.doc["bufferViews"][index]
        if not isinstance(view.get("buffer", 0), int) or not 0 <= view.get("buffer", 0) < len(self.buffers):
            raise ValueError(f"bufferView {index} has an invalid buffer index")
        data = self.buffers[view.get("buffer", 0)]
        start, length = view.get("byteOffset", 0), view["byteLength"]
        if start < 0 or length < 0 or start + length > len(data):
            raise ValueError(f"bufferView {index} lies outside its buffer")
        return memoryview(data)[start:start + length], view

    def unpack_values(self, view_index, offset, count, component, kind, normalize=False):
        data, view = self.read_view(view_index)
        fmt, size = COMPONENTS[component]
        dimension = COUNTS[kind]
        if kind.startswith("MAT"):
            rows = int(kind[-1])
            column_stride = ((rows * size + 3) // 4) * 4
            offsets = [col * column_stride + row * size for col in range(rows) for row in range(rows)]
            element_size = rows * column_stride
        else:
            offsets, element_size = [size * i for i in range(dimension)], size * dimension
        stride = view.get("byteStride", element_size)
        if stride < element_size or offset < 0 or count < 0:
            raise ValueError("invalid accessor stride, offset, or count")
        if count and offset + (count - 1) * stride + element_size > len(data):
            raise ValueError("accessor overruns its bufferView")
        output = []
        normalizers = {5120: 127, 5121: 255, 5122: 32767, 5123: 65535}
        for item in range(count):
            values = [struct.unpack_from("<" + fmt, data, offset + item * stride + p)[0] for p in offsets]
            if normalize and component in normalizers:
                denominator = normalizers[component]
                values = [max(-1., v / denominator) if component in (5120, 5122) else v / denominator for v in values]
            if any(not math.isfinite(v) for v in values):
                raise ValueError("accessor contains a non-finite number")
            output.append(tuple(values))
        return output

    def accessor(self, index):
        if not isinstance(index, int) or not 0 <= index < len(self.doc.get("accessors", [])):
            raise ValueError(f"invalid accessor index {index}")
        if index in self.cache:
            return self.cache[index]
        accessor = self.doc["accessors"][index]
        count, component, kind = accessor["count"], accessor["componentType"], accessor["type"]
        if kind not in COUNTS or component not in COMPONENTS:
            raise ValueError(f"unsupported accessor {index} type/component")
        if "bufferView" in accessor:
            values = self.unpack_values(accessor["bufferView"], accessor.get("byteOffset", 0), count,
                                        component, kind, accessor.get("normalized", False))
        else:
            values = [(0,) * COUNTS[kind] for _ in range(count)]
        sparse = accessor.get("sparse")
        if sparse:
            sidx, sval = sparse["indices"], sparse["values"]
            indices = self.unpack_values(sidx["bufferView"], sidx.get("byteOffset", 0), sparse["count"],
                                         sidx["componentType"], "SCALAR")
            replacements = self.unpack_values(sval["bufferView"], sval.get("byteOffset", 0), sparse["count"],
                                              component, kind, accessor.get("normalized", False))
            previous = -1
            for (idx,), replacement in zip(indices, replacements):
                if idx <= previous or idx >= count:
                    raise ValueError(f"accessor {index} has invalid sparse indices")
                values[idx] = replacement
                previous = idx
        self.cache[index] = values
        return values


def clipped_projection(points, x_min, x_max, y_min, y_max):
    """Clip a triangle's X/height projection; nonempty means corridor intrusion.

    The corridor extends through the entire depth of the model, so projection
    clipping is equivalent to intersecting an infinite-depth rectangular prism.
    Degenerate projections are retained because side-facing triangles still
    constitute visible collision geometry.
    """
    poly = [(float(p[0]), float(p[1])) for p in points]
    for axis, limit, keep_greater in [(0,x_min,True),(0,x_max,False),
                                      (1,y_min,True),(1,y_max,False)]:
        if not poly: break
        result=[]
        previous=poly[-1]
        previous_inside=previous[axis]>=limit if keep_greater else previous[axis]<=limit
        for current in poly:
            inside=current[axis]>=limit if keep_greater else current[axis]<=limit
            if inside!=previous_inside:
                delta=current[axis]-previous[axis]
                if abs(delta)>1e-15:
                    t=(limit-previous[axis])/delta
                    result.append(tuple(previous[i]+t*(current[i]-previous[i]) for i in range(2)))
            if inside: result.append(current)
            previous,previous_inside=current,inside
        poly=result
    return poly


def inspect(path, gate_width=None, gate_height=None, player_report=None):
    report={'file':str(Path(path).resolve()),'passed':False,'errors':[],'warnings':[],
            'scope':'Independent GLB structure and static geometry checks. Engine colliders, timing, materials in Unity and mobile performance require integration testing.'}
    errors,warnings=report['errors'],report['warnings']
    try:
        asset=GLB(path); doc=asset.doc
        if doc.get('asset',{}).get('version')!='2.0': errors.append('asset.version is not 2.0')
        def finite(value, label='document'):
            if isinstance(value,float) and not math.isfinite(value): errors.append('nonfinite JSON at '+label)
            elif isinstance(value,dict):
                for k,v in value.items(): finite(v,label+'.'+k)
            elif isinstance(value,list):
                for i,v in enumerate(value): finite(v,label+f'[{i}]')
        finite(doc)
        for i in range(len(doc.get('bufferViews',[]))): asset.read_view(i)
        for i in range(len(doc.get('accessors',[]))): asset.accessor(i)
        nodes=doc.get('nodes',[]); meshes=doc.get('meshes',[]); parents={}
        for i,node in enumerate(nodes):
            if 'matrix' in node and any(k in node for k in ['translation','rotation','scale']): errors.append(f'node {i} has matrix and TRS')
            rotation=node.get('rotation',[0,0,0,1])
            if abs(sum(v*v for v in rotation)-1)>.005: errors.append(f'node {i} rotation is not unit')
            for child in node.get('children',[]):
                if not isinstance(child,int) or not 0<=child<len(nodes): raise ValueError(f'node {i} invalid child')
                if child in parents: raise ValueError(f'node {child} has multiple parents')
                parents[child]=i
        matrices={}; active=set()
        def world(i):
            if i in matrices:return matrices[i]
            if i in active:raise ValueError('node hierarchy contains cycle')
            active.add(i)
            m=local_matrix(nodes[i])
            if i in parents:m=multiply(world(parents[i]),m)
            active.remove(i);matrices[i]=m
            return m
        for i in range(len(nodes)):world(i)
        scene_index=doc.get('scene',0)
        scenes=doc.get('scenes',[])
        if not scenes or not 0<=scene_index<len(scenes): raise ValueError('missing or invalid default scene')
        reachable=set()
        def walk(i):
            if not 0<=i<len(nodes):raise ValueError('scene refers invalid root')
            if i in reachable:return
            reachable.add(i)
            for j in nodes[i].get('children',[]):walk(j)
        for i in scenes[scene_index].get('nodes',[]):walk(i)
        all_points=[];triangles=[];primitive_count=vertex_count=0; normal_error=0
        material_count=len(doc.get('materials',[]));mesh_instances=0
        for node_i in sorted(reachable):
            node=nodes[node_i]
            if 'mesh' not in node:continue
            mesh_i=node['mesh']
            if not 0<=mesh_i<len(meshes):raise ValueError('invalid mesh index')
            mesh_instances+=1
            for primitive in meshes[mesh_i].get('primitives',[]):
                primitive_count+=1
                if primitive.get('mode',4)!=4:raise ValueError('only triangle primitives are expected')
                attrs=primitive.get('attributes',{})
                if 'POSITION' not in attrs:raise ValueError('primitive lacks POSITION')
                positions=asset.accessor(attrs['POSITION'])
                if any(len(p)!=3 for p in positions):raise ValueError('POSITION is not VEC3')
                if not positions:raise ValueError('empty primitive positions')
                transformed=[point(world(node_i),p) for p in positions]
                all_points.extend(transformed);vertex_count+=len(positions)
                for attribute,index in attrs.items():
                    values=asset.accessor(index)
                    if len(values)!=len(positions):raise ValueError(f'{attribute} count mismatch')
                    if attribute=='NORMAL':
                        normal_error=max(normal_error,max(abs(math.sqrt(sum(n*n for n in v))-1) for v in values))
                if 'material' in primitive and not 0<=primitive['material']<material_count:raise ValueError('invalid material index')
                indices=[v[0] for v in asset.accessor(primitive['indices'])] if 'indices'in primitive else list(range(len(positions)))
                if len(indices)%3:raise ValueError('triangle index count is not multiple of3')
                if any(not isinstance(i,int) or not 0<=i<len(positions) for i in indices):raise ValueError('out-of-range vertex index')
                for j in range(0,len(indices),3):triangles.append(tuple(transformed[i] for i in indices[j:j+3]))
        if not all_points or not triangles:errors.append('no scene triangle geometry')
        if normal_error>.01:errors.append('nonunit mesh normals')
        image_records=[]
        for i,img in enumerate(doc.get('images',[])):
            if 'bufferView'in img:
                raw=bytes(asset.read_view(img['bufferView'])[0])
            elif img.get('uri','').startswith('data:'):
                raw=base64.b64decode(img['uri'].split(',',1)[1],validate=True)
            else:raise ValueError(f'image{i} external or absent')
            record={'name':img.get('name',str(i)),'embedded_bytes':len(raw),'mime_type':img.get('mimeType')}
            if raw.startswith(b'\x89PNG\r\n\x1a\n') and len(raw)>=24:
                record['width'],record['height']=struct.unpack('>II',raw[16:24])
            if not raw:errors.append('empty embedded image')
            image_records.append(record)
        textures=doc.get('textures',[])
        for i,texture in enumerate(textures):
            if 'source' not in texture or not 0<=texture['source']<len(image_records):errors.append(f'texture{i} invalid image source')
        for i,material in enumerate(doc.get('materials',[])):
            slots=[material.get('normalTexture'),material.get('occlusionTexture'),material.get('emissiveTexture')]
            pbr=material.get('pbrMetallicRoughness',{})
            slots.extend([pbr.get('baseColorTexture'),pbr.get('metallicRoughnessTexture')])
            for slot in slots:
                if slot is not None and not 0<=slot.get('index',-1)<len(textures):errors.append(f'material{i} invalid texture index')
        lo=[min(p[i] for p in all_points) for i in range(3)]
        hi=[max(p[i] for p in all_points) for i in range(3)]
        if lo[1]<-.001:errors.append('visual geometry extends below floor Y0')
        report.update({'file_bytes':asset.size,'generator':doc.get('asset',{}).get('generator'),
            'coordinate_system':'glTF +Y up, +Z forward after Blender -Y conversion; metres',
            'node_count':len(nodes),'mesh_count':len(meshes),'mesh_instances':mesh_instances,
            'primitive_instances':primitive_count,'vertices_instanced':vertex_count,'triangles_instanced':len(triangles),
            'material_count':material_count,'normal_max_length_error':normal_error,
            'skin_count':len(doc.get('skins',[])),'animation_count':len(doc.get('animations',[])),
            'images':image_records,'bounds':{'min':lo,'max':hi,'size':[hi[i]-lo[i] for i in range(3)]}})
        if gate_width is not None or gate_height is not None:
            if gate_width is None or gate_height is None or min(gate_width,gate_height)<=0:raise ValueError('gate width and height must both be positive')
            eps=1e-4
            intrusions=[]
            for i,tri in enumerate(triangles):
                if clipped_projection(tri,-gate_width/2+eps,gate_width/2-eps,eps,gate_height-eps):
                    intrusions.append(i)
            gate={'width':gate_width,'height':gate_height,'depth':'entire exported model',
                  'boundary_tolerance':eps,'triangle_intrusions':len(intrusions),'passed':not intrusions,
                  'method':'Clip every exported world-space triangle projected to X/Y against the open corridor rectangle. Projection includes all depth positions; degenerate projections are retained.'}
            if intrusions:errors.append(f'{len(intrusions)} exported triangles intrude into gate corridor')
            if player_report:
                player=json.loads(Path(player_report).read_text())
                envelope=player['slide_hold_envelope']
                gate['player_slide_envelope_blender']=envelope
                gate['left_margin']=envelope['x_min']+gate_width/2
                gate['right_margin']=gate_width/2-envelope['x_max']
                gate['top_margin']=gate_height-envelope['z_max']
                standing=next(p for p in player['poses'] if p['action']=='Idle')['max'][2]
                gate['standing_player_height']=standing
                gate['standing_height_exceeds_opening']=standing>gate_height
                player_intrusions=sum(bool(clipped_projection(tri,
                    envelope['x_min']+eps,envelope['x_max']-eps,
                    envelope['z_min']+eps,envelope['z_max']-eps)) for tri in triangles)
                gate['player_slide_aabb_prism_triangle_intrusions']=player_intrusions
                gate['slide_hold_geometry_fits']=min(gate['left_margin'],gate['right_margin'],gate['top_margin'])>0 and player_intrusions==0
                if not gate['slide_hold_geometry_fits']:errors.append('measured player Slide hold geometry does not fit')
            report['slide_corridor']=gate
        report['passed']=not errors
    except Exception as exc:
        errors.append(f'{type(exc).__name__}: {exc}')
    return report


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('glb');parser.add_argument('--report')
    parser.add_argument('--gate-width',type=float);parser.add_argument('--gate-height',type=float)
    parser.add_argument('--player-report')
    args=parser.parse_args()
    report=inspect(args.glb,args.gate_width,args.gate_height,args.player_report)
    if args.report:Path(args.report).write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2))
    return 0 if report['passed'] else 1


if __name__=='__main__':sys.exit(main())

