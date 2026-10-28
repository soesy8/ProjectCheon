var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var floor = (Renderer)new SerializedObject(game).FindProperty("bossDetectionFloor").objectReferenceValue;
Vector3 position = floor.bounds.center + Vector3.forward * 8f;
position.y = floor.bounds.max.y + 0.15f;
var movement = game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();
movement.Teleport(position, Quaternion.LookRotation(Vector3.ProjectOnPlane(game.Boss.transform.position - position, Vector3.up)));
movement.InputEnabled = false;
game.View.SnapBehind();
return position.ToString();
