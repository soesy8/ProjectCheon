var session = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonSession>();
var player = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonPlayer>();
var boss = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonBoss>(UnityEngine.FindObjectsInactive.Include);
return new { frame = UnityEngine.Time.frameCount, time = UnityEngine.Time.time, scale = UnityEngine.Time.timeScale, chapter = session.CurrentChapter.ToString(), player = player.transform.position, health = player.Health, bossHealth = boss.Health, intent = boss.Intent, background = UnityEngine.Application.runInBackground, verification = UnityEngine.Object.FindObjectsByType<Test_CheonPlayVerification>(UnityEngine.FindObjectsSortMode.None).Length };
