using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;
using Cheon.Tests.PlayerReactions;
using Cheon.Tests.BossPatterns;
using Object = UnityEngine.Object;

public static class ApplyIntegration
{
    private static readonly Dictionary<Object, Object> replacements = new Dictionary<Object, Object>();
    private static readonly Dictionary<string, string> copiedPaths = new Dictionary<string, string>();
    private static readonly List<string> created = new List<string>();

    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit mode required");
        var main = Load("Assets/Scenes/MountainPass/MountainPass.unity");
        var vfxScene = Load("Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity");
        var reactionScene = Load("Assets/Test/PlayerReactions/Scenes/Test_PlayerReactions.unity");
        var bossScene = Load("Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity");
        if (main.isDirty) throw new Exception("Main scene has unsaved changes");
        const string backup = "Assets/Temp/MainSceneIntegration~/MountainPass.before.unity";
        if (!File.Exists(backup)) File.Copy(main.path, backup);
        SceneManager.SetActiveScene(main);
        var animation = One<PassAnimationController>(main);
        var movement = One<PassPlayerMovement>(main);
        var game = One<PassGameFlow>(main);
        var ranges = One<PassAttackRangeEffects>(main);
        if (All<PassFanAttackVfx>(main).Any()) throw new Exception("VFX already applied");

        // 실행용 프로필은 타입과 에셋 참조를 함께 바꿔 테스트 폴더에 의존하지 않는다.
        var sourceVfx = One<Test_FanAttackVfx>(vfxScene);
        var profile = ScriptableObject.CreateInstance<PassFanAttackVfxProfile>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sourceVfx.Profile), profile);
        profile.name = "MountainFanAttackVfx";
        EnsureFolder("Assets/Settings/MountainPass");
        AssetDatabase.CreateAsset(profile, "Assets/Settings/MountainPass/MountainFanAttackVfx.asset");
        replacements[sourceVfx.Profile] = profile;
        Rewrite(profile);
        var sourceRoll = One<Test_RollDust>(vfxScene);
        var rollProfile = ScriptableObject.CreateInstance<PassRollDustProfile>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sourceRoll.Profile), rollProfile);
        rollProfile.name = "MountainRollDust";
        AssetDatabase.CreateAsset(rollProfile, "Assets/Settings/MountainPass/MountainRollDust.asset");
        replacements[sourceRoll.Profile] = rollProfile;
        Rewrite(rollProfile);

        var sourceAnimation = One<PassAnimationController>(vfxScene);
        replacements[sourceAnimation] = animation;
        replacements[One<PassPlayerMovement>(vfxScene).transform] = movement.transform;
        replacements[One<PassAttackRangeEffects>(vfxScene)] = ranges;
        var vfxRoot = PromoteVfx(sourceVfx.gameObject, main);
        var rollRoot = PromoteVfx(sourceRoll.gameObject, main);
        var vfx = vfxRoot.GetComponent<PassFanAttackVfx>();
        vfxRoot.transform.SetPositionAndRotation(movement.transform.position, movement.transform.rotation);
        rollRoot.transform.SetPositionAndRotation(movement.transform.position, movement.transform.rotation);
        ranges.enabled = false;
        EnsureFolder("Assets/Prefabs/MountainPass");
        PrefabUtility.SaveAsPrefabAssetAndConnect(vfxRoot, "Assets/Prefabs/MountainPass/FanAttackVfx.prefab", InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAssetAndConnect(rollRoot, "Assets/Prefabs/MountainPass/RollDust.prefab", InteractionMode.AutomatedAction);

        var damageObject = new GameObject("FanAttackDamageSettings");
        SceneManager.MoveGameObjectToScene(damageObject, main);
        var damage = damageObject.AddComponent<PassFanAttackDamageSettings>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(One<Test_FanAttackDamageSettings>(vfxScene)), damage);
        var oldCombat = movement.GetComponent<PassPlayerCombat>();
        var combat = movement.gameObject.AddComponent<PassFanAttackCombat>();
        EditorUtility.CopySerializedManagedFieldsOnly(oldCombat, combat);
        combat.enabled = true;
        combat.ConfigureVfxCombat(profile, damage, vfx);
        replacements[oldCombat] = combat;
        foreach (var component in All<MonoBehaviour>(main)) if (component != null && component != oldCombat) Rewrite(component);
        Object.DestroyImmediate(oldCombat);

        // 피격·기상·사망은 이미 통합된 메인 전용 모션과 보스 강피격 연결을 유지한다.
        var sourceBoss = One<Test_BossPatternSettings>(bossScene);
        var boss = One<PassBossPatternSettings>(main);
        var dust = boss.dust.material; var crack = boss.crack.material;
        var death = boss.death.material; var powder = boss.death.powderMaterial;
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sourceBoss), boss);
        boss.dust.material = dust; boss.crack.material = crack;
        boss.death.material = death; boss.death.powderMaterial = powder;
        EditorUtility.SetDirty(boss);

        return Finish();
    }

    public static object Finish()
    {
        var main = Load("Assets/Scenes/MountainPass/MountainPass.unity");
        var vfxScene = Load("Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity");
        var reactionScene = Load("Assets/Test/PlayerReactions/Scenes/Test_PlayerReactions.unity");
        var bossScene = Load("Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity");
        var animation = One<PassAnimationController>(main);
        var movement = One<PassPlayerMovement>(main);
        var game = One<PassGameFlow>(main);
        var combat = One<PassFanAttackCombat>(main);
        var ranges = One<PassAttackRangeEffects>(main);
        var damage = One<PassFanAttackDamageSettings>(main);
        var boss = One<PassBossPatternSettings>(main);
        var vfx = One<PassFanAttackVfx>(main);
        var sourceDrink = One<Test_GourdDrinking>(reactionScene);
        var sourceAttachment = One<Test_GourdAttachment>(reactionScene);
        var animator = animation.CharacterAnimator;
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        var sourceController = (AnimatorController)new SerializedObject(One<Test_PlayerReactionPreview>(reactionScene)).FindProperty("reactionController").objectReferenceValue;
        AddDrinkingLayer(controller, sourceController);
        var belt = Socket(animator.GetBoneTransform(HumanBodyBones.Hips), "GourdBeltSocket", sourceAttachment.BeltSocket);
        var grip = Socket(animator.GetBoneTransform(HumanBodyBones.LeftHand), "LeftGourdGrip", sourceAttachment.HandSocket);
        var gourd = Object.Instantiate(sourceAttachment.Gourd.gameObject, belt, false);
        if (PrefabUtility.IsPartOfPrefabInstance(gourd)) PrefabUtility.UnpackPrefabInstance(gourd, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        gourd.transform.localPosition = Vector3.zero; gourd.transform.localRotation = Quaternion.identity;
        foreach (var child in gourd.GetComponentsInChildren<Transform>(true)) child.name = child.name.Replace("Test_", "");
        foreach (var component in gourd.GetComponentsInChildren<Component>(true)) if (component != null) Rewrite(component);
        PrefabUtility.SaveAsPrefabAssetAndConnect(gourd, "Assets/Prefabs/MountainPass/Gourd.prefab", InteractionMode.AutomatedAction);
        var drinking = animation.gameObject.AddComponent<PassGourdDrinking>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sourceDrink), drinking);
        var attachment = animation.gameObject.AddComponent<PassGourdAttachment>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sourceAttachment), attachment);
        var drinkSerialized = new SerializedObject(sourceDrink);
        drinking.Configure(animation, movement, game.PlayerHealth, game, attachment,
            (AnimationClip)CopyArt(drinkSerialized.FindProperty("drawClip").objectReferenceValue),
            (AnimationClip)CopyArt(drinkSerialized.FindProperty("drinkingClip").objectReferenceValue));
        attachment.Configure(animator, drinking, gourd.transform, belt, grip, gourd.transform.Find("Cork").gameObject);
        var pose = animation.gameObject.AddComponent<PassGourdDrinkingPose>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(One<Test_GourdDrinkingPose>(reactionScene)), pose);
        pose.Configure(animation, drinking);
        game.ConfigureGourd(drinking);
        foreach (var component in new Component[] { drinking, attachment, pose, animation, game, combat, ranges, damage }) EditorUtility.SetDirty(component);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(main);
        if (!EditorSceneManager.SaveScene(main)) throw new Exception("Saving main failed");
        foreach (var scene in new[] { vfxScene, reactionScene, bossScene })
            if (!scene.isDirty) EditorSceneManager.CloseScene(scene, true);
        SceneManager.SetActiveScene(main);
        Selection.activeGameObject = animation.gameObject;
        return new { scene = main.path, player = movement.name, vfx = vfx.name, combat = combat.GetType().Name,
            gourd = drinking.name, maximumUses = drinking.MaximumUses, recovery = drinking.HealAmount,
            bossPatterns = new { boss.crack.enabled, linked = boss.linkedAttack.enabled, boss.summon.spawnCount }, created };
    }

    private static GameObject PromoteVfx(GameObject source, Scene scene)
    {
        var clone = Object.Instantiate(source);
        SceneManager.MoveGameObjectToScene(clone, scene);
        if (PrefabUtility.IsPartOfPrefabInstance(clone)) PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        clone.name = source.name.Replace("Test_", "");
        var oldComponents = clone.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c!=null && c.GetType().Namespace=="Cheon.Tests.FanAttackVfx").ToArray();
        foreach (var old in oldComponents)
        {
            string typeName = "Cheon.Prototype.MountainPass.Pass" + old.GetType().Name.Substring(5);
            var type = typeof(PassFanAttackVfx).Assembly.GetType(typeName, true);
            var replacement = clone == old.gameObject ? clone.AddComponent(type) : old.gameObject.AddComponent(type);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(old), replacement);
            replacements[old] = replacement;
        }
        foreach (var component in clone.GetComponentsInChildren<Component>(true)) if (component!=null && !oldComponents.Contains(component)) Rewrite(component);
        foreach (var old in oldComponents) Object.DestroyImmediate(old);
        foreach (var child in clone.GetComponentsInChildren<Transform>(true)) child.name = child.name.Replace("Test_", "");
        return clone;
    }

    private static void AddDrinkingLayer(AnimatorController target, AnimatorController source)
    {
        if (target.layers.Any(l=>l.name=="GourdUpperBody")) throw new Exception("Drinking layer already exists");
        var sourceLayer = source.layers.Single(l=>l.name=="GourdUpperBody");
        foreach (var parameter in source.parameters.Where(p=>p.name.StartsWith("Gourd")))
            if (!target.parameters.Any(p=>p.name==parameter.name)) target.AddParameter(parameter.name, parameter.type);
        var machine = new AnimatorStateMachine { name = "GourdUpperBody" };
        AssetDatabase.AddObjectToAsset(machine, target);
        foreach (var child in sourceLayer.stateMachine.states)
        {
            var state = machine.AddState(child.state.name, child.position);
            state.motion = child.state.motion != null ? (Motion)CopyArt(child.state.motion) : null;
            state.mirror = child.state.mirror; state.iKOnFeet = false; state.writeDefaultValues = false;
            state.timeParameter = child.state.timeParameter; state.timeParameterActive = child.state.timeParameterActive;
            if (child.state == sourceLayer.stateMachine.defaultState) machine.defaultState = state;
            EditorUtility.SetDirty(state);
        }
        target.AddLayer(new AnimatorControllerLayer { name="GourdUpperBody", stateMachine=machine,
            avatarMask=(AvatarMask)CopyArt(sourceLayer.avatarMask), blendingMode=sourceLayer.blendingMode, defaultWeight=0f, iKPass=false });
        EditorUtility.SetDirty(machine); EditorUtility.SetDirty(target);
    }

    private static void Rewrite(Object target)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;
            var value = property.objectReferenceValue;
            if (value == null) continue;
            Object replacement;
            if (!replacements.TryGetValue(value, out replacement)) replacement = CopyArt(value);
            if (replacement != value) property.objectReferenceValue = replacement;
        }
        if (serialized.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(target);
    }

    private static Object CopyArt(Object source)
    {
        if (source == null) return null;
        if (replacements.TryGetValue(source, out var cached)) return cached;
        string path = AssetDatabase.GetAssetPath(source);
        if (!path.StartsWith("Assets/Test/")) return source;
        if (source is MonoScript) throw new Exception("Unexpected test script dependency: " + path);
        string destination;
        if (!copiedPaths.TryGetValue(path, out destination))
        {
            string name = Path.GetFileName(path).Replace("Test_", "");
            string category = source is Shader ? "Shaders" : source is Material ? "Materials" : source is Texture ? "Textures" : source is Mesh ? "Meshes" : "Animations";
            string root = path.Contains("/Drinking/") ? (category=="Animations" ? "Assets/Arts/Characters/Player/Animations/MountainMap" : "Assets/Arts/Props/Gourd/"+category) : "Assets/Arts/Effects/FanAttack/" + category;
            destination = root + "/" + name;
            EnsureFolder(root);
            if (File.Exists(destination)) throw new Exception("Unexpected existing destination: " + destination);
            if (!AssetDatabase.CopyAsset(path, destination)) throw new Exception("Copy failed: " + path);
            copiedPaths[path] = destination; created.Add(destination);
            if (source is Shader)
            {
                File.WriteAllText(destination, File.ReadAllText(destination).Replace("Cheon/Tests/", "Cheon/Effects/"), new System.Text.UTF8Encoding(false));
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            }
        }
        Object copy;
        if (AssetDatabase.IsSubAsset(source))
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
            copy = AssetDatabase.LoadAllAssetsAtPath(destination).First(a=>{
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string g, out long localId); return id==localId;
            });
        }
        else copy = AssetDatabase.LoadMainAssetAtPath(destination);
        if (copy == null) throw new Exception("Copy load failed: " + destination);
        replacements[source] = copy;
        if (source is Material || source is AvatarMask || source is Mesh) Rewrite(copy);
        if (source is Texture2D texture)
        {
            if (texture.width > 1024 || texture.height > 1024) throw new Exception("Source texture exceeds 1024: " + path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
            importer.maxTextureSize = 1024;
            foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone", "Android", "iPhone", "WebGL" })
            { var settings=importer.GetPlatformTextureSettings(platform); settings.maxTextureSize=1024; importer.SetPlatformTextureSettings(settings); }
            importer.SaveAndReimport();
        }
        return copy;
    }
    private static Transform Socket(Transform parent, string name, Transform source)
    {
        var socket = new GameObject(name).transform; socket.SetParent(parent, false);
        socket.localPosition=source.localPosition; socket.localRotation=source.localRotation; socket.localScale=source.localScale;
        return socket;
    }
    private static Scene Load(string path)
    { var scene=SceneManager.GetSceneByPath(path); return scene.IsValid() && scene.isLoaded ? scene : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    private static T One<T>(Scene scene) where T : Component => All<T>(scene).Single();
    private static void EnsureFolder(string path)
    { if(AssetDatabase.IsValidFolder(path))return; EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path)); }
}
