var game=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
game.BeginJourney();game.Boss.enabled=false;
var data=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainPassRefinement~/Test_StepGeometry.json"));var p=data[63]["center"];
var player=game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();player.Teleport(new Vector3((float)p[0],(float)p[1]+.05f,(float)p[2]),Quaternion.identity);player.InputEnabled=false;
var focus=GameObject.Find("ApproachLanterns/LeftLantern_65").transform.position-Vector3.up*.35f;
game.View.enabled=false;var offset=new Vector3(4.5f,4.8f,5.5f);game.View.transform.SetPositionAndRotation(focus+offset,Quaternion.LookRotation(-offset));
return new{frame=Time.frameCount};
