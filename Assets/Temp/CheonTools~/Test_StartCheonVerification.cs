var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
var gameView = UnityEditor.EditorWindow.GetWindow(gameViewType);
gameView.Show(); gameView.Focus();
if (UnityEngine.Object.FindFirstObjectByType<Test_CheonPlayVerification>() == null)
    new UnityEngine.GameObject("Test_PlayVerification").AddComponent<Test_CheonPlayVerification>();
return new { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, width = UnityEngine.Screen.width, height = UnityEngine.Screen.height };
