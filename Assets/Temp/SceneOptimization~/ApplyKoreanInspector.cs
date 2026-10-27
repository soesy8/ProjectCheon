using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;

public static class ApplyKoreanInspector
{
    public static object Main()
    {
        var labels=File.ReadAllLines("Assets/Temp/SceneOptimization~/InspectorLabels.txt").Where(l=>l.Contains("|")).Select(l=>l.Split(new[]{'|'},2)).ToDictionary(p=>p[0],p=>p[1]);
        var fields=JArray.Parse(File.ReadAllText("Assets/Temp/SceneOptimization~/InspectorFields.json"));
        var missing=fields.Where(f=>f["label"].Type==JTokenType.Null).ToArray();
        var absent=missing.Select(f=>(string)f["name"]).Distinct().Where(n=>!labels.ContainsKey(n)).ToArray();
        if(absent.Length>0)throw new Exception("Missing translations: "+string.Join(",",absent));
        int count=0;
        foreach(var group in missing.GroupBy(f=>(string)f["path"]))
        {
            string source=File.ReadAllText(group.Key);var tree=CSharpSyntaxTree.ParseText(source);var root=tree.GetRoot();
            var wanted=new HashSet<string>(group.Select(f=>(string)f["type"]+"."+(string)f["name"]));
            var edits=new List<TextChange>();
            foreach(var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                var owner=field.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if(owner==null || field.AttributeLists.Any(a=>a.ToString().Contains("InspectorName")))continue;
                string name=field.Declaration.Variables.First().Identifier.ValueText;
                if(!wanted.Contains(owner.Identifier.ValueText+"."+name))continue;
                int indentation=tree.GetLineSpan(new TextSpan(field.SpanStart,0)).StartLinePosition.Character;
                string text="[InspectorName(\""+labels[name]+"\")]\n"+new string(' ',indentation);
                edits.Add(new TextChange(new TextSpan(field.SpanStart,0),text));
            }
            if(edits.Count==0)continue;
            File.WriteAllText(group.Key,tree.GetText().WithChanges(edits).ToString(),new System.Text.UTF8Encoding(false));
            count+=edits.Count;
        }
        return new{translatedFields=count,files=missing.Select(f=>(string)f["path"]).Distinct().Count()};
    }
}
