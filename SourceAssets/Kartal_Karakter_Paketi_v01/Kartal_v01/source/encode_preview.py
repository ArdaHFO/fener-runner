"""Encode actual Blender renders as a labeled, silent MP4 preview."""
from pathlib import Path
import json
import subprocess

ROOT = Path(__file__).resolve().parent.parent
QA = ROOT/'qa'
OUT = ROOT/'output'
meta = json.loads((QA/'motion_timing.json').read_text())
fps = meta['fps']

def stamp(seconds):
    centiseconds = round(seconds*100)
    hours, rem = divmod(centiseconds, 360000)
    minutes, rem = divmod(rem, 6000)
    secs, cs = divmod(rem, 100)
    return f'{hours}:{minutes:02d}:{secs:02d}.{cs:02d}'

labels = {'Run':'Koşu', 'RunTurn':'360° dönüş', 'JumpFlip':'Öne takla',
          'Jump':'Zıplama', 'Slide':'Kayma', 'CelebratePoint':'Karakter pozu'}
ass = '''[Script Info]
ScriptType: v4.00+
PlayResX: 448
PlayResY: 560
WrapStyle: 2

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Clip,DejaVu Sans,18,&H00303030,&H00303030,&HBBFFFFFF,&H00000000,1,0,0,0,100,100,0,0,1,1.3,0,2,15,15,20,1
Style: Heading,DejaVu Sans,12,&H004B4B4B,&H004B4B4B,&HBBFFFFFF,&H00000000,1,0,0,0,100,100,1.3,0,1,1,0,8,10,10,18,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
'''
ass += f"Dialogue: 0,0:00:00.00,{stamp(meta['frame_count']/fps)},Heading,,0,0,0,,3D KARAKTER  /  v0.1\n"
for segment in meta['segments']:
    start = max(0, (segment['first_frame']-1-(4 if segment['first_frame']>1 else 0))/fps)
    end = segment['last_frame']/fps
    ass += f"Dialogue: 0,{stamp(start)},{stamp(end)},Clip,,0,0,0,,{labels.get(segment['clip'], segment['clip'])}\n"
(QA/'motion_labels.ass').write_text(ass, encoding='utf-8')
output = OUT/'Kartal_Animasyon_Onizleme.mp4'
with (QA/'encode.log').open('w') as log:
    subprocess.run(['ffmpeg','-hide_banner','-y','-framerate',str(fps),'-i','qa/motion_frames/%04d.png',
                    '-frames:v',str(meta['frame_count']),'-vf','subtitles=qa/motion_labels.ass',
                    '-c:v','libx264','-preset','medium','-crf','19','-pix_fmt','yuv420p',
                    '-movflags','+faststart','-an',str(output)], cwd=ROOT, stdout=log, stderr=log, check=True)
probe = subprocess.run(['ffprobe','-v','error','-show_entries',
                        'stream=codec_name,width,height,r_frame_rate,nb_frames:format=duration,size',
                        '-of','json',str(output)],capture_output=True,text=True,check=True)
data = json.loads(probe.stdout)
(QA/'video_validation.json').write_text(json.dumps(data,indent=2))
print(json.dumps({'output':str(output),'probe':data},indent=2))
