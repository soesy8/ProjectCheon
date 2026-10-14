var world=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
foreach(var item in world.GetComponentsInChildren<UnityEngine.Transform>(true))
    if(item.name=="Test_ArtHangingVines")UnityEngine.Object.DestroyImmediate(item.gameObject);
foreach(string name in new[]{"SpiritFlame","SpiritOrb","HangingVines0","HangingVines1","HangingVines2"})
{
    string path="Assets/Test/Cheon/Arts/Meshes/Test_Concept"+name+".asset";
    if(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path)!=null)
    {
        string error=UnityEditor.AssetDatabase.MoveAsset(path,"Assets/Temp/Test_Concept"+name+"_Discarded.asset");
        if(!string.IsNullOrEmpty(error))throw new System.InvalidOperationException(error);
    }
}
return true;
