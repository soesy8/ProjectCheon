    public static void Boundaries()
    {
        var game=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();var player=game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();var body=player.GetComponent<CharacterController>();var records=new System.Collections.Generic.List<object>();
        System.Collections.IEnumerator Routine()
        {
            game.BeginJourney();game.Boss.enabled=false;player.InputEnabled=false;var start=player.transform.position;var first=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainPassRefinement~/Test_StepGeometry.json"));var a=first[0]["center"];var b=first[1]["center"];var backwards=new Vector3((float)a[0]-(float)b[0],0,(float)a[2]-(float)b[2]).normalized;
            for(int frame=0;frame<30;frame++){body.Move(backwards*.25f-Vector3.up*.015f);yield return null;}records.Add(new{name="rearEndCap",passed=Vector3.Dot(player.transform.position-start,backwards)<1.3f&&player.transform.position.y>start.y-.3f,distance=Vector3.Dot(player.transform.position-start,backwards)});
            var segments=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainPassPolish~/Test_OuterBoundarySegments.json"));int index=0;
            foreach(var group in segments.GroupBy(d=>(string)d["floorName"]))
            {
                var center=GameObject.Find("MountainPassEnvironment/"+group.Key).GetComponent<Renderer>().bounds.center;foreach(var segment in group.Where((d,i)=>i%23==0))
                {
                    var p=segment["first"];var q=segment["last"];float height=(float)segment["height"];var mid=new Vector3(((float)p[0]+(float)q[0])*.5f,height+.04f,((float)p[1]+(float)q[1])*.5f);var tangent=new Vector3((float)q[0]-(float)p[0],0,(float)q[1]-(float)p[1]).normalized;var outward=Vector3.Cross(tangent,Vector3.up);if(Vector3.Dot(outward,mid-center)<0)outward=-outward;
                    player.Teleport(mid-outward*2,Quaternion.LookRotation(outward));for(int frame=0;frame<20;frame++){body.Move(outward*.25f);yield return null;}float penetration=Vector3.Dot(player.transform.position-mid,outward);records.Add(new{name="outerRim_"+index++,passed=penetration<-.1f,penetration});
                }
            }
            foreach(string name in new[]{"Pine_02","Pine_Shrine_04","Pine_Arena_03"})
            {
                var item=GameObject.Find("MountainPassEnvironment/"+name);var collision=item.GetComponent<MeshCollider>();var bounds=collision.bounds;var origin=new Vector3(bounds.center.x,bounds.min.y+1,bounds.max.z+3);RaycastHit hit;bool found=collision.Raycast(new Ray(origin,Vector3.back),out hit,bounds.size.z+6);
                if(found){player.Teleport(new Vector3(hit.point.x,bounds.min.y+.06f,hit.point.z+2),Quaternion.LookRotation(Vector3.back));for(int frame=0;frame<20;frame++){body.Move(Vector3.back*.22f);yield return null;}records.Add(new{name="tree_"+name,passed=player.transform.position.z>hit.point.z+.15f});}else records.Add(new{name="tree_"+name,passed=false});
            }
            game.BeginBossChallenge();game.Boss.enabled=false;game.Entrance.SkipPresentation();player.InputEnabled=false;var gate=game.Entrance.transform;for(int side=-2;side<=2;side++)
            {
                var position=gate.TransformPoint(new Vector3(side*2.6f,0,2));position.y=37.23f;player.Teleport(position,Quaternion.LookRotation(-gate.forward));for(int frame=0;frame<30;frame++){body.Move(-gate.forward*.2f);yield return null;}float localZ=gate.InverseTransformPoint(player.transform.position).z;records.Add(new{name="gateRetreat_"+side,passed=localZ>-.15f,localZ});
            }
            var report=new{records,allPassed=records.All(r=>(bool)r.GetType().GetProperty("passed").GetValue(r)),completed=true};System.IO.File.WriteAllText("Assets/Temp/MountainPassPolish~/Test_CollisionReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));game.BeginJourney();game.Boss.enabled=true;
        }
        game.StartCoroutine(Routine());
    }
