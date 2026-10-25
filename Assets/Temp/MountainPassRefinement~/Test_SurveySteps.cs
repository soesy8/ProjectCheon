var root=GameObject.Find("MountainPassEnvironment/Shrine_Building_Placement/BuildingBase_Parts");
var stairs=root.transform.Find("Stairs").GetComponent<MeshFilter>();
var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(stairs.sharedMesh);
var vertices=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp);data[0].GetVertices(vertices);
var points=vertices.Select(v=>stairs.transform.TransformPoint(v)).ToArray();
var shrineAxis=new Vector3(1f,0f,-1f).normalized;
var stairCenter=stairs.GetComponent<Renderer>().bounds.center;
var levels=points.GroupBy(v=>System.Math.Round(v.y,3)).OrderBy(g=>g.Key).Select(g=>new{y=g.Key,count=g.Count(),alongMin=g.Min(v=>Vector3.Dot(v-stairCenter,shrineAxis)),alongMax=g.Max(v=>Vector3.Dot(v-stairCenter,shrineAxis)),x=g.Average(v=>v.x),z=g.Average(v=>v.z)}).ToArray();
vertices.Dispose();data.Dispose();
var longStairs=GameObject.Find("MountainPassEnvironment/Stairway_Winding_Approach").GetComponent<MeshCollider>();
var bounds=longStairs.bounds;var samples=new System.Collections.Generic.List<Vector3>();
for(float x=bounds.min.x;x<=bounds.max.x;x+=0.25f)for(float z=bounds.min.z;z<=bounds.max.z;z+=0.25f){RaycastHit hit;if(longStairs.Raycast(new Ray(new Vector3(x,bounds.max.y+3f,z),Vector3.down),out hit,bounds.size.y+5f)&&hit.normal.y>0.98f)samples.Add(hit.point);}
var groups=samples.GroupBy(p=>System.Math.Round(p.y,2)).Where(g=>g.Count()>=5).OrderBy(g=>g.Key).ToArray();
var centers=groups.Select(g=>new Vector3(g.Average(p=>p.x),g.Average(p=>p.y),g.Average(p=>p.z))).ToArray();
var steps=new System.Collections.Generic.List<object>();
for(int i=0;i<centers.Length;i++){var tangent=centers[Mathf.Min(i+2,centers.Length-1)]-centers[Mathf.Max(0,i-2)];tangent.y=0;var right=Vector3.Cross(Vector3.up,tangent.normalized);float left=groups[i].Min(p=>Vector3.Dot(p-centers[i],right));float rightWidth=groups[i].Max(p=>Vector3.Dot(p-centers[i],right));steps.Add(new{center=new[]{centers[i].x,centers[i].y,centers[i].z},right=new[]{right.x,right.y,right.z},left,rightWidth});}
System.IO.File.WriteAllText("Assets/Temp/MountainPassRefinement~/Test_StepGeometry.json",Newtonsoft.Json.JsonConvert.SerializeObject(steps,Newtonsoft.Json.Formatting.Indented));
return new{shrineStairCenter=stairCenter.ToString("F4"),levels,approachCount=centers.Length,approachFirst=steps.First(),approachLast=steps.Last()};
