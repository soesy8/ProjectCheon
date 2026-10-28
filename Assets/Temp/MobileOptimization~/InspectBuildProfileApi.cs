var type = typeof(UnityEditor.Build.Profile.BuildProfile);
var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
var methods = new System.Collections.Generic.List<string>();
foreach (var method in type.GetMethods(flags))
    if (method.Name.Contains("Create") || method.Name.Contains("Quality"))
        methods.Add(method.ToString());
var properties = new System.Collections.Generic.List<string>();
foreach (var property in type.GetProperties(flags))
    if (property.Name.Contains("platform") || property.Name.Contains("Target") || property.Name.Contains("scene") || property.Name.Contains("quality") || property.Name.Contains("settings"))
        properties.Add(property.Name + ": " + property.PropertyType + "; setter=" + property.SetMethod);
return new { methods, properties };
