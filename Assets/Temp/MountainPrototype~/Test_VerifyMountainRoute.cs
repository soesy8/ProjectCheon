var game=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var player=game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();
var body=player.GetComponent<CharacterController>();
Vector3[] Steps(string name){
 var collider=GameObject.Find("MountainPassEnvironment/"+name).GetComponent<MeshCollider>();
 var bounds=collider.bounds;var samples=new System.Collections.Generic.List<Vector3>();
 for(float x=bounds.min.x;x<=bounds.max.x;x+=0.38f)for(float z=bounds.min.z;z<=bounds.max.z;z+=0.38f){
  RaycastHit hit;if(collider.Raycast(new Ray(new Vector3(x,bounds.max.y+3f,z),Vector3.down),out hit,bounds.size.y+5f)&&hit.normal.y>0.98f)samples.Add(hit.point);
 }
 return samples.GroupBy(p=>System.Math.Round(p.y,2)).Where(g=>g.Count()>=3).OrderBy(g=>g.Key).Select(g=>new Vector3(g.Average(p=>p.x),g.Average(p=>p.y)+0.04f,g.Average(p=>p.z))).ToArray();
}
var first=Steps("Stairway_Winding_Approach");var second=Steps("Stairway_Between_Summits");
var route=new System.Collections.Generic.List<Vector3>();route.AddRange(first.Skip(1));route.Add(new Vector3(15.75f,27.2f,25.1f));route.AddRange(second);route.Add(new Vector3(-15f,37.2f,-43f));route.Add(new Vector3(-18f,37.2f,-51f));
System.Collections.IEnumerator Routine(){
 game.BeginJourney();game.Boss.enabled=false;player.InputEnabled=false;
 float vertical=0f,started=Time.time;int reached=0;string blocked=null;
 for(int i=0;i<route.Count;i++){
  float nodeStart=Time.time; float nodeBudget=3f+Vector3.Distance(player.transform.position,route[i])/4f;
  while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(route[i].x,route[i].z))>0.45f){
   if(Time.time-nodeStart>nodeBudget||Time.time-started>80f){blocked="node "+i+" target="+route[i].ToString()+" position="+player.transform.position.ToString();break;}
   var direction=route[i]-player.transform.position;direction.y=0f;
   if(body.isGrounded&&vertical<0f)vertical=-2f;vertical+=-24f*Time.deltaTime;
   Vector3 displacement=Vector3.ClampMagnitude(direction,6.6f*Time.deltaTime);displacement.y=vertical*Time.deltaTime;
   if(direction.sqrMagnitude>0.001f)player.transform.rotation=Quaternion.LookRotation(direction);
   player.AnimationControl.SetLocomotion(true,1f);body.Move(displacement);
   yield return null;
  }
  if(blocked!=null)break;reached++;
 }
 player.AnimationControl.SetLocomotion(false);player.InputEnabled=true;
 var report=new{success=blocked==null && game.ReachedShrine && game.InEncounter,reached,total=route.Count,firstSteps=first.Length,secondSteps=second.Length,blocked,shrine=game.ReachedShrine,encounter=game.InEncounter,hp=game.PlayerHealth.Current,seconds=Time.time-started};
 System.IO.File.WriteAllText("Assets/Temp/MountainPrototype~/Test_RouteVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
}
game.StartCoroutine(Routine());return new{started=true,nodes=route.Count};


