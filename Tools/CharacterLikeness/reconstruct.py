"""Recover a photo-fitted 3D facial surface and retain original photo UVs.
MediaPipe supplies estimated depth, not a scan; profile/back are authored separately.
"""
import json, pathlib, sys
import numpy as np
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision
from PIL import Image
photo=pathlib.Path(sys.argv[1]); out=pathlib.Path(sys.argv[2])
options=vision.FaceLandmarkerOptions(base_options=python.BaseOptions(model_asset_path='/tmp/face_landmarker.task', delegate=python.BaseOptions.Delegate.CPU),output_facial_transformation_matrixes=True)
with vision.FaceLandmarker.create_from_options(options) as detector:
 result=detector.detect(mp.Image.create_from_file(str(photo)))
if not result.face_landmarks: raise RuntimeError('No face detected')
w,h=Image.open(photo).size
landmarks=result.face_landmarks[0][:468]
p=np.array([[v.x*w,-v.y*h,-v.z*w] for v in landmarks])
# Remove camera orientation using orthogonal axes from the face itself.
x=p[454]-p[234]; x/=np.linalg.norm(x)
y=p[10]-p[152]; y-=x*np.dot(y,x); y/=np.linalg.norm(y)
z=np.cross(x,y); z/=np.linalg.norm(z)
center=(p[10]+p[152])*.5
q=(p-center)@np.stack([x,y,z],axis=1)
q *= .205/(q[10,1]-q[152,1])
# Approximate bilateral geometry, while retaining each observed texture coordinate.
faces=[]
for line in pathlib.Path('Tools/CharacterLikeness/canonical_face_model.obj').read_text().splitlines():
 if line.startswith('f '): faces.append([int(v.split('/')[0])-1 for v in line.split()[1:]])
uv=[[v.x,1-v.y] for v in landmarks]
out.write_text(json.dumps({'vertices':q.tolist(),'uv':uv,'faces':faces,'photo':str(photo.resolve()),'width':w,'height':h}))
print(out, 'vertices',len(q),'depth',np.ptp(q[:,2]))
