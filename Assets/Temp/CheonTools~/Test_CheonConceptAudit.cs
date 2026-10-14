var records=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/Test_CheonBeforeConceptArt.json"));
var missing=new System.Collections.Generic.List<string>();var moved=new System.Collections.Generic.List<string>();var resized=new System.Collections.Generic.List<string>();var collisionChanged=new System.Collections.Generic.List<string>();var meshBoundsChanged=new System.Collections.Generic.List<string>();
foreach(var record in records)
{
    var item=UnityEditor.EditorUtility.InstanceIDToObject((int)record["id"]) as UnityEngine.Transform;
    if(item==null){missing.Add((string)record["name"]);continue;}
    var position=record["position"];var scale=record["scale"];var rotation=record["rotation"];
    var oldPosition=new UnityEngine.Vector3((float)position[0],(float)position[1],(float)position[2]);
    var oldScale=new UnityEngine.Vector3((float)scale[0],(float)scale[1],(float)scale[2]);
    var oldRotation=new UnityEngine.Quaternion((float)rotation[0],(float)rotation[1],(float)rotation[2],(float)rotation[3]);
    if(UnityEngine.Vector3.Distance(item.localPosition,oldPosition)>.0001f || UnityEngine.Quaternion.Angle(item.localRotation,oldRotation)>.001f)moved.Add(item.name);
    if(UnityEngine.Vector3.Distance(item.localScale,oldScale)>.0001f)resized.Add(item.name);
    var collider=item.GetComponent<UnityEngine.BoxCollider>();var box=record["boxSize"];
    if(collider!=null && box.Type!=Newtonsoft.Json.Linq.JTokenType.Null && UnityEngine.Vector3.Distance(collider.size,new UnityEngine.Vector3((float)box[0],(float)box[1],(float)box[2]))>.0001f)collisionChanged.Add(item.name);
    var filter=item.GetComponent<UnityEngine.MeshFilter>();var oldBounds=record["meshBounds"];
    if(filter!=null && oldBounds.Type!=Newtonsoft.Json.Linq.JTokenType.Null)
    {
        var b=filter.sharedMesh.bounds;
        if(UnityEngine.Vector3.Distance(b.min,new UnityEngine.Vector3((float)oldBounds[0],(float)oldBounds[1],(float)oldBounds[2]))>.0001f || UnityEngine.Vector3.Distance(b.max,new UnityEngine.Vector3((float)oldBounds[3],(float)oldBounds[4],(float)oldBounds[5]))>.0001f)meshBoundsChanged.Add(item.name);
    }
}
var world=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
var real=world.transform.Find("Test_WalkableGround").GetComponent<UnityEngine.BoxCollider>();
var spirit=world.transform.Find(Test_CheonSpiritWidth.MarkerName+"/Test_SpiritWalkableGround").GetComponent<UnityEngine.BoxCollider>();
var camera=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonCamera>().GetComponent<UnityEngine.Camera>();
var cameraData=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
var volume=world.GetComponentInChildren<UnityEngine.Rendering.Volume>();
var shaderErrors=new System.Collections.Generic.List<string>();
foreach(string name in new[]{"Cheon/Test/ConceptSurface","Cheon/Test/ConceptSky"})
foreach(var message in UnityEditor.ShaderUtil.GetShaderMessages(UnityEngine.Shader.Find(name)))
    if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)shaderErrors.Add(message.message);
var result=new{originalObjects=records.Count,preservedObjects=records.Count-missing.Count,missing=missing.ToArray(),moved=moved.ToArray(),resized=resized.ToArray(),meshBoundsChanged=meshBoundsChanged.ToArray(),collisionChanged=collisionChanged.ToArray(),realityWidth=real.size.x,spiritWidth=spirit.size.x,totalLength=real.size.z+spirit.size.z,shaderErrors=shaderErrors.ToArray(),postProcessing=cameraData.renderPostProcessing,volumeLayerIncluded=(cameraData.volumeLayerMask.value & (1<<volume.gameObject.layer))!=0,volumeComponents=volume.sharedProfile.components.Count,skybox=camera.clearFlags.ToString()};
System.IO.File.WriteAllText("Assets/Temp/Test_CheonConceptAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
return result;
