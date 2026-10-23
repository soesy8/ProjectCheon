Application.runInBackground = SessionState.GetBool("Cheon.HeightFog.RunInBackground", false);
var fog = GameObject.Find("HeightFog_VFX");
Selection.activeGameObject = fog;
return new { playing = EditorApplication.isPlaying, Application.runInBackground, scene = fog.scene.path, fog.scene.isDirty, selected = Selection.activeGameObject.name, cameraPosition = Camera.main.transform.position.ToString() };
