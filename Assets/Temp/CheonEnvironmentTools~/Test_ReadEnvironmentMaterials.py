import bpy
import json
from pathlib import Path


class Test_ReadEnvironmentMaterials:
    @staticmethod
    def main():
        result = {}
        for path in sorted(Path(r"E:\C.J.I\Blender").glob("*/*.blend")):
            bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
            materials = []
            for material in bpy.data.materials:
                shader = next((node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED'), None) if material.use_nodes else None
                images = [node.image.name for node in material.node_tree.nodes if node.type == 'TEX_IMAGE' and node.image] if material.use_nodes else []
                materials.append({
                    'name': material.name,
                    'color': list(shader.inputs['Base Color'].default_value) if shader else list(material.diffuse_color),
                    'roughness': shader.inputs['Roughness'].default_value if shader else material.roughness,
                    'metallic': shader.inputs['Metallic'].default_value if shader else material.metallic,
                    'emission': list(shader.inputs['Emission Color'].default_value) if shader else [0, 0, 0, 1],
                    'emissionStrength': shader.inputs['Emission Strength'].default_value if shader else 0,
                    'images': images,
                })
            result[path.stem] = materials
        output = Path(r"D:\project\ProjectCheon\Assets\Temp\Test_CheonEnvironmentMaterials.json")
        output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
        print('MATERIAL_MANIFEST_READY', list(result))


Test_ReadEnvironmentMaterials.main()
