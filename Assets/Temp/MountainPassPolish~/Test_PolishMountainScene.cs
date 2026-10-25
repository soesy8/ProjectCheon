using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Cheon.Prototype.MountainPass;

public class Test_PolishMountainScene : MonoBehaviour
{
    const string Root = "Assets/Proto/MountainPass/";
    static Vector3[] centers, left, right;
    static GameObject ReplaceRoot(string name)
    {
        var old = GameObject.Find(name); if (old != null) DestroyImmediate(old);
        return new GameObject(name);
    }
    static void ReadStairs()
    {
        var data = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainPassRefinement~/Test_StepGeometry.json"));
        centers = new Vector3[data.Count]; left = new Vector3[data.Count]; right = new Vector3[data.Count];
        for (int i = 0; i < data.Count; i++)
        {
            var d = data[i]; centers[i] = new Vector3((float)d["center"][0], (float)d["center"][1], (float)d["center"][2]);
            var sideways = new Vector3((float)d["right"][0], 0, (float)d["right"][2]);
            float l = -1000, r = 1000;
            for (int j = Mathf.Max(0,i-1); j <= Mathf.Min(data.Count-1,i+1); j++) { l = Mathf.Max(l,(float)data[j]["left"]); r = Mathf.Min(r,(float)data[j]["rightWidth"]); }
            left[i] = centers[i] + sideways * (l + .15f); right[i] = centers[i] + sideways * (r - .15f);
        }
    }
    static Vector3[] Smooth(Vector3[] points)
    {
        var smoothed = new Vector3[points.Length];
        for(int i=0;i<points.Length;i++) { Vector3 sum=Vector3.zero;float weight=0;for(int j=-2;j<=2;j++){int k=Mathf.Clamp(i+j,0,points.Length-1);float w=3-Mathf.Abs(j);sum+=points[k]*w;weight+=w;}smoothed[i]=sum/weight; }
        smoothed[0]=points[0];smoothed[points.Length-1]=points[points.Length-1];return smoothed;
    }
    static Vector3 Spline(Vector3[] p,float index)
    {
        int i=Mathf.Min(Mathf.FloorToInt(index),p.Length-2);float t=index-i;
        Vector3 a=i>0?p[i-1]:p[0]*2-p[1], b=p[i], c=p[i+1],d=i+2<p.Length?p[i+2]:p[i+1]*2-p[i];
        return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
    }
    static Mesh Rail(Vector3[] path,string name)
    {
        var profile=new[]{new Vector2(-.32f,-.14f),new Vector2(.32f,-.14f),new Vector2(.38f,-.03f),new Vector2(.38f,.51f),new Vector2(.28f,.67f),new Vector2(-.28f,.67f),new Vector2(-.38f,.51f),new Vector2(-.38f,-.03f)};
        int rings=(path.Length-1)*4+1;var vertices=new List<Vector3>();var uvs=new List<Vector2>();var triangles=new List<int>();float distance=0;Vector3 previous=path[0];
        for(int i=0;i<rings;i++)
        {
            float sample=i*.25f;var point=Spline(path,sample);distance+=Vector3.Distance(previous,point);previous=point;
            Vector3 tangent=Spline(path,Mathf.Min(path.Length-1,sample+.03f))-Spline(path,Mathf.Max(0,sample-.03f));tangent.y=0;var normal=Vector3.Cross(Vector3.up,tangent.normalized);
            for(int k=0;k<profile.Length;k++){vertices.Add(point+normal*profile[k].x+Vector3.up*profile[k].y);uvs.Add(new Vector2(.281f+Mathf.Repeat(distance*.24f,1)*.055f,.575f+Mathf.InverseLerp(-.14f,.67f,profile[k].y)*.19f));}
            if(i==0)continue;for(int k=0;k<8;k++){int a=(i-1)*8+k,b=(i-1)*8+(k+1)%8,c=i*8+k,d=i*8+(k+1)%8;triangles.AddRange(new[]{a,b,c,b,d,c});}
        }
        for(int k=1;k<7;k++){triangles.AddRange(new[]{0,k+1,k});int e=(rings-1)*8;triangles.AddRange(new[]{e,e+k,e+k+1});}
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"Environment/"+name+".asset");bool create=mesh==null;if(create)mesh=new Mesh{name=name};else mesh.Clear();
        mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();if(create)AssetDatabase.CreateAsset(mesh,Root+"Environment/"+name+".asset");EditorUtility.SetDirty(mesh);return mesh;
    }
    static BoxCollider Wall(string name,Transform parent,Vector3 a,Vector3 b,float height,float thickness,float bottom)
    {
        var delta=b-a;delta.y=0;var item=new GameObject(name);item.transform.SetParent(parent);item.layer=2;
        var center=(a+b)*.5f;center.y=bottom+height*.5f;item.transform.SetPositionAndRotation(center,Quaternion.LookRotation(delta));
        var box=item.AddComponent<BoxCollider>();box.size=new Vector3(thickness,height,delta.magnitude+.25f);return box;
    }
    public static void StoneAndRearWall()
    {
        ReadStairs();left=Smooth(left);right=Smooth(right);
        var source=GameObject.Find("ApproachLanterns").GetComponentsInChildren<MeshRenderer>().First().sharedMaterial;
        var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/M_StairParapet.mat");EditorUtility.CopySerialized(source,material);material.name="M_StairParapet";if(material.HasProperty("_EmissionStrength"))material.SetFloat("_EmissionStrength",0);EditorUtility.SetDirty(material);
        for(int side=0;side<2;side++)
        {
            var points=side==0?left:right;string name=side==0?"ApproachLeftParapet":"ApproachRightParapet";var mesh=Rail(points,name);var item=GameObject.Find(name);item.GetComponent<MeshFilter>().sharedMesh=mesh;item.GetComponent<MeshRenderer>().sharedMaterial=material;
            var collision=item.GetComponent<MeshCollider>();if(collision==null)collision=item.AddComponent<MeshCollider>();collision.sharedMesh=null;collision.sharedMesh=mesh;
            for(int step=2;step<centers.Length;step+=9)GameObject.Find("ApproachLanterns").transform.Find((side==0?"LeftLantern_":"RightLantern_")+step.ToString("00")).position=points[step]+Vector3.up*.69f;
        }
        var root=ReplaceRoot("ApproachRearSafetyWalls");var backward=centers[0]-centers[1];backward.y=0;backward.Normalize();Vector3 backLeft=left[0]+backward*1.15f,backRight=right[0]+backward*1.15f;
        Wall("RearEndCap",root.transform,backLeft,backRight,9f,.8f,centers[0].y-2f);Wall("RearLeftReturn",root.transform,left[0],backLeft,9f,.8f,centers[0].y-2f);Wall("RearRightReturn",root.transform,right[0],backRight,9f,.8f,centers[0].y-2f);
        Save();
    }
    static Vector3[] WorldVertices(MeshFilter filter)
    {
        using(var data=Mesh.AcquireReadOnlyMeshData(filter.sharedMesh))using(var vertices=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp)) {data[0].GetVertices(vertices);return vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();}
    }
    static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
    static Vector2[] Hull(Vector3[] vertices)
    {
        var points=vertices.Select(p=>new Vector2(p.x,p.z)).Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();var lower=new List<Vector2>();var upper=new List<Vector2>();
        foreach(var p in points){while(lower.Count>=2&&Cross(lower[lower.Count-2],lower.Last(),p)<=0)lower.RemoveAt(lower.Count-1);lower.Add(p);}
        foreach(var p in points.Reverse()){while(upper.Count>=2&&Cross(upper[upper.Count-2],upper.Last(),p)<=0)upper.RemoveAt(upper.Count-1);upper.Add(p);}lower.RemoveAt(lower.Count-1);upper.RemoveAt(upper.Count-1);return lower.Concat(upper).ToArray();
    }
    public static void OuterBoundaries()
    {
        ReadStairs();var root=ReplaceRoot("MountainOuterSafetyWalls");var report=new List<object>();
        var stairs=GameObject.Find("MountainPassEnvironment/Stairway_Between_Summits").GetComponent<MeshFilter>();var stairVertices=WorldVertices(stairs);var stairCenters=stairVertices.GroupBy(p=>Math.Round(p.y,2)).Where(g=>g.Count()>3).Select(g=>new Vector2(g.Average(p=>p.x),g.Average(p=>p.z))).ToArray();var longEnds=new[]{new Vector2(centers.Last().x,centers.Last().z)};
        foreach(string floorName in new[]{"Shrine_Floor_Flat_UV","Arena_Floor_Flat_UV"})
        {
            var filter=GameObject.Find("MountainPassEnvironment/"+floorName).GetComponent<MeshFilter>();var points=WorldVertices(filter);var hull=Hull(points);float top=points.Max(p=>p.y);var centroid=hull.Aggregate(Vector2.zero,(s,p)=>s+p)/hull.Length;var group=new GameObject(floorName+"_RimBarrier");group.transform.SetParent(root.transform);int count=0;
            for(int i=0;i<hull.Length;i++)
            {
                var a=hull[i];var b=hull[(i+1)%hull.Length];int segments=Mathf.CeilToInt(Vector2.Distance(a,b)/1.5f);
                for(int j=0;j<segments;j++)
                {
                    var first=Vector2.Lerp(a,b,(float)j/segments);var last=Vector2.Lerp(a,b,(float)(j+1)/segments);var mid=(first+last)*.5f;
                    if(stairCenters.Any(p=>Vector2.Distance(p,mid)<5f)||floorName.StartsWith("Shrine")&&longEnds.Any(p=>Vector2.Distance(p,mid)<8f))continue;
                    Vector2 inward=(centroid-mid).normalized*.15f;first+=inward;last+=inward;
                    Wall("Rim_"+count.ToString("000"),group.transform,new Vector3(first.x,top,first.y),new Vector3(last.x,top,last.y),14f,.85f,top-.2f);report.Add(new{floorName,first=new[]{first.x,first.y},last=new[]{last.x,last.y},height=top});count++;
                }
            }
        }
        System.IO.File.WriteAllText("Assets/Temp/MountainPassPolish~/Test_OuterBoundarySegments.json",Newtonsoft.Json.JsonConvert.SerializeObject(report));Save();
    }
    public static void FitPortal()
    {
        var entrance=FindFirstObjectByType<PassBossEntrance>();var gate=entrance.transform;var veil=gate.Find("SpiritPortalVeil");veil.localPosition=new Vector3(0,.65f,.06f);veil.localScale=new Vector3(7.3f,9.4f,1);
        var particles=gate.Find("AscendingSpiritMotes").GetComponent<ParticleSystem>();var shape=particles.shape;shape.scale=new Vector3(7.1f,8.9f,.24f);var emission=particles.emission;emission.rateOverTime=65;gate.Find("SpiritPortalOutline").GetComponent<LineRenderer>().enabled=false;
        var barrier=gate.Find("BossEncounterRetreatBarrier").GetComponent<BoxCollider>();barrier.transform.localPosition=new Vector3(0,.3f,-.7f);barrier.size=new Vector3(11.5f,12f,.8f);barrier.enabled=false;
        var serialized=new SerializedObject(entrance);serialized.FindProperty("openingSize").vector2Value=new Vector2(7.4f,10f);serialized.ApplyModifiedPropertiesWithoutUndo();Save();
    }
    static PassHeightFog Fog(string name,Vector3 position,Vector2 size,float depth,float topFade,float edge,Color color,float density,float opacity,Transform clearTarget,float radius)
    {
        var root=new GameObject(name);root.transform.position=position;var fog=root.AddComponent<PassHeightFog>();var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="FogVolume";cube.transform.SetParent(root.transform,false);DestroyImmediate(cube.GetComponent<Collider>());var renderer=cube.GetComponent<MeshRenderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        var material=new Material(Shader.Find("Cheon/Prototype/LocalMountainFog")){name="M_"+name};AssetDatabase.CreateAsset(material,Root+"Materials/"+material.name+".mat");renderer.sharedMaterial=material;fog.ConfigureVolume(renderer,size,depth,topFade,edge,color,density,opacity,clearTarget,radius);return fog;
    }
    public static void SetupFog()
    {
        var game=FindFirstObjectByType<PassGameFlow>();var player=game.Player;
        var approach=Fog("ApproachMountainMist",new Vector3(-12,24.2f,72),new Vector2(76,114),85,3,8,new Color(.64f,.72f,.73f),.13f,.985f,player,9.5f);
        ReadStairs();var backward=centers[0]-centers[1];backward.y=0;backward.Normalize();var rearPosition=centers[0]+backward*17;rearPosition.y=24.5f;
        var rear=Fog("RearMistCurtain",rearPosition,new Vector2(75,44),75,5,6,new Color(.62f,.71f,.73f),.24f,1,player,7f);
        var arena=Fog("BossAftermathMist",new Vector3(-26,44,-67),new Vector2(140,130),24,5,9,new Color(.49f,.56f,.65f),0,.43f,player,9f);
        var atmosphere=GameObject.Find("MountainPassSystems").AddComponent<PassMountainMist>();atmosphere.Configure(game,approach,rear,arena);Save();
    }
    public static void RefineValleyFog()
    {
        var fog=GameObject.Find("HeightFog_VFX").GetComponent<PassHeightFog>();var renderer=fog.GetComponentInChildren<MeshRenderer>();
        var material=new Material(renderer.sharedMaterial){name="M_ValleyHeightFog"};material.shader=Shader.Find("Cheon/Prototype/LocalMountainFog");AssetDatabase.CreateAsset(material,Root+"Materials/M_ValleyHeightFog.mat");renderer.sharedMaterial=material;
        var serialized=new SerializedObject(fog);serialized.FindProperty("clearTarget").objectReferenceValue=FindFirstObjectByType<PassGameFlow>().Player;serialized.FindProperty("clearRadius").floatValue=9.5f;serialized.FindProperty("clearStrength").floatValue=1f;serialized.ApplyModifiedPropertiesWithoutUndo();fog.ApplySettings();
        foreach(var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)){camera.depthTextureMode|=DepthTextureMode.Depth;var data=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();if(data!=null)data.requiresDepthTexture=true;}Save();
    }
    public static void SealStairConnections()
    {
        ReadStairs();left=Smooth(left);right=Smooth(right);var root=ReplaceRoot("StairConnectionSafetyWalls");var collider=GameObject.Find("MountainPassEnvironment/Stairway_Between_Summits").GetComponent<MeshCollider>();var bounds=collider.bounds;var samples=new List<Vector3>();
        for(float x=bounds.min.x;x<=bounds.max.x;x+=.2f)for(float z=bounds.min.z;z<=bounds.max.z;z+=.2f){RaycastHit hit;if(collider.Raycast(new Ray(new Vector3(x,bounds.max.y+2,z),Vector3.down),out hit,bounds.size.y+4)&&hit.normal.y>.98f)samples.Add(hit.point);}
        var groups=samples.GroupBy(p=>Math.Round(p.y,2)).Where(g=>g.Count()>=5).OrderBy(g=>g.Key).ToArray();var rows=groups.Select(g=>new Vector3(g.Average(p=>p.x),g.Average(p=>p.y),g.Average(p=>p.z))).ToArray();var l=new Vector3[rows.Length];var r=new Vector3[rows.Length];
        for(int i=0;i<rows.Length;i++)
        {
            Vector3 tangent=rows[Mathf.Min(rows.Length-1,i+1)]-rows[Mathf.Max(0,i-1)];tangent.y=0;Vector3 sideways=Vector3.Cross(Vector3.up,tangent.normalized);float negative=0,positive=0;
            foreach(int direction in new[]{-1,1})for(float amount=.1f;amount<5;amount+=.1f){Vector3 p=rows[i]+sideways*(amount*direction);RaycastHit hit;if(!collider.Raycast(new Ray(p+Vector3.up*.12f,Vector3.down),out hit,.24f)||Mathf.Abs(hit.point.y-rows[i].y)>.06f)break;if(direction<0)negative=-amount;else positive=amount;}
            negative=Mathf.Min(-1.6f,groups[i].Min(p=>Vector3.Dot(p-rows[i],sideways))+.12f);positive=Mathf.Max(1.6f,groups[i].Max(p=>Vector3.Dot(p-rows[i],sideways))-.12f);l[i]=rows[i]+sideways*negative;r[i]=rows[i]+sideways*positive;
        }
        for(int i=0;i<rows.Length-1;i++){float bottom=Mathf.Min(rows[i].y,rows[i+1].y)-1.2f;Wall("SummitLeft_"+i,root.transform,l[i],l[i+1],8,.7f,bottom);Wall("SummitRight_"+i,root.transform,r[i],r[i+1],8,.7f,bottom);}
        var data=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainPassPolish~/Test_OuterBoundarySegments.json"));int links=0;
        foreach(var group in data.GroupBy(d=>(string)d["floorName"]))
        {
            var segments=group.ToArray();for(int i=0;i<segments.Length;i++)
            {
                var last=segments[i]["last"];var first=segments[(i+1)%segments.Length]["first"];var a=new Vector3((float)last[0],(float)segments[i]["height"],(float)last[1]);var b=new Vector3((float)first[0],a.y,(float)first[1]);if(Vector3.Distance(a,b)<.5f)continue;
                Vector3 one,two;if(group.Key.StartsWith("Arena")){one=l.Last();two=r.Last();}else{var midpoint=(a+b)*.5f;var longDistance=Vector3.Distance(midpoint,centers.Last());var shortDistance=Vector3.Distance(midpoint,rows[0]);one=longDistance<shortDistance?left.Last():l[0];two=longDistance<shortDistance?right.Last():r[0];}
                var midpointXZ=(a+b)*.5f;bool longConnection=group.Key.StartsWith("Shrine")&&Vector3.Distance(midpointXZ,centers.Last())<Vector3.Distance(midpointXZ,rows[0]);var leftPath=longConnection?left:l;var rightPath=longConnection?right:r;Func<Vector3,Vector3[],Vector3> closest=(point,path)=>path.OrderBy(v=>new Vector2(v.x-point.x,v.z-point.z).sqrMagnitude).First();var al=closest(a,leftPath);var ar=closest(a,rightPath);var bl=closest(b,leftPath);var br=closest(b,rightPath);if((a-al).sqrMagnitude+(b-br).sqrMagnitude<=(a-ar).sqrMagnitude+(b-bl).sqrMagnitude){one=al;two=br;}else{one=ar;two=bl;}
                Wall("FloorConnection_"+links++,root.transform,a,one,9,.8f,Mathf.Min(a.y,Mathf.Min(one.y,two.y))-1);Wall("FloorConnection_"+links++,root.transform,b,two,9,.8f,Mathf.Min(a.y,Mathf.Min(one.y,two.y))-1);
            }
        }
        System.IO.File.WriteAllText("Assets/Temp/MountainPassPolish~/Test_ConnectionSummary.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{stairs=rows.Length,links,leftWidth=rows.Select((p,i)=>Vector3.Distance(p,l[i])).Min(),rightWidth=rows.Select((p,i)=>Vector3.Distance(p,r[i])).Min()}));Save();
    }
    static void Save(){AssetDatabase.SaveAssets();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);}
}
