var game=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();game.BeginJourney();game.Boss.enabled=false;
var data=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainPassRefinement~/Test_StepGeometry.json"));var p=data[51]["center"];
var player=game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();player.Teleport(new Vector3((float)p[0],(float)p[1]+.05f,(float)p[2]),Quaternion.identity);player.InputEnabled=false;
var rail=GameObject.Find("ApproachRightParapet").GetComponent<MeshFilter>().sharedMesh.vertices;
var focus=Enumerable.Range(0,8).Aggregate(Vector3.zero,(s,k)=>s+rail[54*4*8+k])/8+Vector3.up*.35f;
var offset=new Vector3(4.5f,5.3f,4.5f);game.View.enabled=false;game.View.transform.SetPositionAndRotation(focus+offset,Quaternion.LookRotation(-offset));
return Time.frameCount;
