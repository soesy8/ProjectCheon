var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
view.Show();view.Focus();
return new UnityEngine.GameObject("Test_RealmLayoutVerification").AddComponent<Test_CheonRealmLayoutVerification>().name;
