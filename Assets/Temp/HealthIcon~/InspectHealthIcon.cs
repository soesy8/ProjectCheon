using System.Linq;
using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEngine;

public static class InspectHealthIcon
{
    public static object Run()
    {
        const string path = "Assets/Arts/UI/MountainPass/Resources/CheonHud/T_HealthIcon.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var serialized = new SerializedObject(importer);
        var legacyMaximum = serialized.FindProperty("m_MaxTextureSize");
        var hud = Object.FindFirstObjectByType<PassHud>();
        var image = hud.GetComponentsInChildren<UnityEngine.UI.Image>(true).SingleOrDefault(value => value.name == "HealthIcon");
        var game = Object.FindFirstObjectByType<PassGameFlow>();
        if (image != null && (image.sprite == null || AssetDatabase.GetAssetPath(image.sprite) != path))
            throw new System.InvalidOperationException("체력 아이콘 연결을 확인하세요.");
        return new { state = game.State.ToString(), frame = Time.frameCount,
            image = image != null ? image.name : null, active = image != null && image.gameObject.activeInHierarchy,
            spritePath = image != null ? AssetDatabase.GetAssetPath(image.sprite) : null,
            size = image != null ? image.rectTransform.sizeDelta : Vector2.zero,
            position = image != null ? image.rectTransform.anchoredPosition : Vector2.zero,
            preserveAspect = image != null && image.preserveAspect,
            raycastTarget = image != null && image.raycastTarget, maxSize = importer.maxTextureSize,
            legacyMaximum = legacyMaximum != null ? (int?)legacyMaximum.intValue : null,
            defaultMaxSize = importer.GetDefaultPlatformTextureSettings().maxTextureSize };
    }
}
