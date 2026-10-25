var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/Proto/MountainPass/Scenes/MountainPass.unity" || EditorApplication.isPlaying) throw new System.InvalidOperationException("프로토타입 편집 씬을 확인하세요.");
var isometric = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_IsometricView>();
if (isometric != null) UnityEngine.Object.DestroyImmediate(isometric.gameObject);
var oldControl = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
var oldTimings = oldControl.SavedTimings;
var oldPlayback = oldControl.SavedPlaybackSettings;
var remap = new System.Collections.Generic.Dictionary<int,int>();
var timings = ScriptableObject.CreateInstance<Cheon.Prototype.MountainPass.PassActionTimings>();
JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(oldTimings), timings);
AssetDatabase.CreateAsset(timings, "Assets/Proto/MountainPass/Settings/MountainActionTimings.asset");
remap[oldTimings.GetInstanceID()] = timings.GetInstanceID();
var playback = ScriptableObject.CreateInstance<Cheon.Prototype.MountainPass.PassPlaybackSettings>();
JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(oldPlayback), playback);
AssetDatabase.CreateAsset(playback, "Assets/Proto/MountainPass/Settings/MountainPlayback.asset");
remap[oldPlayback.GetInstanceID()] = playback.GetInstanceID();
var fontPath = "Assets/Proto/MountainPass/Settings/MountainKorean.asset";
AssetDatabase.CopyAsset("Assets/Test/CheonMountainMap/Arts/Fonts/Test_MountainPlayerKorean.asset", fontPath);
var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(fontPath);
if (font.atlasWidth > 1024 || font.atlasHeight > 1024) throw new System.InvalidOperationException("폰트 아틀라스 해상도를 확인하세요."); font.isMultiAtlasTexturesEnabled = true;
var textPath = "Assets/Proto/MountainPass/Settings/MountainTextSettings.asset";
AssetDatabase.CopyAsset("Assets/Test/CheonMountainMap/Settings/Test_MountainTextSettings.asset", textPath);
var textSettings = AssetDatabase.LoadAssetAtPath<TMPro.TMP_Settings>(textPath);
var textSerialized = new SerializedObject(textSettings);
textSerialized.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
textSerialized.ApplyModifiedPropertiesWithoutUndo();
EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font); AssetDatabase.SaveAssetIfDirty(textSettings);
var oldTypes = new[] {typeof(Cheon.Testing.PlayerFan.Test_PlayerFanAttachment), typeof(Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController), typeof(Cheon.Testing.PlayerFan.Test_AttackRangeSector), typeof(Cheon.Testing.PlayerFan.Test_PlayerAttackRangeEffects), typeof(Cheon.Testing.MountainMap.Test_MountainPlayerMovement), typeof(Cheon.World.HeightFogVolume), typeof(Cheon.World.GateEnvironmentTransition)};
var newTypes = new[] {typeof(Cheon.Prototype.MountainPass.PassFanAttachment), typeof(Cheon.Prototype.MountainPass.PassAnimationController), typeof(Cheon.Prototype.MountainPass.PassAttackSector), typeof(Cheon.Prototype.MountainPass.PassAttackRangeEffects), typeof(Cheon.Prototype.MountainPass.PassPlayerMovement), typeof(Cheon.Prototype.MountainPass.PassHeightFog), typeof(Cheon.Prototype.MountainPass.PassWorldTransition)};
var conversions = new System.Collections.Generic.List<System.Tuple<MonoBehaviour,MonoBehaviour,string,bool>>();
for (int i=0; i<oldTypes.Length; i++) {
 var oldItems = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren(oldTypes[i],true)).Cast<MonoBehaviour>().ToArray();
 foreach (var item in oldItems) {
  var replacement = (MonoBehaviour)item.gameObject.AddComponent(newTypes[i]);
  remap[item.GetInstanceID()] = replacement.GetInstanceID();
  conversions.Add(System.Tuple.Create(item, replacement, JsonUtility.ToJson(item), item.enabled));
 }
}
foreach (var entry in conversions) {
 var json = System.Text.RegularExpressions.Regex.Replace(entry.Item3, "\"instanceID\":(-?\\d+)", match => {
  int identifier = int.Parse(match.Groups[1].Value); return "\"instanceID\":" + (remap.ContainsKey(identifier) ? remap[identifier] : identifier);
 });
 JsonUtility.FromJsonOverwrite(json, entry.Item2); entry.Item2.enabled = entry.Item4;
}
foreach (var entry in conversions.AsEnumerable().Reverse()) UnityEngine.Object.DestroyImmediate(entry.Item1);
foreach (var name in new[]{"Test_PlayerControlGuide", "Test_PlayerActionTimingUI", "Test_PlayerPlaybackUI"}) {
 var item=GameObject.Find(name); if(item!=null) UnityEngine.Object.DestroyImmediate(item);
}
var player = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassPlayerMovement>();
player.gameObject.name = "Player";
var control = player.AnimationControl;
var animator = control.CharacterAnimator;
string oldControllerPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
string controllerPath = "Assets/Proto/MountainPass/Settings/MountainPlayer.controller";
AssetDatabase.CopyAsset(oldControllerPath, controllerPath);
animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
var bossObject = scene.GetRootGameObjects().First(g=>g.name=="JangseungBoss");
var camera = Camera.main;
var oldCamera = camera.GetComponent<Cheon.Testing.MountainMap.Test_MountainPlayerCamera>();
if (oldCamera != null) UnityEngine.Object.DestroyImmediate(oldCamera);
camera.name="PlayerCamera"; camera.orthographic=false; camera.fieldOfView=60f;
var cameraControl = camera.gameObject.AddComponent<Cheon.Prototype.MountainPass.PassCamera>();
var systems = new GameObject("MountainPassSystems");
var flow = systems.AddComponent<Cheon.Prototype.MountainPass.PassGameFlow>();
var visuals = systems.AddComponent<Cheon.Prototype.MountainPass.PassEffects>();
var sounds = systems.AddComponent<Cheon.Prototype.MountainPass.PassAudio>();
var hud = systems.AddComponent<Cheon.Prototype.MountainPass.PassHud>();
var playerHealth = player.gameObject.AddComponent<Cheon.Prototype.MountainPass.PassHealth>();
playerHealth.Configure(100f,0.65f);
var bossHealth = bossObject.AddComponent<Cheon.Prototype.MountainPass.PassHealth>();
bossHealth.Configure(420f,0.04f);
var boss = bossObject.AddComponent<Cheon.Prototype.MountainPass.PassBossController>();
var combat = player.gameObject.AddComponent<Cheon.Prototype.MountainPass.PassPlayerCombat>();
var transition = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassWorldTransition>();
flow.Configure(player,playerHealth,boss,cameraControl,visuals,sounds,hud,transition);
cameraControl.Configure(player.transform,boss.transform,flow);
player.Configure(control,camera.transform);
boss.Configure(player,playerHealth,flow,visuals,cameraControl,sounds,bossObject.transform.Find("Visual"));
combat.Configure(control,boss,flow,visuals,cameraControl,sounds);
hud.Configure(flow,font,textSettings);
var fog = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassHeightFog>();
fog.transform.position = new Vector3(-28f,8f,-21f);
var fogProperties = new SerializedObject(fog);
fogProperties.FindProperty("density").floatValue=0.015f;
fogProperties.FindProperty("maxOpacity").floatValue=0.38f;
fogProperties.FindProperty("heightFade").floatValue=16f;
fogProperties.ApplyModifiedPropertiesWithoutUndo(); fog.ApplySettings();
RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared; RenderSettings.fogDensity=0.0035f;
RenderSettings.fogColor=new Color(0.45f,0.56f,0.59f);
RenderSettings.ambientSkyColor=new Color(0.6f,0.68f,0.7f);
RenderSettings.ambientEquatorColor=new Color(0.42f,0.48f,0.48f);
var sun = UnityEngine.Object.FindFirstObjectByType<Light>();
sun.color=new Color(1f,0.89f,0.72f); sun.intensity=1.65f;
sun.transform.rotation=Quaternion.Euler(42f,-32f,0f);
var glowShader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
if(glowShader==null) throw new System.InvalidOperationException("파티클 셰이더가 없습니다.");
var glow = new Material(glowShader) {name="M_SpiritRing"};
glow.SetFloat("_Surface",1f); glow.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha); glow.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
glow.SetFloat("_ZWrite",0f); glow.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glow.renderQueue=3000;
glow.SetColor("_BaseColor",Color.white);
AssetDatabase.CreateAsset(glow,"Assets/Proto/MountainPass/Materials/M_SpiritRing.mat");
visuals.Configure(glow);
var stone = new Material(Shader.Find("Universal Render Pipeline/Lit")) {name="M_EmberStone"};
stone.SetColor("_BaseColor",new Color(0.13f,0.19f,0.2f));
AssetDatabase.CreateAsset(stone,"Assets/Proto/MountainPass/Materials/M_EmberStone.mat");
var ember = new Material(Shader.Find("Universal Render Pipeline/Lit")) {name="M_JadeEmber"};
ember.SetColor("_BaseColor",new Color(0.2f,0.85f,0.65f)); ember.SetColor("_EmissionColor",new Color(0.3f,1.6f,1.1f)); ember.EnableKeyword("_EMISSION");
AssetDatabase.CreateAsset(ember,"Assets/Proto/MountainPass/Materials/M_JadeEmber.mat");
var warm = new Material(ember) {name="M_GoldenEmber"};
warm.SetColor("_BaseColor",new Color(0.95f,0.57f,0.16f)); warm.SetColor("_EmissionColor",new Color(2.3f,1.1f,0.2f));
AssetDatabase.CreateAsset(warm,"Assets/Proto/MountainPass/Materials/M_GoldenEmber.mat");
var decoration = new GameObject("SpiritLanterns");
System.Action<string,Vector3,bool> lantern = (name, position, shrine) => {
 var root = new GameObject(name); root.transform.SetParent(decoration.transform); root.transform.position=position;
 var pieces = new[]{PrimitiveType.Cylinder,PrimitiveType.Cube,PrimitiveType.Sphere,PrimitiveType.Cube};
 var positions = new[]{new Vector3(0f,0.25f,0f),new Vector3(0f,0.62f,0f),new Vector3(0f,1.15f,0f),new Vector3(0f,1.65f,0f)};
 var scales = new[]{new Vector3(0.75f,0.25f,0.75f),new Vector3(0.34f,0.5f,0.34f),new Vector3(0.4f,0.52f,0.4f),new Vector3(0.85f,0.16f,0.85f)};
 for(int i=0;i<pieces.Length;i++) {
  var part=GameObject.CreatePrimitive(pieces[i]); part.name=i==2?"Ember":"Stone";
  part.transform.SetParent(root.transform,false); part.transform.localPosition=positions[i]; part.transform.localScale=scales[i];
  UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
  part.GetComponent<Renderer>().sharedMaterial=i==2?(shrine?ember:warm):stone;
 }
 var lamp=new GameObject("LanternLight"); lamp.transform.SetParent(root.transform,false); lamp.transform.localPosition=Vector3.up*1.2f;
 var light=lamp.AddComponent<Light>(); light.type=LightType.Point;light.color=shrine?new Color(0.3f,1f,0.72f):new Color(1f,0.65f,0.25f);light.range=7f;light.intensity=2f;
};
lantern("FirstEmber",new Vector3(-20.4f,3.48f,99.2f),true);
lantern("ApproachEmber",new Vector3(4.6f,27.15f,38.9f),true);
lantern("ShrineCheckpoint",new Vector3(15.75f,27.12f,25.1f),true);
lantern("GateLeftEmber",new Vector3(-9f,37.13f,-21f),true);
for(int i=0;i<8;i++) {
 float angle=i*Mathf.PI/4f;
 lantern("GuardianLantern_"+(i+1),bossObject.transform.position+new Vector3(Mathf.Cos(angle)*24f,0f,Mathf.Sin(angle)*24f),false);
}
var boundary=visuals.CreateRing("GuardianArenaBoundary",new Color(0.9f,0.65f,0.28f,0.45f));
visuals.DrawRing(boundary,bossObject.transform.position+Vector3.up*0.1f,24f,new Color(0.9f,0.65f,0.28f,0.45f),0.07f);
var shrineRing=visuals.CreateRing("ShrineEmberCircle",new Color(0.3f,0.9f,0.72f,0.7f));
visuals.DrawRing(shrineRing,new Vector3(15.75f,27.22f,25.1f),2f,new Color(0.3f,0.9f,0.72f,0.7f),0.08f);
GameObject.Find("Test_CheonMountainMap").name="MountainPassEnvironment";
var fontChars="천 산문을지키는자 사당의잔불찾아라 부채펼쳐길열고잠든수장깨워 생명력 영약 회복 체크포인트 다시일어설시간 도전 여정계속하기 처음부터 멈춤 귀기파동 구르 통과하세요 공격틈 연격 천하대장군 격노한 붉은원에서벗나세요 수문장은잠들었습니 마지막잔불영약채우고도전 산문개방 걷히바람돌왔 명중 꺼진 숨르기 ◇ 0123456789/.:E F WASD Shift Space Tab Esc ENTER CHEON MOUNTAIN PASS A TALE BEYOND THE GATE";
font.TryAddCharacters(fontChars,out string missing);
EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);
foreach(var component in new UnityEngine.Object[]{flow,visuals,sounds,hud,player,playerHealth,boss,bossHealth,combat,cameraControl,control,fog,transition}) EditorUtility.SetDirty(component);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
Selection.activeGameObject=systems;
return new {scene=scene.path, converted=conversions.Count, missingCharacters=missing, bossScale=bossObject.transform.localScale.ToString(), bossCollider=new {height=bossObject.GetComponent<CapsuleCollider>().height,radius=bossObject.GetComponent<CapsuleCollider>().radius,center=bossObject.GetComponent<CapsuleCollider>().center.ToString()}, player=player.transform.position.ToString(), roots=scene.rootCount};


