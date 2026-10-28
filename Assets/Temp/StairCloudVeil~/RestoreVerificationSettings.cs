Application.runInBackground = UnityEditor.SessionState.GetBool("Cheon.StairCloud.PreviousRunInBackground", true);
UnityEditor.SessionState.EraseBool("Cheon.StairCloud.PreviousRunInBackground");
return new { playing = Application.isPlaying, background = Application.runInBackground,
    dirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty };
