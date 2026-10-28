var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
var window = UnityEngine.Resources.FindObjectsOfTypeAll(gameViewType).First();
gameViewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).SetValue(window, 7);
return true;
