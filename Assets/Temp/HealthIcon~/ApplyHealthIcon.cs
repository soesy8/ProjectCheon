using UnityEditor;
using UnityEngine;

public static class ApplyHealthIcon
{
    public static object Run()
    {
        const string path = "Assets/Arts/UI/MountainPass/Resources/CheonHud/T_HealthIcon.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.maxTextureSize = 1024;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var defaults = importer.GetDefaultPlatformTextureSettings();
        defaults.maxTextureSize = 1024;
        defaults.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(defaults);
        // 작은 HUD에서도 붓 윤곽과 투명 가장자리를 유지한다.
        foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.maxTextureSize = 1024;
            settings.overridden = true;
            settings.format = TextureImporterFormat.RGBA32;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
        }
        var serializedImporter = new SerializedObject(importer);
        serializedImporter.FindProperty("m_MaxTextureSize").intValue = 1024;
        serializedImporter.ApplyModifiedPropertiesWithoutUndo();
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new System.InvalidOperationException("체력 아이콘 스프라이트 임포트 실패");
        return new { path, width = sprite.texture.width, height = sprite.texture.height,
            guid = AssetDatabase.AssetPathToGUID(path), sprite = sprite.name };
    }
}
