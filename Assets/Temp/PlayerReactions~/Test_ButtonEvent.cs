var p=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
var button=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(b=>b.name=="LethalAttack");
var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
if(p.Mode!=Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewMode.Death) throw new Exception("버튼 연결 오류");
return new {button=button.name,mode=p.Mode.ToString()};
