var game=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var player=game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();var body=player.GetComponent<CharacterController>();
var data=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainPassRefinement~/Test_StepGeometry.json"));
var route=data.Select(d=>new Vector3((float)d["center"][0],(float)d["center"][1]+.04f,(float)d["center"][2])).ToArray();
System.Collections.IEnumerator Routine(){
game.BeginJourney();game.Boss.enabled=false;player.InputEnabled=false;player.Teleport(route[44],Quaternion.identity);yield return null;
float vertical=0;int firstFrame=Time.frameCount;int reached=0;string blocked=null;
for(int i=45;i<=76;i++){
float start=Time.time;
while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(route[i].x,route[i].z))>.35f){
if(Time.time-start>4f){blocked="Step "+i+" "+player.transform.position.ToString();break;}
var direction=route[i]-player.transform.position;direction.y=0;if(body.isGrounded&&vertical<0)vertical=-2;vertical-=24*Time.deltaTime;
var move=Vector3.ClampMagnitude(direction,6.6f*Time.deltaTime);move.y=vertical*Time.deltaTime;body.Move(move);yield return null;
}if(blocked!=null)break;reached++;
}
var report=new{completed=true,framesAdvanced=Time.frameCount-firstFrame,reached,total=32,blocked,passed=reached==32&&blocked==null};
System.IO.File.WriteAllText("Assets/Temp/MountainParapetBend~/Test_RouteReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
}
game.StartCoroutine(Routine());return true;
