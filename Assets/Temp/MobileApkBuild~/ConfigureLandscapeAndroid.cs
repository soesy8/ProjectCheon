using UnityEditor;

public static class ConfigureLandscapeAndroid
{
    public static object Main()
    {
        if (BuildPipeline.isBuildingPlayer || EditorApplication.isPlaying)
            throw new System.InvalidOperationException("Wait for the current build to finish.");
        PlayerSettings.productName = "천지인";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        AssetDatabase.SaveAssets();
        return new
        {
            appName = PlayerSettings.productName,
            orientation = PlayerSettings.defaultInterfaceOrientation.ToString(),
            portrait = PlayerSettings.allowedAutorotateToPortrait,
            upsideDown = PlayerSettings.allowedAutorotateToPortraitUpsideDown
        };
    }
}
