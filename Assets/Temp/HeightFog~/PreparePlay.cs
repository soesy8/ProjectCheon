SessionState.SetBool("Cheon.HeightFog.RunInBackground", Application.runInBackground);
Application.runInBackground = true;
return new { Application.runInBackground, fog = GameObject.Find("HeightFog_VFX").activeSelf };
