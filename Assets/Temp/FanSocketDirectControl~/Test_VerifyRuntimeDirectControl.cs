if(!Application.isPlaying)throw new Exception("Play mode required");
var attachment=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.PlayerFan.Test_PlayerFanAttachment>();
var control=attachment.GetComponent<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
var socket=attachment.HandSocket;
Vector3 savedPosition=socket.localPosition;
Quaternion savedRotation=socket.localRotation;
// Inspector에서 Transform을 직접 수정하는 것과 같은 변경을 넣어 프레임과 애니메이션 전환을 지나도 유지되는지 확인한다.
Vector3 expectedPosition=savedPosition+new Vector3(0.004f,-0.002f,0.003f);
Quaternion expectedRotation=savedRotation*Quaternion.AngleAxis(5f,Vector3.forward);
socket.localPosition=expectedPosition;socket.localRotation=expectedRotation;
float maxPositionDrift=0f,maxRotationDrift=0f;
int samples=0,lastFrame=-1,activeAttack=0;
var finishedAttacks=new List<int>();
var observedAttacks=new HashSet<int>();
double began=EditorApplication.timeSinceStartup;
EditorApplication.CallbackFunction tick=null;
float RotationError(Quaternion first,Quaternion second)
{
    double dot=(double)first.x*second.x+(double)first.y*second.y+(double)first.z*second.z+(double)first.w*second.w;
    double aa=(double)first.x*first.x+(double)first.y*first.y+(double)first.z*first.z+(double)first.w*first.w;
    double bb=(double)second.x*second.x+(double)second.y*second.y+(double)second.z*second.z+(double)second.w*second.w;
    return (float)(2d*Math.Acos(Math.Min(1d,Math.Abs(dot)/Math.Sqrt(aa*bb)))*180d/Math.PI);
}
void Finish(bool success,string reason)
{
    EditorApplication.update-=tick;
    if(socket!=null){socket.localPosition=savedPosition;socket.localRotation=savedRotation;Selection.activeGameObject=socket.gameObject;}
    var result=new{success,reason,samples,maxPositionDrift,maxRotationDrift,finishedAttacks,elapsed=EditorApplication.timeSinceStartup-began,restoredPosition=savedPosition.ToString("F6"),restoredEuler=savedRotation.eulerAngles.ToString("F3"),fanAttachedToSocket=attachment!=null&&attachment.FanRoot.parent==socket};
    System.IO.File.WriteAllText("Assets/Temp/FanSocketDirectControl~/Test_RuntimeVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
}
tick=()=>
{
    try
    {
        if(!Application.isPlaying||socket==null){Finish(false,"Play session ended early");return;}
        if(EditorApplication.timeSinceStartup-began>25d){Finish(false,"Timed out");return;}
        if(lastFrame==Time.frameCount)return;
        lastFrame=Time.frameCount;samples++;
        maxPositionDrift=Mathf.Max(maxPositionDrift,Vector3.Distance(socket.localPosition,expectedPosition));
        maxRotationDrift=Mathf.Max(maxRotationDrift,RotationError(socket.localRotation,expectedRotation));
        if(maxPositionDrift>0.00001f||maxRotationDrift>0.001f){Finish(false,"Socket Transform was overwritten");return;}
        if(activeAttack>0&&control.SelectedState=="ATTACK"+activeAttack){if(control.PlaybackProgress>0.2f)observedAttacks.Add(activeAttack);return;}
        if(control.SelectedState!="DRAWN_IDLE"||control.CharacterAnimator.IsInTransition(0))return;
        if(activeAttack>0)
        {
            if(!observedAttacks.Contains(activeAttack)){Finish(false,"Attack did not advance");return;}
            finishedAttacks.Add(activeAttack);
            if(activeAttack==3){Finish(true,"Direct Transform edits survived drawing and all three attacks");return;}
        }
        activeAttack++;
        if(activeAttack==1)control.PlayAttack1();else if(activeAttack==2)control.PlayAttack2();else control.PlayAttack3();
    }
    catch(Exception exception){Finish(false,exception.ToString());}
};
control.EnterCombat();EditorApplication.update+=tick;
return new{started=true,position=expectedPosition.ToString("F6"),euler=expectedRotation.eulerAngles.ToString("F3")};
