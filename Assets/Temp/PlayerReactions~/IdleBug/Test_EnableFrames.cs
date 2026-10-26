UnityEditor.SessionState.SetBool("Test_ReactionIdleBug_Background",Application.runInBackground);
UnityEditor.SessionState.SetBool("Test_ReactionIdleBug_Paused",UnityEditor.EditorApplication.isPaused);
var before = new {background=Application.runInBackground,paused=UnityEditor.EditorApplication.isPaused};
Application.runInBackground=true;UnityEditor.EditorApplication.isPaused=false;
return before;
