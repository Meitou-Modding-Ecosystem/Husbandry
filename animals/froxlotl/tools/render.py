import bpy, sys, math, mathutils
import os; argv=os.environ['RARGS'].split('|')
glb, tex, out = argv[0], argv[1], argv[2]
views = argv[3].split(',') if len(argv)>3 else ['34']
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
texnodes=[n for o in meshes for m in o.data.materials for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.outputs['Color'].links and n.outputs['Color'].links[0].to_socket.name=='Base Color']
bpy.context.view_layer.update()
pts=[o.matrix_world @ mathutils.Vector(c) for o in meshes for c in o.bound_box]
mn=mathutils.Vector([min(p[i] for p in pts) for i in range(3)]); mx=mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
ctr=(mn+mx)/2; rad=(mx-mn).length/2
sc=bpy.context.scene
sc.render.engine='CYCLES'; sc.cycles.samples=48; sc.cycles.device='CPU'; sc.cycles.use_denoising=True
sc.render.resolution_x=sc.render.resolution_y=int(argv[4]) if len(argv)>4 else 640
sc.render.film_transparent=True
sc.view_settings.view_transform='Standard'
w=bpy.data.worlds.new('w'); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[1].default_value=0.6
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
cam.data.lens=50
def light(rot,e):
    l=bpy.data.objects.new('l',bpy.data.lights.new('l','SUN')); l.data.energy=e; l.rotation_euler=rot; sc.collection.objects.link(l)
light((math.radians(50),0,math.radians(30)),3.0); light((math.radians(70),0,math.radians(200)),1.2)
print('bounds',mn,mx)
for t in tex.split(','):
  img=bpy.data.images.load(t)
  for n in texnodes: n.image=img
  stem=os.path.splitext(os.path.basename(t))[0]
  for v in views:
    az,el=[math.radians(float(q)) for q in v.split(':')]
    d=rad*(2.2 if abs(el)>0.3 else 3.0)
    cam.location=ctr+mathutils.Vector((math.sin(az)*math.cos(el)*d,-math.cos(az)*math.cos(el)*d,math.sin(el)*d))
    cam.rotation_euler=(ctr-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=os.path.join(out,stem+'_'+v.replace(':','_').replace('-','m')+'.png')
    bpy.ops.render.render(write_still=True)
