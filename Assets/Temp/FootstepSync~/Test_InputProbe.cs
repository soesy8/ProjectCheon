using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;
public static class Test_InputProbe
{
    public static async Task<object> Run()
    {
        var p=UnityEngine.Object.FindFirstObjectByType<PassPlayerMovement>();var a=p.AnimationControl;
        var f=p.GetComponent<Test_PlayerFoley>();var keyboard=Keyboard.current;var rows=new List<object>();
        try
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
            for(int i=0;i<8;i++)
            {
                await Task.Delay(120);
                rows.Add(new {keyboard=Keyboard.current.name,same=Keyboard.current==keyboard,pressed=keyboard.wKey.isPressed,
                    input=p.InputEnabled,enabled=p.enabled,moving=p.IsMoving,running=a.IsRunning,state=a.SelectedState,
                    phase=a.CharacterAnimator.GetFloat("LocomotionTime"),moveAmount=a.CharacterAnimator.GetFloat("MoveAmount"),
                    current=a.CharacterAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    steps=f.PlayedStepCount,frame=Time.frameCount});
            }
        }
        finally{InputSystem.QueueStateEvent(keyboard,new KeyboardState());}
        return rows;
    }
}
