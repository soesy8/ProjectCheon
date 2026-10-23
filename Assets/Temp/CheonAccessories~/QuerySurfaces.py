import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

deps=bpy.context.evaluated_depsgraph_get()
def tree(name):
    obj=bpy.data.objects[name].evaluated_get(deps)
    mesh=obj.to_mesh()
    result=BVHTree.FromPolygons([obj.matrix_world@v.co for v in mesh.vertices],[list(f.vertices) for f in mesh.polygons])
    obj.to_mesh_clear()
    return result
body=tree('Body')
hat=tree('Gat')
def ray(bvh,origin,direction):
    hit=bvh.ray_cast(Vector(origin),Vector(direction))
    return tuple(round(c,5) for c in hit[0]) if hit[0] is not None else None
print('SHOULDER FRONT/BACK')
for x,z in [(-.18,1.36),(-.17,1.34),(-.155,1.32),(-.12,1.28),(-.08,1.22),(0,1.13),(.06,1.06),(.13,.99)]:
    print(x,z,ray(body,(x,-2,z),(0,1,0)),ray(body,(x,2,z),(0,-1,0)))
print('SHOULDER TOP')
for y in [-.12,-.09,-.06,-.03,0,.03,.06,.09,.12]:
    print(y,ray(body,(-.18,y,2),(0,0,-1)))
print('CHIN FACE')
for x,z in [(.145,1.79),(.155,1.72),(.160,1.67),(.157,1.61),(.135,1.56),(.11,1.52),(.075,1.49),(.035,1.465),(0,1.45),(0,1.43),(0,1.4),(0,1.36),(0,1.30)]:
    print(x,z,ray(body,(x,-2,z),(0,1,0)))
print('HAT ANCHOR',ray(hat,(.155,-.015,1.5),(0,0,1)))
pouch=bpy.data.objects['Pouch']
print('POUCH LOCAL LEVELS',sorted(set(round(v.co.z,3) for v in pouch.data.vertices)))
for x in [-.93,.93]:
    for z in [.65,.85,1.0]:
        p=pouch.matrix_world@Vector((x,0,z))
        print('POUCH ATTACH',x,z,tuple(round(c,5) for c in p))
