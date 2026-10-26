var preview=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
var rawPath="Assets/Temp/PlayerReactionRawProbe/Test_GetUpRaw.fbx";
var raw=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(rawPath));raw.name="Test_RawGetUpProbe";
raw.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
var rawClip=UnityEditor.AssetDatabase.LoadAllAssetsAtPath(rawPath).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
var probe=UnityEngine.Object.Instantiate(preview.CharacterAnimator.gameObject);probe.name="Test_RetargetProbe";
foreach(var renderer in probe.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
var animator=probe.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
probe.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
var graph=UnityEngine.Playables.PlayableGraph.Create("Test_PoseComparison");graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"Probe",animator);graph.Play();
var rawBones=raw.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);
var jointPairs=new [] {new[]{"mixamorig:Hips","mixamorig:Spine"},new[]{"mixamorig:LeftUpLeg","mixamorig:LeftLeg"},new[]{"mixamorig:LeftLeg","mixamorig:LeftFoot"},new[]{"mixamorig:RightUpLeg","mixamorig:RightLeg"},new[]{"mixamorig:RightLeg","mixamorig:RightFoot"},new[]{"mixamorig:LeftArm","mixamorig:LeftForeArm"}};
var result=new List<object>();
foreach(var path in new []{"Assets/Test/PlayerReactions/Animations/Test_GetUp.fbx","Assets/Temp/PlayerReactionRawProbe/Test_GetUpNeutral.fbx"}){
 var clip=UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
 var playable=UnityEngine.Animations.AnimationClipPlayable.Create(graph,clip);UnityEngine.Playables.PlayableExtensions.SetSpeed(playable,0);UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,playable);
 var bones=probe.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
 foreach(var frame in Enumerable.Range(0,59).ToArray()){
  rawClip.SampleAnimation(raw,frame/30f);UnityEngine.Playables.PlayableExtensions.SetTime(playable,frame/30f);graph.Evaluate(0);
  var angles=jointPairs.Select(pair=>Vector3.Angle(rawBones[pair[1]].position-rawBones[pair[0]].position,bones[pair[1]].position-bones[pair[0]].position)).ToArray();
  result.Add(new{variant=path.Contains("Neutral")?"neutral":"current",frame,angles=angles.Select(v=>Mathf.Round(v*10)/10).ToArray(),rawHip=rawBones["mixamorig:Hips"].position.ToString(),hip=animator.GetBoneTransform(HumanBodyBones.Hips).position.ToString(),leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.ToString(),head=animator.GetBoneTransform(HumanBodyBones.Head).position.ToString()});
 }
 graph.DestroyPlayable(playable);
}
graph.Destroy();UnityEngine.Object.DestroyImmediate(raw);UnityEngine.Object.DestroyImmediate(probe);
System.IO.File.WriteAllText("Assets/Temp/PlayerReactions~/RecoveryPolish/Test_PoseComparison.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
var rows=Newtonsoft.Json.Linq.JArray.FromObject(result); return rows.GroupBy(row=>row["variant"].ToString()).Select(group=>new {variant=group.Key,frames=group.Count(),spineMax=group.Max(row=>(float)row["angles"][0]),legMax=group.SelectMany(row=>row["angles"].Skip(1).Take(4).Select(value=>(float)value)).Max()}).ToArray();


