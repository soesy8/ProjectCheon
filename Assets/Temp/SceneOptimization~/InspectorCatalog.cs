using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class InspectorCatalog
{
    public static object Main()
    {
        var fields=new List<object>();var visited=new HashSet<Type>();
        foreach(string path in Directory.GetFiles("Assets/Scripts/MountainPass","*.cs").Concat(new[]{"Assets/Scripts/Systems/FrameRateLimiter.cs"}))
        {
            Type type=AssetDatabase.LoadAssetAtPath<MonoScript>(path.Replace('\\','/'))?.GetClass();
            if(type!=null)Collect(type,path.Replace('\\','/'),fields,visited);
        }
        File.WriteAllText("Assets/Temp/SceneOptimization~/InspectorFields.json",Newtonsoft.Json.JsonConvert.SerializeObject(fields,Newtonsoft.Json.Formatting.Indented));
        return new{fields=fields.Count,report="Assets/Temp/SceneOptimization~/InspectorFields.json"};
    }
    private static void Collect(Type type,string path,List<object> result,HashSet<Type> visited)
    {
        if(!visited.Add(type))return;
        foreach(var field in type.GetFields(BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
        {
            if(field.IsStatic||field.IsInitOnly||field.IsNotSerialized||field.IsPrivate&&!Attribute.IsDefined(field,typeof(SerializeField)))continue;
            if(!field.IsPublic&&!Attribute.IsDefined(field,typeof(SerializeField)))continue;
            var label=field.GetCustomAttribute<InspectorNameAttribute>();
            result.Add(new{path,type=type.Name,name=field.Name,label=label?.displayName,fieldType=field.FieldType.Name});
            Type nested=field.FieldType.IsArray?field.FieldType.GetElementType():field.FieldType;
            if(nested!=null && nested.IsSerializable && nested.Namespace==type.Namespace && !nested.IsEnum && !typeof(UnityEngine.Object).IsAssignableFrom(nested))Collect(nested,path,result,visited);
        }
    }
}
