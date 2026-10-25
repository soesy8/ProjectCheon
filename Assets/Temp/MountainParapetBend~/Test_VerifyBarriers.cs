var data=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainParapetBend~/Test_Curves.json"));
var centers=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainPassRefinement~/Test_StepGeometry.json"));
var checks=new System.Collections.Generic.List<object>();
foreach(var rail in data){var path=rail["path"].Select(p=>new Vector3((float)p[0],(float)p[1],(float)p[2])).ToArray();var old=rail["original"].Select(p=>new Vector3((float)p[0],(float)p[1],(float)p[2])).ToArray();
for(int i=0;i<path.Length-1;i++){if(Vector3.Distance(path[i],old[i])<.005f&&Vector3.Distance(path[i+1],old[i+1])<.005f)continue;
for(int sample=0;sample<4;sample++){
float t=(sample+.5f)/4;var anchor=Vector3.Lerp(path[i],path[i+1],t)+Vector3.up*.9f;
var a=centers[i]["center"];var b=centers[i+1]["center"];var middle=Vector3.Lerp(new Vector3((float)a[0],(float)a[1],(float)a[2]),new Vector3((float)b[0],(float)b[1],(float)b[2]),t);
var outward=anchor-middle;outward.y=0;outward.Normalize();RaycastHit hit;bool blocked=Physics.Raycast(anchor-outward*1.5f,outward,out hit,3f,1<<2,QueryTriggerInteraction.Ignore);
checks.Add(new{rail=(string)rail["name"],step=i,sample,blocked});
}}}
var report=new{frames=Time.frameCount,tests=checks.Count,allBlocked=checks.All(c=>(bool)c.GetType().GetProperty("blocked").GetValue(c)),failed=checks.Where(c=>!(bool)c.GetType().GetProperty("blocked").GetValue(c)).ToArray()};
System.IO.File.WriteAllText("Assets/Temp/MountainParapetBend~/Test_BarrierReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
