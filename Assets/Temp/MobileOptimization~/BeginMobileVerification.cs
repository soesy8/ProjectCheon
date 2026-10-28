var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
game.BeginJourney();
return new { frame = UnityEngine.Time.frameCount, state = game.State.ToString(), background = UnityEngine.Application.runInBackground };
