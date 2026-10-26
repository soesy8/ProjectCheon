var ev=UnityEngine.EventSystems.EventSystem.current;
var data = new UnityEngine.EventSystems.PointerEventData(ev){position=new Vector2(294,820)};
var hits=new List<UnityEngine.EventSystems.RaycastResult>(); ev.RaycastAll(data,hits);
return new { focused=ev.isFocused, module=ev.currentInputModule == null ? "none" : ev.currentInputModule.GetType().Name, mouse=UnityEngine.InputSystem.Mouse.current == null ? "none" : UnityEngine.InputSystem.Mouse.current.position.ReadValue().ToString(), hits=hits.Select(h=>h.gameObject.name).ToArray(), devices=UnityEngine.InputSystem.InputSystem.devices.Select(d=>d.name).ToArray()};
