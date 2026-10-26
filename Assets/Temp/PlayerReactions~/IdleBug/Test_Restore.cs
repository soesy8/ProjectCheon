Application.runInBackground=UnityEditor.SessionState.GetBool("Test_ReactionIdleBug_Background",true);
UnityEditor.EditorApplication.isPaused=UnityEditor.SessionState.GetBool("Test_ReactionIdleBug_Paused",false);
UnityEditor.SessionState.EraseBool("Test_ReactionIdleBug_Background");
UnityEditor.SessionState.EraseBool("Test_ReactionIdleBug_Paused");
return new {playing=UnityEditor.EditorApplication.isPlaying};
