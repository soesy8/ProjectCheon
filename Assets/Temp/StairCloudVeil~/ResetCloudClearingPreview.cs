var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var movement = game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();
movement.Teleport(new Vector3(-19.42f, 3.76f, 100.26f), Quaternion.LookRotation(new Vector3(0f, 0f, -1f)));
movement.InputEnabled = false;
game.View.SnapBehind();
Time.timeScale = 1f;
return game.Player.position.ToString();
