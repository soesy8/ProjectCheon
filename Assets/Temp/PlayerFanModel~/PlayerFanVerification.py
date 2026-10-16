import bpy
import bmesh
import os
import json
from mathutils import Vector


class PlayerFanVerification:
    """디스크에 저장된 블렌더 파일을 다시 열어 메시와 패킹된 이미지를 확인한다."""

    def verify(self):
        directory = os.path.dirname(os.path.abspath(__file__))
        path = os.path.join(directory, 'Cheon_PlayerFan.blend')
        bpy.ops.wm.open_mainfile(filepath=path)
        root = bpy.data.objects['PlayerFan_Root']
        objects = [obj for obj in root.children if obj.type == 'MESH']
        assert len(objects) == 41
        assert bpy.context.scene.camera.name == 'Camera_Isometric'
        assert bpy.context.scene.unit_settings.system == 'METRIC'
        images = [image for image in bpy.data.images if image.source == 'FILE']
        assert len(images) == 5
        assert all(image.packed_file for image in images)
        texture = bpy.data.images['T_PlayerFan_PaperBaseColor']
        assert tuple(texture.size) == (1024, 1024)
        assert all(len(obj.data.uv_layers) == 1 for obj in objects)
        bounds, triangles = [], 0
        dependency_graph = bpy.context.evaluated_depsgraph_get()
        for obj in objects:
            evaluated = obj.evaluated_get(dependency_graph)
            bounds.extend(evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box)
            mesh = evaluated.to_mesh()
            mesh.calc_loop_triangles()
            triangles += len(mesh.loop_triangles)
            bm = bmesh.new()
            bm.from_mesh(mesh)
            assert all(edge.is_manifold for edge in bm.edges), obj.name
            assert all(vertex.link_faces for vertex in bm.verts), obj.name
            assert bm.calc_volume(signed=True) > 0, obj.name
            bm.free()
            evaluated.to_mesh_clear()
        dimensions = [max(point[axis] for point in bounds) - min(point[axis] for point in bounds) for axis in range(3)]
        assert .50 < dimensions[0] < .55
        assert .42 < dimensions[2] < .47
        report = {'reopened_blend': True, 'mesh_objects': len(objects),
                  'evaluated_triangles': triangles, 'dimensions_xyz_m': dimensions,
                  'embedded_images': len(images), 'packed_artwork': True,
                  'closed_meshes_after_bevel': True, 'outward_normals': True,
                  'default_camera': bpy.context.scene.camera.name}
        with open(os.path.join(directory, 'PlayerFanVerification.json'), 'w', encoding='utf-8') as target:
            json.dump(report, target, indent=2, ensure_ascii=False)
        print('SAVED_BLEND_VERIFICATION_PASS', json.dumps(report), flush=True)


if __name__ == '__main__':
    PlayerFanVerification().verify()
