"""Render actual exported-source geometry without rebuilding the asset.

blender --background output/Kartal_Runner_v01.blend --threads 8 --python src/render_saved.py -- --motion
"""
from pathlib import Path
import sys
import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_character import render_motion, render_stills

rig = bpy.data.objects['Kartal_Rig']
camera = bpy.data.objects['Preview_Camera']
actions = {a.name: a for a in bpy.data.actions}
if '--motion' in sys.argv:
    render_motion(rig, camera, actions)
else:
    render_stills(rig, camera, actions)
print('KARTAL_PREVIEW_COMPLETE', flush=True)
