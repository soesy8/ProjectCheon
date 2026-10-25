var game=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
game.BeginJourney();game.Boss.enabled=false;
var data=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainPassRefinement~/Test_StepGeometry.json"));
var p=data[54]["center"];var point=new Vector3((float)p[0],(float)p[1]+.05f,(float)p[2]);
var player=game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();player.Teleport(point,Quaternion.identity);player.InputEnabled=false;
game.View.enabled=false;
var focus=Vector3.Lerp(point,GameObject.Find("ApproachLanterns/RightLantern_56").transform.position,.35f);
game.View.transform.SetPositionAndRotation(focus+new Vector3(-4.5f,5.5f,-3.5f),Quaternion.LookRotation(new Vector3(4.5f,-5.5f,3.5f)));
return new{frame=Time.frameCount,player=point.ToString()};
