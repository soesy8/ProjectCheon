var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(EditorApplication.isPlaying || !scene.path.StartsWith("Assets/Proto/MountainPass/"))throw new System.InvalidOperationException("프로토타입 편집 씬을 확인하세요.");
var camera=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassCamera>();
var cameraSettings=new SerializedObject(camera);cameraSettings.FindProperty("combatDistance").floatValue=17f;cameraSettings.ApplyModifiedPropertiesWithoutUndo();
var transition=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassWorldTransition>();
var environment=new SerializedObject(transition);
environment.FindProperty("spiritSun").colorValue=new Color(0.86f,0.87f,1f);
environment.FindProperty("spiritSunIntensity").floatValue=1.15f;
environment.FindProperty("spiritAmbient").colorValue=new Color(0.5f,0.52f,0.65f);
environment.ApplyModifiedPropertiesWithoutUndo();
var boss=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassBossController>();
var effects=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassEffects>();
var boundary=GameObject.Find("GuardianArenaBoundary").GetComponent<LineRenderer>();
effects.DrawRing(boundary,boss.transform.position+Vector3.up*0.1f,28f,new Color(0.9f,0.65f,0.28f,0.45f),0.07f);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new{saved=scene.path,missingScripts=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)), scripts=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c!=null).Select(c=>c.GetType().FullName).Distinct().ToArray()};

