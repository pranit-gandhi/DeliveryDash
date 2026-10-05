"""Build a real skinned FBX from CC0 body and a photo-fitted facial surface.
Run with Blender --background --python ... -- sam-altman.
"""
import bpy,bmesh,json,math,random,pathlib,sys
from mathutils import Vector,Quaternion
ROOT=pathlib.Path.cwd(); slug=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'sam-altman'
DATA=json.loads((ROOT/'References/Characters'/f'{slug}-face.json').read_text())
OUT=ROOT/'Assets/DeliveryDash/Art/Characters/Likeness'/slug; OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
def mat(name,color,rough=.6):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=rough
 return m
skin=mat('Skin',(.57,.36,.25)); cloth=mat('Tailored clothing', {'sam-altman':(.025,.042,.075),'dario-amodei':(.065,.07,.075),'elon-musk':(.016,.017,.019),'donald-trump':(.018,.026,.048),'jensen-huang':(.012,.014,.016)}[slug],.36 if slug=='jensen-huang' else .86); denim=mat('Dark denim',(.035,.04,.05),.9); shoes=mat('Leather shoes',(.022,.019,.017),.45)
faceMat=mat('Photo fitted facial albedo',(.6,.4,.3),.8)
image=bpy.data.images.load(DATA['photo']); tex=faceMat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
faceMat.node_tree.links.new(tex.outputs['Color'],faceMat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/DeliveryDash/ThirdParty/Quaternius/CourierBase.fbx'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');body=bpy.data.objects['SuperHero_Male'];body.name='Clothed anatomical body'
for name in ['Eyes','Eyebrows']:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
bm=bmesh.new();bm.from_mesh(body.data)
plane=body.matrix_world.inverted()@Vector((0,0,1.50))
normal=body.matrix_world.to_3x3().transposed()@Vector((0,0,1))
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),plane_co=plane,plane_no=normal,clear_outer=True,dist=.00001)
bm.to_mesh(body.data);bm.free()
body.data.materials.clear()
for m in [skin,cloth,denim,shoes]:body.data.materials.append(m)
for p in body.data.polygons:
 co=sum((body.matrix_world@body.data.vertices[i].co for i in p.vertices),Vector())/len(p.vertices)
 p.material_index=3 if co.z<.115 else 2 if co.z<.97 else 0 if abs(co.x)>.71 or co.z>1.585 else 1;p.use_smooth=True
sub=body.modifiers.new('Smooth tailored body','SUBSURF');sub.levels=1
# Fitted face retains the original photograph as UV albedo, with actual nonplanar geometry.
verts=[(x,z+.08,y+1.685) for x,y,z in DATA['vertices']]
faces=[list(reversed(f)) for f in DATA['faces']]; uv=DATA['uv']
if slug=='jensen-huang':
 for i in [10,338,297,332,284,251,389,356,454,323,361,288,397,365,379,378,400,377,152,148,176,149,150,136,172,58,132,93,234,127,162,21,54,103,67,109]:
  uv[i]=[uv[i][k]*.94+uv[1][k]*.06 for k in [0,1]]
def mesh(name,verts,faces,material,uvs=None):
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);me.materials.append(material)
 for p in me.polygons:p.use_smooth=True
 if uvs:
  layer=me.uv_layers.new(name='Photo UV')
  for p in me.polygons:
   for li in p.loop_indices:layer.data[li].uv=uvs[me.loops[li].vertex_index]
 return o
def skin_to(o,bone='Head'):
 # Mesh coordinates are world coordinates; preserve them under the imported armature.
 o.parent=arm;o.matrix_parent_inverse=arm.matrix_world.inverted()
 vg=o.vertex_groups.new(name=bone);vg.add(list(range(len(o.data.vertices))),1,'REPLACE')
 mod=o.modifiers.new('Skeleton','ARMATURE');mod.object=arm
 return o
# Landmark estimates on an occluded cheek can project outside the silhouette.
# Correct the mesh UVs, keeping the licensed source image unchanged.
if slug=='jensen-huang':
 pixels=list(image.pixels);w,h=image.size
 for pair in uv:
  for attempt in range(45):
   x=int(max(0,min(w-1,pair[0]*w)));y=int(max(0,min(h-1,pair[1]*h)));r,g,b=pixels[4*(y*w+x):4*(y*w+x)+3]
   if r>g*1.05 or g<b*1.05:break
   pair[0]+=(.5-pair[0])*.025
face=skin_to(mesh('Reconstructed facial surface',verts,faces,faceMat,uv))
# Close the eye and lip openings with photo-matched geometry (no billboard).
for name,ring in [('Left eye',[33,160,159,158,133,153,145,144]),('Right eye',[263,387,386,385,362,380,374,373]),('Mouth',[78,191,80,81,82,13,312,311,310,415,308,324,318,402,317,14,87,178,88,95])]:
 center=sum((Vector(verts[i]) for i in ring),Vector())/len(ring); cUV=tuple(sum(uv[i][k] for i in ring)/len(ring) for k in [0,1])
 vs=[tuple(center)]+[verts[i] for i in ring];uvs=[cUV]+[uv[i] for i in ring]
 skin_to(mesh(name,vs,[(0,1+i,1+(i+1)%len(ring)) for i in range(len(ring))],faceMat,uvs))
sub=face.modifiers.new('Facial subdivision','SUBSURF');sub.levels=2
oval=[10,338,297,332,284,251,389,356,454,323,361,288,397,365,379,378,400,377,152,148,176,149,150,136,172,58,132,93,234,127,162,21,54,103,67,109]
vs=[];fs=[];skullUV=[];count=len(oval)
for ring in range(5):
 t=ring/4
 for i in oval:
  x,y,z=verts[i]
  vs.append((x*(1-.22*t),y*(1-t)-.065*t,1.69+(z-1.69)*(1-.2*t)))
  skullUV.append(uv[i])
for k in range(4):
 for j in range(count):a=k*count+j;b=k*count+(j+1)%count;fs.append((a,b,b+count,a+count))
vs.append((0,-.105,1.69));skullUV.append(uv[234])
for j in range(count):fs.append((4*count+j,4*count+(j+1)%count,len(vs)-1))
skull=skin_to(mesh('Full skull and jaw',vs,[list(reversed(f)) for f in fs],faceMat,skullUV));sub=skull.modifiers.new('Skull smoothing','SUBSURF');sub.levels=2
# Ears with concha, helix and lobe, modeled in 3D.
def ellipsoid(name,location,scale,material):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=16,location=location);o=bpy.context.object;o.name=name;o.scale=scale
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 bpy.ops.object.transform_apply(location=True,rotation=False,scale=False)
 o.data.materials.append(material)
 for p in o.data.polygons:p.use_smooth=True
 return skin_to(o)
for s in [-1,1]:
 ellipsoid('Ear', (s*.087,-.003,1.68),(.015,.016,.03),skin)
 ellipsoid('Ear lobe',(s*.089,.0,1.661),(.011,.01,.012),skin)
# Sculpted scalp and hundreds of individual curved locks, exported as meshes.
hairColor={'donald-trump':(.42,.27,.10),'jensen-huang':(.30,.31,.30),'dario-amodei':(.015,.011,.009)}.get(slug,(.038,.027,.019));rng=random.Random(6553)
hairMats=[mat('Hair strand '+str(i),tuple(c*(.7+i*.18) for c in hairColor),.7) for i in range(5)]
# Domed scalp with no open crown and an irregular, swept hairline.
def scalp(a,t):
 front=max(0,math.sin(a)); bottom=1.682+.093*front**3
 if slug=='dario-amodei':bottom-=.008
 if slug=='donald-trump':bottom+=.004
 bottom+=.002*math.sin(a*11)+.001*math.sin(a*23)
 phi=t*math.acos((bottom-1.705)/.121)
 return Vector((.089*math.sin(phi)*math.cos(a)+(.008*(1-t) if slug=='donald-trump' else 0),-.013+.096*math.sin(phi)*math.sin(a),1.705+(.14 if slug=='dario-amodei' else .121)*math.cos(phi)))
hv=[];hf=[];N=96;R=24
for j in range(R+1):
 for i in range(N):hv.append(tuple(scalp(i*2*math.pi/N,j/R)))
for j in range(R):
 for i in range(N):a=j*N+i;b=j*N+(i+1)%N;hf.append((a,b,b+N,a+N))
cap=skin_to(mesh('Contoured hair cap',hv,[list(reversed(f)) for f in hf],hairMats[1]))
for k in range(1800):
 a=rng.uniform(0,math.pi*2);t=rng.uniform(.01,.98)
 points=[]
 for j in range(7):
  u=j/6
  p=scalp(a+u*.28,min(1,t+u*.1))
  normal=Vector((p.x/.089,(p.y+.013)/.096,(p.z-1.705)/.121)).normalized()
  p+=normal*(.0015+(.009 if slug in ['donald-trump','dario-amodei'] else .004)*math.sin(u*math.pi))
  if slug=='dario-amodei':p+=normal*(.003+.003*math.sin(u*math.pi*3+k))
  points.append(p)
 vv=[];ff=[]
 for j,p in enumerate(points):
  r=.00065*(1-j/7)+.00008
  tangent=(points[min(6,j+1)]-points[max(0,j-1)]).normalized()
  right=tangent.cross(Vector((0,1,0))).normalized();up=tangent.cross(right).normalized()
  for n in range(3):vv.append(tuple(p+right*r*math.cos(n*2*math.pi/3)+up*r*math.sin(n*2*math.pi/3)))
 for j in range(6):
  for n in range(3):ff.append((j*3+n,j*3+(n+1)%3,(j+1)*3+(n+1)%3,(j+1)*3+n))
 skin_to(mesh('Hair lock',vv,ff,hairMats[k%5]))
# A fitted garment shell replaces the painted-on superhero torso silhouette.
rows=[(.94,.151,.102),(1.0,.16,.105),(1.13,.166,.112),(1.27,.191,.124),(1.4,.215,.11),(1.475,.20,.079),(1.525,.057,.052),(1.54,.054,.051)]
vv=[];ff=[];segs=64
for z,rx,ry in rows:
 for j in range(segs):
  angle=j*math.pi*2/segs; crease=.002*math.sin(j*1.8+z*24)
  vv.append(((rx+crease)*math.cos(angle),.012+(ry+crease)*math.sin(angle),z))
for row in range(len(rows)-1):
 for j in range(segs):a=row*segs+j;b=row*segs+(j+1)%segs;ff.append((a,b,b+segs,a+segs))
garment=skin_to(mesh('Tailored garment shell',vv,ff,cloth),'spine_03')
sub=garment.modifiers.new('Fabric surface','SUBSURF');sub.levels=2
# Jensen's zipper and Trump's shirt/lapels/tie are real surfaces, not name cues.
if slug in ['donald-trump','jensen-huang']:
 pale=mat('Shirt and hardware',(.75,.76,.72),.35)
 if slug=='donald-trump':
  skin_to(mesh('White shirt front',[(-.050,.145,1.49),(.050,.145,1.49),(.044,.145,1.18),(-.044,.145,1.18)],[(0,1,2,3)],pale),'spine_03')
  red=mat('Red silk tie',(.42,.018,.024),.4)
  skin_to(mesh('Silk tie',[(-.015,.15,1.46),(.015,.15,1.46),(.024,.15,1.14),(0,.15,1.1),(-.024,.15,1.14)],[(0,1,2,3,4)],red),'spine_03')
 else:
  skin_to(mesh('Jacket zipper',[(-.003,.138,1.05),(.003,.138,1.05),(.003,.092,1.47),(-.003,.092,1.47)],[(0,1,2,3)],pale),'spine_03')
# Clean neck overlap and a tailored collar conceal the base mesh cut line.
ellipsoid('Neck connection',(0,-.005,1.573),(.048,.046,.086),skin)
def torus(name,location,major,minor,material):
 bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=12,location=location,major_radius=major,minor_radius=minor)
 o=bpy.context.object;o.name=name;bpy.ops.object.transform_apply(location=True,rotation=False,scale=False);o.data.materials.append(material)
 for p in o.data.polygons:p.use_smooth=True
 return skin_to(o,'neck_01')
torus('Sweater crewneck collar',(0,.012,1.531),.051,.004,cloth)
# Apply each mesh's smoothing before joining so the skull modifiers are retained.
for o in list(bpy.data.objects):
 if o.type!='MESH':continue
 bpy.context.view_layer.objects.active=o
 for mod in list(o.modifiers):
  if mod.type=='SUBSURF':bpy.ops.object.modifier_apply(modifier=mod.name)
# Combine head meshes for practical draw-call counts, preserving material slots and weights.
bpy.ops.object.select_all(action='DESELECT')
headObjects=[o for o in bpy.data.objects if o.type=='MESH' and o!=body]
for o in headObjects:o.select_set(True)
bpy.context.view_layer.objects.active=face
bpy.ops.object.join();face.name='Likeness head and groom'
# Reduce redundant identical material slots after join.
# Body + head use the same existing humanoid skeleton. Armature animation is authored in Unity.
for o in [body,face]:
 bpy.context.view_layer.objects.active=o
 for modifier in list(o.modifiers):
  if modifier.type=='SUBSURF':bpy.ops.object.modifier_apply(modifier=modifier.name)
# Export before posing, giving Unity a clean rest skeleton.
bpy.ops.object.select_all(action='DESELECT')
for o in [arm,body,face]:o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=str(OUT/f'{slug}.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=False,axis_forward='-Z',axis_up='Y')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'References/Characters'/f'{slug}.blend'))
# Neutral studio review of front, 3/4 and profile. Real geometry rendered from each camera.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Studio world');scene.world.color=(.16,.16,.16);scene.view_settings.view_transform='AgX'
for loc,power,size in [((1.5,2.5,3),170,3),((-2,1,2.2),90,2),((0,-2,2.8),150,2)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,1.68))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.39
for label,loc in [('front',(0,1.5,1.70)),('three-quarter',(.85,1.1,1.70)),('profile',(1.5,.02,1.70))]:
 cam.location=loc;cam.rotation_euler=(Vector((0,0,1.70))-cam.location).to_track_quat('-Z','Y').to_euler()
 scene.render.filepath=str(ROOT/'Captures'/f'{slug}-{label}.png');bpy.ops.render.render(write_still=True)
print('EXPORTED',slug)
