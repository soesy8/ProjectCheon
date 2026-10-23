SessionState.SetBool("Cheon.GateTransition.RunInBackground", Application.runInBackground);
Application.runInBackground = true;
return new { Application.runInBackground, scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path };
