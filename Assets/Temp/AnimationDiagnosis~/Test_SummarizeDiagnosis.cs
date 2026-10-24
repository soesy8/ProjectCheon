var rows = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/AnimationDiagnosis~/Test_PoseComparison.json"));
Quaternion Q(Newtonsoft.Json.Linq.JToken token) => new Quaternion((float)token[0], (float)token[1], (float)token[2], (float)token[3]);
Quaternion Combined(Newtonsoft.Json.Linq.JToken sample) => Q(sample["neck"]) * Q(sample["head"]);
var summary = new List<object>();
for (int attack = 1; attack <= 3; attack++)
{
    var original = (Newtonsoft.Json.Linq.JArray)rows.First(r => (int)r["attack"] == attack && (string)r["mode"] == "original")["data"]["samples"];
    var neutral = (Newtonsoft.Json.Linq.JArray)rows.First(r => (int)r["attack"] == attack && (string)r["mode"] == "neutralAvatar")["data"]["samples"];
    var raw = (Newtonsoft.Json.Linq.JArray)rows.First(r => (int)r["attack"] == attack && (string)r["mode"] == "rawGeneric")["data"]["samples"];
    var deviations = Enumerable.Range(0, raw.Count).Select(f => new {frame=f, originalError=Quaternion.Angle(Combined(original[f]),Combined(raw[f])), neutralError=Quaternion.Angle(Combined(neutral[f]),Combined(raw[f]))}).ToArray();
    var maxStep = raw.OrderByDescending(s => (float)s["headStep"]).First();
    summary.Add(new { attack, sampledFrames=raw.Count,
        maxOriginalDeviationDegrees=deviations.Max(d=>d.originalError),
        meanOriginalDeviationDegrees=deviations.Average(d=>d.originalError),
        maxCorrectedDeviationDegrees=deviations.Max(d=>d.neutralError),
        originalMaxCombinedDegrees=original.Max(s=>(float)s["headRelativeChestAngle"]),
        correctedMaxCombinedDegrees=neutral.Max(s=>(float)s["headRelativeChestAngle"]),
        rawMaxCombinedDegrees=raw.Max(s=>(float)s["headRelativeChestAngle"]),
        rawMaxHeadStepDegrees=(float)maxStep["headStep"], rawMaxHeadStepAtFrame=(int)maxStep["frame"]
    });
}
System.IO.File.WriteAllText("Assets/Temp/AnimationDiagnosis~/Test_DiagnosisSummary.json", Newtonsoft.Json.JsonConvert.SerializeObject(summary,Newtonsoft.Json.Formatting.Indented));
return summary;
