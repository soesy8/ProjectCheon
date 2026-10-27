using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class Test_KeyProbe
{
    public static async Task<object> Main()
    {
        var original=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();var records=new List<object>();int start=Time.frameCount;
        Action sample=()=>{if(records.Count<24)records.Add(new{frame=Time.frameCount,update=InputState.currentUpdateType.ToString(),down=keyboard.eKey.isPressed,pressed=keyboard.eKey.wasPressedThisFrame,current=Keyboard.current.deviceId});};
        InputSystem.onAfterUpdate+=sample;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));
        await Task.Delay(250);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        await Task.Delay(100);
        InputSystem.onAfterUpdate-=sample;
        InputSystem.RemoveDevice(keyboard);if(original!=null)original.MakeCurrent();
        return new{start,end=Time.frameCount,records};
    }
}
