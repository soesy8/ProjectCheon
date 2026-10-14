var gameView = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
gameView.Show(); gameView.Focus();
return new UnityEngine.GameObject("Test_ExpansionVerification").AddComponent<Test_CheonExpansionVerification>().name;
