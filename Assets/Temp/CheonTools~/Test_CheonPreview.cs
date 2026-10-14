var session = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonSession>();
var player = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonPlayer>();
session.BeginTraining();
var controller = player.GetComponent<UnityEngine.CharacterController>();
controller.enabled = false; player.transform.position = new UnityEngine.Vector3(0, 0, 4); controller.enabled = true;
return true;
