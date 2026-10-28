var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
UnityEditor.SessionState.SetBool("Cheon.StairCloud.PreviousRunInBackground", Application.runInBackground);
Application.runInBackground = true;
game.BeginJourney();
return new { frame = Time.frameCount, background = Application.runInBackground };
