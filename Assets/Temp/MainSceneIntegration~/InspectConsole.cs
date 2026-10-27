using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
public static class InspectConsole
{
    public static object Main()
    {
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
        var assembly=typeof(EditorApplication).Assembly;
        var entries=assembly.GetType("UnityEditor.LogEntries");
        var entryType=assembly.GetType("UnityEditor.LogEntry");
        int count=(int)entries.GetMethod("GetCount",flags).Invoke(null,null);
        var result=Enumerable.Range(0,count).Select(index=>{
            var entry=Activator.CreateInstance(entryType);
            entries.GetMethod("GetEntryInternal",flags).Invoke(null,new[]{(object)index,entry});
            return new { message=entryType.GetField("message").GetValue(entry),mode=entryType.GetField("mode").GetValue(entry) };
        }).ToArray();
        return new { count,result };
    }
}
