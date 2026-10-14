var renderers=UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None);
var report=new System.Collections.Generic.List<object>();
foreach(var r in renderers)
{
    if(r.bounds.size.magnitude>60 || r.sharedMaterial.shader.name.Contains("Sky"))report.Add(new{name=r.name,size=r.bounds.size.ToString(),position=r.bounds.center.ToString(),material=r.sharedMaterial.name,shader=r.sharedMaterial.shader.name});
}
return report;
