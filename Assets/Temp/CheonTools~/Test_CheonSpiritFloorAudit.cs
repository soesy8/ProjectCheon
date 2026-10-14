Test_CheonSpiritWidth.Apply();
var world=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
var floor=world.transform.Find("Test_SpiritTerrace");
var bounds=floor.GetComponent<UnityEngine.Renderer>().bounds;
float regionRatio=Test_CheonSpiritWidth.SpiritWidth/(Test_CheonMapLayout.MaximumZ-Test_CheonMapLayout.BoundaryZ);
var floorScale=floor.localScale;var floorPosition=floor.localPosition;
var originalPoints=new System.Collections.Generic.List<UnityEngine.Vector3[]>();
bool ringsFit=true;
foreach(string name in new[]{"Test_OuterSeal","Test_InnerSeal"})
{
    var line=world.transform.Find(name).GetComponent<UnityEngine.LineRenderer>();
    var points=new UnityEngine.Vector3[line.positionCount];line.GetPositions(points);originalPoints.Add(points);
    ringsFit &= UnityEngine.Mathf.Abs(line.transform.localPosition.z-floor.localPosition.z)<.001f;
    foreach(var point in points)ringsFit &= UnityEngine.Mathf.Abs(point.x)<bounds.extents.x && UnityEngine.Mathf.Abs(point.z)<bounds.extents.z;
}
Test_CheonSpiritWidth.Apply();
bool stable=floorScale==floor.localScale && floorPosition==floor.localPosition;
var result=new{floorWidth=bounds.size.x,floorDepth=bounds.size.z,regionRatio=regionRatio,floorRatio=bounds.size.x/bounds.size.z,ratioMatches=UnityEngine.Mathf.Abs(regionRatio-bounds.size.x/bounds.size.z)<.0001f,floorCentered=UnityEngine.Mathf.Abs(floor.localPosition.z-(Test_CheonMapLayout.BoundaryZ+Test_CheonMapLayout.MaximumZ)/2)<.001f,insideRegion=bounds.min.x>-Test_CheonSpiritWidth.SpiritWidth/2 && bounds.max.x<Test_CheonSpiritWidth.SpiritWidth/2 && bounds.min.z>Test_CheonMapLayout.BoundaryZ && bounds.max.z<Test_CheonMapLayout.MaximumZ,ringsFit=ringsFit,reapplyingStable=stable};
System.IO.File.WriteAllText("Assets/Temp/Test_CheonSpiritFloorAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
return result;
