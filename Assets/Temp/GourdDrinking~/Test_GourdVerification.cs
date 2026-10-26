using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Cheon.Tests.PlayerReactions;

public static class Test_GourdVerification
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Folder = "Assets/Temp/GourdDrinking~/";

    public static string QueueDrink()
    {
        var gameView = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
        gameView.Focus();
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,
            new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.G));
        return "G queued for the next real input frame";
    }
    public static string QueueMove()
    {
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,
            new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
        return "W queued";
    }
    public static string ReleaseKeys()
    {
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,
            new UnityEngine.InputSystem.LowLevel.KeyboardState());
        return "Keys released";
    }
    public static string MovementInfo()
    {
        var p = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        var d=p.GetComponent<Test_GourdDrinking>();
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { basePhase=p.Phase.ToString(),
            actorPosition=p.CharacterAnimator.transform.position.ToString("F3"),
            moveAmount=p.AnimationController.GetFloat("MoveAmount"), gourdPhase=d.Phase.ToString(),
            source=p.AnimationController.GetCurrentAnimatorClipInfo(1).Select(c=>UnityEditor.AssetDatabase.GetAssetPath(c.clip)).ToArray(),
            excludedBoneError=ExcludedBoneError(p) });
    }

    public static string CheckBehaviors()
    {
        var p=UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        var d=p.GetComponent<Test_GourdDrinking>(); var a=p.GetComponent<Test_GourdAttachment>();
        var input=p.GetComponent<Test_PlayerReactionInput>(); input.enabled=false;
        try
        {
            p.ResetPreview(); d.PlayDrinking(); p.SetInput(new Vector2(0,.25f),false);
            for(int i=0;i<120;i++) Tick(p,a,1f/60f);
            float distance=p.CharacterAnimator.transform.position.magnitude;
            bool moving=p.Phase==Test_PlayerReactionPreview.PreviewPhase.Moving && p.AnimationController.GetFloat("MoveAmount")>.99f;
            bool drinking=d.Phase==Test_GourdDrinking.DrinkingPhase.Drinking;
            float excludedError=ExcludedBoneError(p);
            typeof(Test_GourdAttachment).GetMethod("LateUpdate",Private).Invoke(a,null);
            float contactDistance=a.MouthDistance;
            var movingPicture=Capture(p.CharacterAnimator.transform.position,768,768,90);
            File.WriteAllBytes(Folder+"Test_MovingDrinkSide.png",movingPicture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(movingPicture);
            p.SetPaused(true); float progress=d.Progress; Vector3 local=a.Gourd.localPosition;
            Tick(p,a,.5f);
            bool paused=Mathf.Abs(progress-d.Progress)<.00001f && Vector3.Distance(local,a.Gourd.localPosition)<.00001f;
            p.ResetPreview(); d.PlayDrinking(); for(int i=0;i<70;i++) Tick(p,a,1f/60f);
            p.Play(Test_PlayerReactionPreview.PreviewMode.HeavyThenGetUp); Tick(p,a,1f/60f);
            bool hitCancels=!d.IsPlaying && !a.IsInHand && p.AnimationController.GetLayerWeight(1)==0;
            p.ResetPreview(); d.PlayDrinking(); for(int i=0;i<45;i++) Tick(p,a,1f/60f);
            p.SetInput(Vector2.zero,true); Tick(p,a,1f/60f); Tick(p,a,1f/60f);
            bool dodgeCancels=p.Phase==Test_PlayerReactionPreview.PreviewPhase.Dodging && !d.IsPlaying && !a.IsInHand;
            p.ResetPreview(); d.PlayDrinking(); for(int i=0;i<330;i++) Tick(p,a,1f/60f);
            bool returns=!d.IsPlaying && !a.IsInHand && a.Gourd.parent==a.BeltSocket && p.AnimationController.GetLayerWeight(1)==0;
            p.ResetPreview();
            var back=Capture(p.CharacterAnimator.transform.position,768,768,0);
            File.WriteAllBytes(Folder+"Test_HolsteredBack.png",back.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(back);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { moving,drinking,distance,excludedError,contactDistance,paused,hitCancels,dodgeCancels,returns });
        }
        finally { p.SetInput(Vector2.zero,false); input.enabled=true; }
    }
    private static void Tick(Test_PlayerReactionPreview p,Test_GourdAttachment a,float seconds)
    { p.AdvancePlayback(seconds); typeof(Test_GourdAttachment).GetMethod("LateUpdate",Private).Invoke(a,null); }

    public static string StartLive()
    {
        var p = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        p.ResetPreview(); p.GetComponent<Test_GourdDrinking>().PlayDrinking();
        return "Live drinking started";
    }

    public static string LiveInfo()
    {
        var p = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        var a = p.GetComponent<Test_GourdAttachment>(); var d = p.GetComponent<Test_GourdDrinking>();
        var hand = p.CharacterAnimator.GetBoneTransform(HumanBodyBones.LeftHand); var head = p.CharacterAnimator.GetBoneTransform(HumanBodyBones.Head);
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { phase=d.Phase.ToString(), d.Progress, d.SipWeight,
            a.MouthDistance, hand = hand.position.ToString("F3"), head = head.position.ToString("F3"),
            gourd = a.Gourd.position.ToString("F3"), gourdUp = a.Gourd.up.ToString("F3"), parent=a.Gourd.parent.name,
            gripLocalRotation = a.HandSocket.localEulerAngles.ToString("F2"),
            handRotation = hand.eulerAngles.ToString("F2"),
            mouthInHand=hand.InverseTransformPoint(head.TransformPoint(new Vector3(0,.035f,.095f))).ToString("F3"),
            state=p.AnimationController.GetCurrentAnimatorStateInfo(1).fullPathHash,
            time=p.AnimationController.GetCurrentAnimatorStateInfo(1).normalizedTime,
            drinkTime=p.AnimationController.GetFloat("GourdDrinkTime"),
            expectedDraw=Animator.StringToHash("GourdUpperBody.GOURD_DRAW"),
            expectedDrink=Animator.StringToHash("GourdUpperBody.GOURD_DRINK"),
            transition=p.AnimationController.IsInTransition(1),
            clips=p.AnimationController.GetCurrentAnimatorClipInfo(1).Select(c=>UnityEditor.AssetDatabase.GetAssetPath(c.clip)).ToArray() });
    }

    public static string FlipGrip()
    {
        var p = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        p.GetComponent<Test_GourdAttachment>().HandSocket.localRotation=Quaternion.Euler(0,0,-90);
        return LiveInfo();
    }

    public static string PauseAtSip()
    {
        var preview = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        preview.ResetPreview(); preview.GetComponent<Test_GourdDrinking>().PlayDrinking();
        for (int step = 0; step < 138; step++) preview.AdvancePlayback(1f/60f);
        preview.SetPaused(true);
        return preview.GetComponent<Test_GourdDrinking>().Status;
    }

    public static string CaptureSequence()
    {
        var preview = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        if (preview == null) throw new InvalidOperationException("피격·기상 테스트 씬이 열려 있지 않습니다.");
        var motion = preview.GetComponent<Test_GourdDrinking>();
        var attachment = preview.GetComponent<Test_GourdAttachment>();
        var animator = preview.CharacterAnimator;
        preview.ResetPreview();
        motion.PlayDrinking();
        var poses = new List<object>();
        var sheet = new Texture2D(960, 960, TextureFormat.RGB24, false);
        float elapsed = 0f;
        float[] times = { 0f, .55f, .8f, 1.35f, 2.3f, 3.4f, 4.2f, 4.65f, 5.1f };
        float maxExcludedError = 0f;
        for (int index = 0; index < times.Length; index++)
        {
            while (elapsed + .0001f < times[index])
            {
                float seconds = Mathf.Min(1f / 60f, times[index] - elapsed);
                preview.AdvancePlayback(seconds);
                typeof(Test_GourdAttachment).GetMethod("LateUpdate", Private).Invoke(attachment, null);
                elapsed += seconds;
            }
            maxExcludedError = Mathf.Max(maxExcludedError, ExcludedBoneError(preview));
            typeof(Test_GourdAttachment).GetMethod("LateUpdate", Private).Invoke(attachment, null);
            poses.Add(new { time = times[index], phase = motion.Phase.ToString(), progress = motion.Progress,
                inHand = attachment.IsInHand, distance = attachment.MouthDistance,
                hand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position.ToString("F3"),
                mouthInHand = animator.GetBoneTransform(HumanBodyBones.LeftHand).InverseTransformPoint(
                    animator.GetBoneTransform(HumanBodyBones.Head).TransformPoint(new Vector3(0,.035f,.095f))).ToString("F3"),
                handRotation = animator.GetBoneTransform(HumanBodyBones.LeftHand).rotation.eulerAngles.ToString("F2"),
                bottleUp = attachment.Gourd.up.ToString("F3") });
            var picture = Capture(animator.transform.position, 320, 320, 155f);
            sheet.SetPixels((index % 3) * 320, (2 - index / 3) * 320, 320, 320, picture.GetPixels());
            UnityEngine.Object.DestroyImmediate(picture);
            if (index == 4)
            {
                var close = Capture(animator.transform.position, 768, 768, 155f);
                File.WriteAllBytes(Folder + "Test_DrinkingClose.png", close.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(close);
            }
        }
        sheet.Apply(); File.WriteAllBytes(Folder + "Test_GourdSequence.png", sheet.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(sheet);
        preview.SetPaused(true);
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { maxExcludedBoneError = maxExcludedError, poses });
    }

    private static float ExcludedBoneError(Test_PlayerReactionPreview preview)
    {
        var controller = preview.AnimationController;
        int layer = controller.GetLayerCount() - 1;
        float weight = controller.GetLayerWeight(layer);
        if (weight <= 0) return 0f;
        var bones = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand };
        var animator = preview.CharacterAnimator;
        var graph = (PlayableGraph)typeof(Test_PlayerReactionPreview).GetField("graph", Private).GetValue(preview);
        graph.Evaluate(0f);
        var rotations = bones.Select(b => animator.GetBoneTransform(b).localRotation).ToArray();
        controller.SetLayerWeight(layer, 0f); graph.Evaluate(0f);
        float error = bones.Select((bone, index) => Quaternion.Angle(rotations[index], animator.GetBoneTransform(bone).localRotation)).Max();
        controller.SetLayerWeight(layer, weight); graph.Evaluate(0f);
        return error;
    }

    private static Texture2D Capture(Vector3 actor, int width, int height, float yaw)
    {
        var bakedObjects = new List<GameObject>();
        var hiddenRenderers = new List<SkinnedMeshRenderer>();
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            var mesh = new Mesh(); renderer.BakeMesh(mesh);
            var item = new GameObject("Test_BakedPose", typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(renderer.transform, false); item.GetComponent<MeshFilter>().sharedMesh=mesh;
            item.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            renderer.enabled=false; hiddenRenderers.Add(renderer); bakedObjects.Add(item);
        }
        var cameraObject = new GameObject("Test_GourdCaptureCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f,.11f,.14f);
        camera.fieldOfView = 35f;
        camera.transform.position = actor + Quaternion.Euler(0,yaw,0) * new Vector3(0,1.65f,-3.4f);
        camera.transform.LookAt(actor + Vector3.up * .95f);
        var target = new RenderTexture(width,height,24);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var result = new Texture2D(width,height,TextureFormat.RGB24,false);
            result.ReadPixels(new Rect(0,0,width,height),0,0); result.Apply(); return result;
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject);
            foreach (var item in bakedObjects) { UnityEngine.Object.DestroyImmediate(item.GetComponent<MeshFilter>().sharedMesh); UnityEngine.Object.DestroyImmediate(item); }
            foreach (var renderer in hiddenRenderers) renderer.enabled=true;
        }
    }
}
