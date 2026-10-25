var rails=GameObject.Find("ApproachStoneParapets").GetComponentsInChildren<MeshFilter>();
var report=rails.Select(filter=>{
var vertices=filter.sharedMesh.vertices;var rings=new Vector3[vertices.Length/8];
for(int i=0;i<rings.Length;i++){for(int k=0;k<8;k++)rings[i]+=filter.transform.TransformPoint(vertices[i*8+k]);rings[i]/=8;}
var bends=Enumerable.Range(4,rings.Length-8).Where(i=>i%4==0).Select(i=>{var a=rings[i]-rings[i-4];var b=rings[i+4]-rings[i];a.y=0;b.y=0;return new{ring=i,step=i/4,turn=Vector3.Angle(a,b),point=new[]{rings[i].x,rings[i].y,rings[i].z}};}).OrderByDescending(x=>x.turn).Take(12).ToArray();
return new{name=filter.name,asset=AssetDatabase.GetAssetPath(filter.sharedMesh),position=filter.transform.position.ToString(),scale=filter.transform.lossyScale.ToString(),vertices=vertices.Length,bends=bends,rings=rings.Select(p=>new[]{p.x,p.y,p.z}).ToArray()};
}).ToArray();
System.IO.File.WriteAllText("Assets/Temp/MountainParapetBend~/Test_Inspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return report.Select(x=>new{x.name,x.asset,x.position,x.scale,x.vertices,x.bends}).ToArray();
