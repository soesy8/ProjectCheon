var data=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainParapetBend~/Test_Curves.json"));
var ground=GameObject.Find("MountainPassEnvironment/Stairway_Winding_Approach").GetComponent<MeshCollider>();
var records=new System.Collections.Generic.List<object>();
foreach(var rail in data){int focus=((string)rail["name"]).Contains("Left")?66:54;for(int i=focus-5;i<=focus+5;i++){var p=rail["path"][i];var point=new Vector3((float)p[0],(float)p[1],(float)p[2]);RaycastHit hit;bool support=ground.Raycast(new Ray(point+Vector3.up*3,Vector3.down),out hit,6);records.Add(new{rail=(string)rail["name"],step=i,support=support,heightDelta=support?hit.point.y-point.y:-999f});}}
return records;
