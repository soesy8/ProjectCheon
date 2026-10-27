using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Cheon.Prototype.MountainPass;

public static class Test_SampleFootAnimation
{
    public static object Sample()
    {
        var control=UnityEngine.Object.FindFirstObjectByType<PassAnimationController>();
        if(EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        var original=control.CharacterAnimator;
        var clone=UnityEngine.Object.Instantiate(original.gameObject);
        clone.name="Test_FootSample"; clone.hideFlags=HideFlags.HideAndDontSave;
        clone.transform.SetParent(null);clone.transform.position=Vector3.zero;clone.transform.rotation=Quaternion.identity;
        try
        {
            var animator=clone.GetComponent<Animator>();
            animator.Rebind();
            var clip=original.runtimeAnimatorController.animationClips.First(c=>c.name=="Run2");
            var bones=clone.GetComponentsInChildren<Transform>();
            var left=bones.First(t=>t.name=="mixamorig:LeftFoot");
            var right=bones.First(t=>t.name=="mixamorig:RightFoot");
            var leftToe=bones.First(t=>t.name=="mixamorig:LeftToeBase");
            var rightToe=bones.First(t=>t.name=="mixamorig:RightToeBase");
            var rows=new List<object>();
            for(int i=0;i<=210;i++)
            {
                float frame=i/10f;
                clip.SampleAnimation(clone,frame/clip.frameRate);
                Vector3 l=clone.transform.InverseTransformPoint(left.position),r=clone.transform.InverseTransformPoint(right.position);
                Vector3 lt=clone.transform.InverseTransformPoint(leftToe.position),rt=clone.transform.InverseTransformPoint(rightToe.position);
                rows.Add(new {frame,leftY=l.y,leftZ=l.z,rightY=r.y,rightZ=r.z,leftToeY=lt.y,leftToeZ=lt.z,rightToeY=rt.y,rightToeZ=rt.z});
            }
            System.IO.File.WriteAllText("Assets/Temp/FootstepSync~/Test_FootPoseSamples.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));
            return new {clip=clip.name,frames=21, samples=rows.Count, start=rows[0],middle=rows[105]};
        }
        finally {UnityEngine.Object.DestroyImmediate(clone);}
    }
}
