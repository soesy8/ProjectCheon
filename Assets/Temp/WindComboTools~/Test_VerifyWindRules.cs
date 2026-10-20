using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Cheon.Combat;
using Object = UnityEngine.Object;

public static class Test_VerifyWindRules
{
    public static object Run()
    {
        var attack=Object.FindFirstObjectByType<WindComboAttack>();
        var input=attack.GetComponent<WindComboPlayer>();
        input.AutoDemonstration=false; input.enabled=false;
        Time.captureDeltaTime=1f/60;
        var result=new List<string>();
        int strikes=0;
        Action<int,float> record=(stage,time)=>strikes++;
        attack.Strike+=record;
        IEnumerator ToThird()
        {
            attack.CancelAttack();
            attack.RequestAttack();
            while(attack.Stage!=3)
            {
                if(attack.Elapsed>=.06f && !attack.HasBufferedInput) attack.RequestAttack();
                yield return null;
            }
        }
        void Check(string name,bool passed) { result.Add((passed ? "PASS ":"FAIL ")+name); }
        IEnumerator Sequence()
        {
            attack.CancelAttack();
            attack.RequestAttack();
            Check("buffer rejects input before +0.05",!attack.RequestAttack());
            while(attack.Elapsed<.06f) yield return null;
            Check("buffer accepts exactly one next hit",attack.RequestAttack() && !attack.RequestAttack());
            while(attack.IsAttacking) yield return null;
            Check("buffer does not queue a third hit",strikes==2);
            yield return new WaitForSeconds(.12f);
            attack.RequestAttack();
            Check("late continuation within 0.30 starts third",attack.Stage==3);
            attack.CancelAttack();
            yield return ToThird();
            while(attack.Elapsed<.20f) yield return null;
            int before=strikes;
            Check("third preparation can dodge cancel",attack.TryCancelForDodge());
            yield return new WaitForSeconds(.45f);
            Check("cancelled preparation never fires",strikes==before && Object.FindObjectsByType<WindComboProjectile>(FindObjectsSortMode.None).Length==0);
            yield return ToThird();
            while(attack.Elapsed<.44f) yield return null;
            Check("release commitment blocks dodge",!attack.TryCancelForDodge());
            while(attack.Elapsed<.69f) yield return null;
            int projectiles=Object.FindObjectsByType<WindComboProjectile>(FindObjectsSortMode.None).Length;
            Check("late recovery can dodge cancel",attack.TryCancelForDodge());
            Check("late cancel preserves released projectile",Object.FindObjectsByType<WindComboProjectile>(FindObjectsSortMode.None).Length==projectiles && projectiles==1);
            yield return new WaitForSeconds(.2f);
            Check("projectile expires",Object.FindObjectsByType<WindComboProjectile>(FindObjectsSortMode.None).Length==0);
            attack.RequestAttack();
            while(attack.IsAttacking) yield return null;
            yield return new WaitForSeconds(.32f);
            attack.RequestAttack();
            Check("expired continuation restarts first",attack.Stage==1);
            attack.CancelAttack();
            if(Keyboard.current!=null)
            {
                input.enabled=true;
                int oldStrikes=strikes;
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.J));
                yield return new WaitForSeconds(.75f);
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
                Check("holding J performs only one attack",strikes==oldStrikes+1 && attack.Stage==0);
                input.enabled=false;
            }
            attack.Strike-=record;
            input.enabled=true;
            Time.captureDeltaTime=0;
            File.WriteAllLines("Assets/Temp/WindComboPreviews/RulesVerification.txt",result);
        }
        attack.StartCoroutine(Sequence());
        return "Rule verification coroutine started.";
    }
}
