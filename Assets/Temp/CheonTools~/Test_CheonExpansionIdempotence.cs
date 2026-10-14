var world = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
int before=world.GetComponentsInChildren<UnityEngine.Transform>(true).Length;
var player=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonPlayer>();
var playerPosition=player.transform.position;
Test_CheonMapExpansion.Apply();
return new { duplicateProps = world.GetComponentsInChildren<UnityEngine.Transform>(true).Length-before, playerUnchanged = player.transform.position==playerPosition, floor = world.transform.Find("Test_WalkableGround").GetComponent<UnityEngine.BoxCollider>().size.ToString() };
