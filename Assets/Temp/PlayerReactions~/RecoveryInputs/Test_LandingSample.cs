var p=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
var result=new System.Collections.Generic.List<object>();
for(int f=0;f<=91;f+=2){p.Scrub(0,f);var a=p.CharacterAnimator;var h=a.GetBoneTransform(HumanBodyBones.Hips).position;var head=a.GetBoneTransform(HumanBodyBones.Head).position;result.Add(new{frame=f,hipY=h.y,hipZ=h.z,headY=head.y,headZ=head.z});}
System.IO.File.WriteAllText("Assets/Temp/PlayerReactions~/RecoveryInputs/Test_LandingSamples.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
return result;
