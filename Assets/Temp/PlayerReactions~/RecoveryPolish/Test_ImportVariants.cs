var original=(UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Test/PlayerReactions/Animations/Test_GetUp.fbx");
string[] paths={"Assets/Temp/PlayerReactionRawProbe/Test_GetUpRaw.fbx","Assets/Temp/PlayerReactionRawProbe/Test_GetUpNeutral.fbx"};
foreach(var path in paths){
 UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceSynchronousImport);
 var importer=(UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
 importer.animationType=path.Contains("Neutral")?UnityEditor.ModelImporterAnimationType.Human:UnityEditor.ModelImporterAnimationType.Generic;
 if(path.Contains("Neutral")) {importer.avatarSetup=UnityEditor.ModelImporterAvatarSetup.CopyFromOther; importer.sourceAvatar=UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Arts/Characters/Player/Models/Test_CheonPlayer.fbx").OfType<Avatar>().Single();}
 importer.importAnimation=true; importer.importCameras=false;importer.importLights=false;importer.materialImportMode=UnityEditor.ModelImporterMaterialImportMode.None;
 importer.animationCompression=UnityEditor.ModelImporterAnimationCompression.Off;
 if(path.Contains("Neutral"))importer.clipAnimations=original.clipAnimations;
 importer.SaveAndReimport();
}
return paths.Select(path=>new {path,clips=UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).Select(c=>new {c.name,c.length,c.humanMotion}).ToArray()}).ToArray();
