var world=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
var roots=new System.Collections.Generic.List<UnityEngine.Transform>();
foreach(UnityEngine.Transform child in world.transform)
{
    if(child.name=="Test_ExpandedLandscape") {foreach(UnityEngine.Transform item in child) roots.Add(item);}
    else roots.Add(child);
}
var records=new System.Collections.Generic.List<object>();
foreach(var item in roots) records.Add(new{id=item.GetInstanceID(),name=item.name,position=new[]{item.position.x,item.position.y,item.position.z},scale=new[]{item.lossyScale.x,item.lossyScale.y,item.lossyScale.z}});
System.IO.File.WriteAllText("Assets/Temp/Test_CheonBeforeSpiritWidth.json",Newtonsoft.Json.JsonConvert.SerializeObject(records));
Test_CheonSpiritWidth.Apply();
return true;
