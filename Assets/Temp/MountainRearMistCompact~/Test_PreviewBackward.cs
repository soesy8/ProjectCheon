var game=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();game.BeginJourney();game.Boss.enabled=false;
var movement=game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();movement.InputEnabled=false;
var view=game.View;view.enabled=true;view.LockBoss(false);view.SnapBehind();
var cameraValues=new SerializedObject(view);cameraValues.FindProperty("pitch").floatValue=-40f;cameraValues.ApplyModifiedPropertiesWithoutUndo();
typeof(Cheon.Prototype.MountainPass.PassCamera).GetField("yaw",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(view,game.Player.eulerAngles.y+180f);
return new{frame=Time.frameCount,player=game.Player.position.ToString()};
