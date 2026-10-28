var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var movement = game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();
var veil = game.GetComponent<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
veil.enabled = true;
movement.Teleport(new Vector3(-3.5854f, 24.45f, 43.8872f), Quaternion.LookRotation(new Vector3(0.77002f, 0f, -0.638018f)));
movement.InputEnabled = false;
game.View.SnapBehind();
Time.timeScale = 1f;
return game.Player.position.ToString();
