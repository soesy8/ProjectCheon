var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var movement = game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();
movement.Teleport(new Vector3(5.654829f, 27.25f, 36.23096f), Quaternion.LookRotation(new Vector3(0.77002f, 0f, -0.638018f)));
game.View.SnapBehind();
Time.timeScale = 1f;
return game.Player.position.ToString();
