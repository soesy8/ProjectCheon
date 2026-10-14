var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
var view = UnityEditor.EditorWindow.GetWindow(type);
view.maximized = true; view.Show(); view.Focus();
UnityEngine.Application.runInBackground = true;
return new { width = UnityEngine.Screen.width, height = UnityEngine.Screen.height, properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static).Select(p => p.Name).ToArray() };
