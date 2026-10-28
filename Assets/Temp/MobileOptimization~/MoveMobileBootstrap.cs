string error = UnityEditor.AssetDatabase.MoveAsset("Assets/Temp/MobileOptimization/Editor/Test_MobileQualityBootstrap.cs", "Assets/Temp/MobileOptimization/Test_MobileQualityBootstrap.cs");
if (!string.IsNullOrEmpty(error)) throw new System.Exception(error);
return true;
