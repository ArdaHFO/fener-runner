"""Reference-inspired, skinnable stylized bird mascot.

Blender 4.5 compatible. Z is up, the character faces -Y.  This module only
creates character assets; it deliberately does not reset, rig or export a scene.
"""
from pathlib import Path
import math
import bpy
import numpy as np
from mathutils import Vector


def _linear(value):
    return value / 12.92 if value < 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def _rgba(hex_color):
    s = hex_color.lstrip('#')
    return tuple(_linear(int(s[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (1.0,)


def _material(name, color, roughness=0.72):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = _rgba(color)
    principled = material.node_tree.nodes.get('Principled BSDF')
    principled.inputs['Base Color'].default_value = _rgba(color)
    principled.inputs['Roughness'].default_value = roughness
    principled.inputs['Metallic'].default_value = 0.0
    return material


def _image_from_array(name, pixels, path, noncolor=False):
    height, width = pixels.shape[:2]
    image = bpy.data.images.new(name, width=width, height=height, alpha=True)
    if noncolor:
        image.colorspace_settings.name = 'Non-Color'
    rgba = np.ones((height, width, 4), dtype=np.float32)
    rgba[..., :3] = pixels
    image.pixels.foreach_set(rgba.ravel())
    image.filepath_raw = str(path)
    image.file_format = 'PNG'
    image.save()
    image.pack()
    return image


def _knit_material(texture_dir):
    """Numerically construct a seamless cable knit height field and normal map."""
    size = 1024
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    u, v = x / size, y / size
    # Small V-shaped wool stitches underneath the larger raised cable columns.
    cell_x = (u * 80.0 + 0.5) % 1.0 - 0.5
    cell_y = (v * 80.0 + 0.5) % 1.0 - 0.5
    stitch = np.exp(-((cell_y - 0.72 * np.abs(cell_x) + 0.15) / 0.09) ** 2)
    height = 0.18 + 0.065 * stitch
    column = (u * 8.0 + 0.5) % 1.0 - 0.5
    phase = v * 6.0 * 2.0 * np.pi
    strand1 = 0.205 * np.sin(phase)
    strand2 = -strand1
    for center, offset in ((strand1, 0.0), (strand2, np.pi)):
        ridge = np.exp(-((column - center) / 0.077) ** 2)
        over = 0.75 + 0.25 * np.cos(phase + offset)
        thread = 0.96 + 0.04 * np.cos(v * 180.0 * 2.0 * np.pi + column * 25.0)
        height += 0.30 * ridge * over * thread
    # Quiet purl/rib gutters between braided columns.
    height += 0.085 * np.exp(-((np.abs(column) - 0.425) / 0.034) ** 2)
    gx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    gy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    nx, ny = -gx * 22.0, -gy * 22.0
    nz = np.ones_like(nx)
    norm = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack((nx / norm, ny / norm, nz / norm), axis=-1) * 0.5 + 0.5
    # Surface color carries only soft occlusion; all the actual relief is normal data.
    shade = np.clip(0.91 + (height - 0.30) * 0.20, 0.82, 1.0)
    base = np.array([0.91, 0.88, 0.79], dtype=np.float32)
    albedo = np.clip(shade[..., None] * base[None, None, :], 0, 1)
    color_image = _image_from_array('Kartal_Knit_BaseColor', albedo,
                                    texture_dir / 'kartal_knit_basecolor.png')
    normal_image = _image_from_array('Kartal_Knit_Normal', normal,
                                     texture_dir / 'kartal_knit_normal.png', True)
    material = _material('Kartal_Cream_CableKnit', '#EEE5CD', 0.90)
    nodes, links = material.node_tree.nodes, material.node_tree.links
    principled = nodes.get('Principled BSDF')
    color_node = nodes.new('ShaderNodeTexImage')
    color_node.name = 'Portable knit base color'
    color_node.image = color_image
    color_node.location = (-470, 140)
    normal_node = nodes.new('ShaderNodeTexImage')
    normal_node.name = 'Portable knit normal'
    normal_node.image = normal_image
    normal_node.location = (-470, -150)
    normal_map = nodes.new('ShaderNodeNormalMap')
    normal_map.inputs['Strength'].default_value = 0.62
    normal_map.location = (-180, -90)
    links.new(color_node.outputs['Color'], principled.inputs['Base Color'])
    links.new(normal_node.outputs['Color'], normal_map.inputs['Color'])
    links.new(normal_map.outputs['Normal'], principled.inputs['Normal'])
    material['texture_note'] = 'Numerically generated cable knit. Packed PNG base color + tangent normal.'
    return material


class Builder:
    def __init__(self, collection, materials):
        self.collection, self.materials = collection, materials
        self.objects = []

    def finish(self, obj, name, material, bone='head', skin_segment=None):
        obj.name = name
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        self.collection.objects.link(obj)
        obj.data.materials.clear()
        obj.data.materials.append(material)
        for poly in obj.data.polygons:
            poly.use_smooth = True
        obj['bind_bone'] = bone
        if skin_segment:
            obj['skin_segment'] = skin_segment
        self.objects.append(obj)
        return obj

    def sphere(self, name, center, radii, material, bone='head', segments=32, rings=20,
               direction=None):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings,
                                             radius=1.0, location=center)
        obj = bpy.context.object
        obj.scale = radii
        if direction:
            obj.rotation_mode = 'QUATERNION'
            obj.rotation_quaternion = Vector(direction).to_track_quat('Z', 'Y')
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        return self.finish(obj, name, material, bone)

    def mesh(self, name, vertices, faces, material, bone='head', uv=None, skin_segment=None):
        data = bpy.data.meshes.new(name + '_Mesh')
        data.from_pydata(vertices, [], faces)
        data.update()
        obj = bpy.data.objects.new(name, data)
        self.collection.objects.link(obj)
        if uv is not None:
            layer = data.uv_layers.new(name='UVMap')
            for poly in data.polygons:
                for loop_index in poly.loop_indices:
                    layer.data[loop_index].uv = uv[data.loops[loop_index].vertex_index]
        return self.finish(obj, name, material, bone, skin_segment)

    def lathe(self, name, profile, material, bone='spine', sectors=64, rib=0.0, uvscale=(1, 1)):
        """profile entries are z, x-radius, y-radius, y-center."""
        vertices, faces, uv = [], [], []
        z0, z1 = profile[0][0], profile[-1][0]
        for ri, (z, rx, ry, cy) in enumerate(profile):
            for j in range(sectors + 1):
                a = 2.0 * math.pi * j / sectors
                corr = 1.0 + rib * math.cos(a * 16.0)
                vertices.append((rx * math.cos(a) * corr, cy + ry * math.sin(a) * corr, z))
                uv.append((j / sectors * uvscale[0], (z - z0) / (z1 - z0) * uvscale[1]))
        row = sectors + 1
        for i in range(len(profile) - 1):
            for j in range(sectors):
                a = i * row + j
                faces.append((a, a + 1, a + row + 1, a + row))
        # Close ends. The caps are deliberately small and mostly occluded.
        faces.append(tuple(reversed(tuple(range(sectors)))))
        faces.append(tuple((len(profile) - 1) * row + j for j in range(sectors)))
        return self.mesh(name, vertices, faces, material, bone, uv)

    def tube(self, name, centers, radii, material, bone, sides=20, skin_segment=None,
             uvscale=(1.0, 1.0), rib=0.0):
        vertices, faces, uv = [], [], []
        points = [Vector(p) for p in centers]
        for i, point in enumerate(points):
            tangent = points[min(i + 1, len(points) - 1)] - points[max(0, i - 1)]
            tangent.normalize()
            # Prefer Y as the depth axis for arms/feet. Fall back safely for front-facing parts.
            reference = Vector((0, 1, 0))
            if abs(tangent.dot(reference)) > .94:
                reference = Vector((1, 0, 0))
            u = (reference - tangent * reference.dot(tangent)).normalized()
            v = tangent.cross(u).normalized()
            depth, width = radii[i] if isinstance(radii[i], (tuple, list)) else (radii[i], radii[i])
            for j in range(sides + 1):
                a = j / sides * math.tau
                factor = 1 + rib * math.cos(a * 8)
                p = point + factor * (u * math.cos(a) * depth + v * math.sin(a) * width)
                vertices.append(tuple(p))
                uv.append((j / sides * uvscale[0], i / (len(points) - 1) * uvscale[1]))
        row = sides + 1
        for i in range(len(points) - 1):
            for j in range(sides):
                a = i * row + j
                faces.append((a, a + 1, a + row + 1, a + row))
        faces.append(tuple(reversed(tuple(range(sides)))))
        faces.append(tuple((len(points) - 1) * row + j for j in range(sides)))
        return self.mesh(name, vertices, faces, material, bone, uv, skin_segment)

    def beak(self, name, sections, material, bone):
        # Sections go from rear to front: Y, half width, top Z, lower Z.
        vertices, faces, uv = [], [], []
        sides = 24
        for i, (y, width, top, bottom) in enumerate(sections):
            middle = (top + bottom) * .5
            for j in range(sides + 1):
                angle = j / sides * math.tau
                x = width * math.cos(angle)
                z = middle + math.sin(angle) * (top - bottom) * .5
                vertices.append((x, y, z))
                uv.append((j / sides, i / (len(sections) - 1)))
        row = sides + 1
        for i in range(len(sections) - 1):
            for j in range(sides):
                a = i * row + j
                faces.append((a, a + 1, a + row + 1, a + row))
        faces.append(tuple(reversed(tuple(range(sides)))))
        faces.append(tuple((len(sections) - 1) * row + j for j in range(sides)))
        return self.mesh(name, vertices, faces, material, bone, uv)

    def upper_eyelid(self, side, material):
        """A fitted golden cap narrows the eye without changing the facial landmarks."""
        cx, cy, cz = side * .162, -.375, 1.566
        rx, ry, rz = .132, .078, .145
        sectors, rows = 32, 9
        vertices, faces, uv = [], [], []
        for row in range(rows + 1):
            for j in range(sectors + 1):
                angle = j / sectors * math.tau
                # A gently tilted lower edge: outer eye higher, inner eye lower.
                phi_max = math.acos(.055 / rz)
                for _ in range(4):
                    cut = .055 + .26 * rx * math.sin(phi_max) * math.cos(angle) * side
                    phi_max = math.acos(max(-.99, min(.99, cut / rz)))
                phi = max(.001, phi_max * row / rows)
                vertices.append((cx + rx * math.sin(phi) * math.cos(angle),
                                 cy + ry * math.sin(phi) * math.sin(angle),
                                 cz + rz * math.cos(phi)))
                uv.append((j / sectors, row / rows))
        stride = sectors + 1
        for row in range(rows):
            for j in range(sectors):
                a = row * stride + j
                # Clockwise viewed from above; outward-facing cap surface.
                faces.append((a, a + stride, a + stride + 1, a + 1))
        return self.mesh('Confident upper eyelid ' + ('L' if side > 0 else 'R'),
                         vertices, faces, material, 'head', uv)

    def combine(self):
        # Parts sharing a deformation bone and material become one mesh. This keeps
        # mobile draw calls modest while preserving sleeves' smooth weighting hints.
        groups = {}
        for obj in self.objects:
            key = (obj['bind_bone'], obj.get('skin_segment', ''), obj.data.materials[0].name)
            groups.setdefault(key, []).append(obj)
        result = []
        for (bone, segment, material), objects in groups.items():
            if len(objects) > 1:
                bpy.ops.object.select_all(action='DESELECT')
                for obj in objects:
                    obj.select_set(True)
                bpy.context.view_layer.objects.active = objects[0]
                bpy.ops.object.join()
            obj = objects[0]
            obj.name = f'Kartal_{bone.replace(".", "_")}_{material.replace("Kartal_", "")}'
            obj['bind_bone'] = bone
            if segment:
                obj['skin_segment'] = segment
            result.append(obj)
        bpy.ops.object.select_all(action='DESELECT')
        self.objects = result
        return result


def build_character(output_dir):
    output_dir = Path(output_dir)
    texture_dir = output_dir / 'textures'
    texture_dir.mkdir(parents=True, exist_ok=True)
    collection = bpy.data.collections.new('Kartal_Character')
    bpy.context.scene.collection.children.link(collection)
    materials = {
        'yellow': _material('Kartal_Golden_Feathers', '#F3CD57', .77),
        'yellow_light': _material('Kartal_Feather_Highlights', '#F7DC78', .78),
        'yellow_shadow': _material('Kartal_Feather_Warm', '#E4B644', .79),
        'beak': _material('Kartal_Orange_Beak', '#E99A32', .48),
        'beak_lower': _material('Kartal_Lower_Beak', '#D88528', .55),
        'feet': _material('Kartal_Orange_Feet', '#CA792E', .72),
        'claws': _material('Kartal_Toe_Tips', '#DEAD68', .67),
        'mouth': _material('Kartal_Mouth_Interior', '#52291F', .94),
        'brow': _material('Kartal_Strong_Brows', '#9A6829', .83),
        'white': _material('Kartal_Eye_Ivory', '#FFF5DB', .36),
        'iris': _material('Kartal_Eye_Amber', '#98582C', .38),
        'pupil': _material('Kartal_Eye_Pupils', '#251B16', .29),
        'glint': _material('Kartal_Eye_Catchlight', '#FFFFFF', .20),
        'rib': _material('Kartal_Sweater_Ribbing', '#E5DDC9', .91),
    }
    materials['knit'] = _knit_material(texture_dir)
    b = Builder(collection, materials)

    # Main soft silhouette. The warm belly peeks out below the sweater hem.
    b.sphere('Golden belly', (0, .025, .57), (.34, .255, .25), materials['yellow'], 'pelvis', 40, 24)
    b.lathe('Cable-knit sweater', [
        (.435, .285, .227, .015), (.45, .340, .26, .015), (.49, .372, .282, .014),
        (.56, .397, .295, .012), (.66, .411, .307, .008), (.78, .411, .306, .002),
        (.91, .394, .292, .0), (1.02, .360, .264, .0), (1.10, .317, .233, .0),
        (1.155, .264, .203, .0), (1.17, .233, .184, .0)
    ], materials['knit'], 'spine', 64)
    b.lathe('Ribbed sweater hem', [
        (.435, .291, .233, .015), (.443, .329, .251, .015), (.458, .352, .269, .015),
        (.48, .366, .279, .014), (.497, .371, .284, .014)
    ], materials['rib'], 'pelvis', 64, .005)
    # High, folded turtleneck frames the head, with the top tucked beneath the cheeks.
    b.lathe('Folded turtleneck collar', [
        (1.055, .285, .245, -.006), (1.075, .311, .270, -.006),
        (1.12, .322, .283, -.006), (1.18, .320, .282, -.006),
        (1.225, .310, .275, -.005), (1.245, .295, .260, -.004),
        (1.242, .250, .211, -.002), (1.208, .225, .189, -.001)
    ], materials['rib'], 'chest', 64, .008)

    # The large head is intentionally round and readable from behind as well as in front.
    head = b.sphere('Large golden head', (0, -.015, 1.48), (.445, .38, .417), materials['yellow'], 'head', 48, 32)
    # The reference's chin sits inside a high collar. Taper only the lower part
    # of the round head to reveal that collar while keeping its rig pivot fixed.
    for vertex in head.data.vertices:
        if vertex.co.z < 1.36:
            amount = min(1.0, (1.36 - vertex.co.z) / .29)
            vertex.co.x *= 1.0 - .24 * amount
            vertex.co.y = -.015 + (vertex.co.y + .015) * (1.0 - .30 * amount)
    head.data.update()
    b.sphere('Soft left cheek', (.285, -.228, 1.375), (.15, .16, .145), materials['yellow'], 'head', 24, 16)
    b.sphere('Soft right cheek', (-.285, -.228, 1.375), (.15, .16, .145), materials['yellow'], 'head', 24, 16)

    # Three swept tufts, leaning gently toward one side rather than a symmetrical crown.
    b.tube('Central crown feather', [(0, .015, 1.82), (.005, -.002, 1.91), (.06, -.023, 1.988), (.135, -.045, 2.025)],
           [(.088, .077), (.077, .071), (.045, .041), (.002, .003)], materials['yellow_light'], 'head', 20)
    b.tube('Right crown feather', [(-.115, .045, 1.81), (-.12, .016, 1.91), (-.071, -.016, 1.965), (-.015, -.037, 1.98)],
           [(.068, .057), (.052, .05), (.03, .026), (.002, .002)], materials['yellow'], 'head', 16)
    b.tube('Rear crown feather', [(.035, .09, 1.825), (.09, .08, 1.915), (.165, .06, 1.966), (.222, .041, 1.963)],
           [(.061, .057), (.055, .046), (.028, .026), (.002, .002)], materials['yellow_shadow'], 'head', 16)

    for side, suffix in ((1, 'L'), (-1, 'R')):
        # One small side feather softens the outline without expensive fur strands.
        b.tube('Temple feather ' + suffix,
               [(side * .365, .08, 1.49), (side * .433, .099, 1.47), (side * .464, .087, 1.43)],
               [(.055, .054), (.038, .035), (.002, .003)], materials['yellow'], 'head', 14)
        # Eyes are deliberately large, warm and slightly recessed into the face.
        b.sphere('Eye white ' + suffix, (side * .162, -.366, 1.566), (.13, .068, .143), materials['white'], 'head', 32, 20)
        b.sphere('Amber iris ' + suffix, (side * .155, -.431, 1.557), (.060, .017, .083), materials['iris'], 'head', 24, 16)
        b.sphere('Deep pupil ' + suffix, (side * .151, -.446, 1.558), (.035, .012, .058), materials['pupil'], 'head', 24, 16)
        b.sphere('Eye glint ' + suffix, (side * .151 - .014, -.455, 1.586), (.013, .006, .017), materials['glint'], 'head', 16, 10)
        b.sphere('Eye secondary glint ' + suffix, (side * .151 + .015, -.455, 1.533), (.005, .003, .006), materials['glint'], 'head', 12, 8)
        b.upper_eyelid(side, materials['yellow'])
        # The inner ends drop to communicate the confident expression in the source.
        brow_points = [(side * .286, -.324, 1.744), (side * .245, -.389, 1.738),
                       (side * .182, -.424, 1.713), (side * .116, -.426, 1.683),
                       (side * .071, -.406, 1.668)]
        b.tube('Sculpted eyebrow ' + suffix, brow_points,
               [(.009, .014), (.020, .025), (.024, .027), (.020, .023), (.006, .009)],
               materials['brow'], 'head', 14)

    # Upper bill is a broad hooked shell-like shape, with an independently skinnable jaw.
    b.beak('Sculpted upper bill', [
        (-.309, .085, 1.339, 1.283), (-.345, .131, 1.366, 1.278),
        (-.397, .178, 1.378, 1.273), (-.451, .173, 1.365, 1.260),
        (-.505, .146, 1.338, 1.244), (-.558, .108, 1.301, 1.226),
        (-.602, .061, 1.265, 1.215), (-.635, .008, 1.226, 1.211)
    ], materials['beak'], 'head')
    b.sphere('Dark mouth gap', (0, -.425, 1.249), (.137, .089, .031), materials['mouth'], 'head', 28, 16)
    b.beak('Lower jaw', [
        (-.318, .076, 1.268, 1.207), (-.36, .117, 1.266, 1.192),
        (-.414, .143, 1.250, 1.177), (-.475, .126, 1.226, 1.165),
        (-.533, .088, 1.204, 1.167), (-.576, .026, 1.189, 1.174),
        (-.588, .003, 1.182, 1.177)
    ], materials['beak_lower'], 'jaw')
    # Shallow nostrils, tucked on the upper slope of the beak.
    for side in (-1, 1):
        b.sphere('Bill nostril', (side * .075, -.405, 1.371), (.018, .015, .006), materials['brow'], 'head', 16, 10)

    for side, suffix in ((1, 'L'), (-1, 'R')):
        # Continuous, softly bent sleeves span both bones; weighting can blend across the elbow.
        sleeve_centers = [(.285, .012, 1.047), (.337, .008, 1.024), (.398, .003, .994),
                          (.459, .0, .958), (.516, -.002, .918), (.56, -.004, .873),
                          (.603, -.007, .828), (.647, -.01, .782), (.689, -.013, .738),
                          (.724, -.015, .704)]
        sleeve_centers = [(side * x, y, z) for x, y, z in sleeve_centers]
        b.tube('Continuous knit sleeve ' + suffix, sleeve_centers,
               [(.115, .13), (.139, .14), (.138, .14), (.126, .133), (.119, .125),
                (.116, .121), (.113, .115), (.107, .109), (.099, .101), (.088, .091)],
               materials['knit'], 'upper_arm.' + suffix, 32, 'arm.' + suffix, (.6, .75))
        cuff_centers = [(side * x, y, z) for x, y, z in
                        [( .672, -.012, .755), (.689, -.013, .738), (.711, -.015, .716), (.735, -.016, .692)]]
        b.tube('Ribbed cuff ' + suffix, cuff_centers,
               [(.101, .104), (.103, .106), (.103, .106), (.092, .096)],
               materials['rib'], 'forearm.' + suffix, 32, rib=.006)
        b.sphere('Soft palm ' + suffix, (side * .782, -.023, .659), (.092, .068, .085),
                 materials['yellow'], 'hand.' + suffix, 24, 16)
        # Rounded three-finger silhouette with one small thumb: readable rather than detailed human hands.
        for number, (dy, reach, drop) in enumerate(((-.048, .105, .041), (-.006, .126, .032), (.035, .103, .021))):
            p0 = (side * .805, -.023 + dy, .651)
            p1 = (side * (.805 + reach * .6), -.028 + dy, .651 - drop * .55)
            p2 = (side * (.805 + reach), -.026 + dy, .651 - drop)
            b.tube(f'Finger {suffix} {number}', [p0, p1, p2],
                   [(.026, .027), (.027, .026), (.010, .012)], materials['yellow'], 'hand.' + suffix, 12)
        b.tube('Thumb ' + suffix,
               [(side * .750, -.05, .685), (side * .767, -.095, .674), (side * .79, -.107, .652)],
               [(.029, .029), (.027, .028), (.008, .01)], materials['yellow'], 'hand.' + suffix, 12)

        # The short thighs are mostly under the sweater; the narrow orange shins remain recognizable.
        b.sphere('Upper leg ' + suffix, (side * .174, .002, .425), (.092, .089, .17),
                 materials['yellow_shadow'], 'thigh.' + suffix, 24, 16)
        b.tube('Orange shin ' + suffix,
               [(side * .180, -.01, .321), (side * .18, -.003, .25), (side * .18, .002, .163), (side * .18, .0, .113)],
               [(.044, .045), (.038, .039), (.039, .04), (.052, .05)],
               materials['feet'], 'shin.' + suffix, 18)
        b.sphere('Foot pad ' + suffix, (side * .18, -.075, .071), (.08, .125, .047),
                 materials['feet'], 'foot.' + suffix, 24, 16)
        for index, (offset, tip_y) in enumerate(((-.088, -.281), (0, -.337), (.088, -.281))):
            x0 = side * .18 + offset * .33
            xm = side * .18 + offset * .79
            xt = side * .18 + offset
            b.tube(f'Spread toe {suffix} {index}',
                   [(x0, -.133, .069), (xm, -.215, .065), (xt, tip_y + .018, .045), (xt, tip_y, .043)],
                   [(.032, .032), (.033, .031), (.024, .022), (.011, .013)],
                   materials['feet'], 'foot.' + suffix, 14)
            b.sphere(f'Soft toe tip {suffix} {index}', (xt, tip_y - .007, .042),
                     (.018, .026, .014), materials['claws'], 'foot.' + suffix, 16, 10)
        b.tube('Rear toe ' + suffix,
               [(side * .18, .015, .072), (side * .20, .085, .05), (side * .225, .12, .035)],
               [(.023, .026), (.022, .022), (.008, .01)], materials['feet'], 'foot.' + suffix, 12)

    # The original does not reveal a tail. Keep this conservative and tucked beneath the hem.
    for x, dy in ((-.08, .025), (0, .06), (.08, .025)):
        b.tube('Tucked tail feather', [(x, .215, .53), (x * 1.12, .32, .505), (x * 1.1, .405 + dy, .55)],
               [(.055, .06), (.052, .055), (.004, .005)], materials['yellow_shadow'], 'pelvis', 14)

    meshes = b.combine()
    for obj in meshes:
        obj['asset_role'] = 'character_mesh'
        obj['source_reference'] = 'kartal.png: stylized interpretation, unseen surfaces designed'
    collection['front_axis'] = '-Y'
    collection['up_axis'] = '+Z'
    collection['reference_note'] = 'Reference-inspired first-pass model; no direct 3D reconstruction claimed.'
    return {'collection': collection, 'meshes': meshes, 'materials': materials, 'height': 2.03,
            'texture_paths': [str(texture_dir / 'kartal_knit_basecolor.png'),
                              str(texture_dir / 'kartal_knit_normal.png')]}
