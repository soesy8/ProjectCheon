var rows = new List<object>();
foreach(var button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)) {
 var point = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
 rows.Add(new {name=button.name,x=point.x,y=point.y});
}
return new {width=Screen.width,height=Screen.height,frame=Time.frameCount,buttons=rows};
