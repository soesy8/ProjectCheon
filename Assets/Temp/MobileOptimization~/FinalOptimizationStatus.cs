UnityEditor.AssetDatabase.Refresh();
return new { building = UnityEditor.BuildPipeline.isBuildingPlayer, target = UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString(), quality = UnityEngine.QualitySettings.GetQualityLevel(), playing = UnityEditor.EditorApplication.isPlaying, compiling = UnityEditor.EditorApplication.isCompiling };
