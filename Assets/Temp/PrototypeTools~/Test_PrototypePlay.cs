if (!UnityEditor.EditorApplication.isPlaying)
    throw new System.InvalidOperationException("Play Mode is required.");

var player = GameObject.Find("Player");
var controller = player.GetComponent<CharacterController>();
var movement = player.GetComponent<PlayerMovement>();
var boss = GameObject.Find("BossDummy").GetComponent<BossDummy>();
var gate = GameObject.Find("WorldGate").GetComponent<WorldGate>();
var checks = new System.Collections.Generic.List<object>();
var originalInputSettings = UnityEngine.InputSystem.InputSystem.settings;
var validationInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
validationInputSettings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
validationInputSettings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
UnityEngine.InputSystem.InputSystem.settings = validationInputSettings;
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("PrototypeValidation");
keyboard.MakeCurrent();
int stage = 0;
float stageStartedAt = Time.time;
Vector3 dodgeStart = Vector3.zero;
Vector3 bossStart = boss.transform.position;
int initialHitCount = boss.HitCount;
int initialHealth = boss.CurrentHealth;
bool wasDodging = false;
UnityEditor.EditorApplication.CallbackFunction tick = null;
string reportPath = "Assets/Temp/Test_PrototypePlayResults.json";

void SetKeys(params UnityEngine.InputSystem.Key[] keys)
{
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));
}

void PlacePlayer(Vector3 position, Vector3 direction)
{
    controller.enabled = false;
    player.transform.position = position;
    player.transform.forward = direction;
    controller.enabled = true;
    Physics.SyncTransforms();
}

void Check(string name, bool passed, object details)
{
    checks.Add(new { name, passed, details });
    if (!passed)
        throw new System.Exception("Validation failed: " + name);
}

void NextStage(int nextStage)
{
    stage = nextStage;
    stageStartedAt = Time.time;
}

void Finish(string error)
{
    UnityEditor.EditorApplication.update -= tick;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
    UnityEngine.InputSystem.InputSystem.settings = originalInputSettings;
    UnityEngine.Object.Destroy(validationInputSettings);
    PlacePlayer(new Vector3(-10f, 1.05f, 0f), Vector3.right);
    var report = new { success = error == null, error, checks };
    System.IO.File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
}

tick = () =>
{
    try
    {
        if (!UnityEditor.EditorApplication.isPlaying)
            throw new System.Exception("Play Mode ended during validation.");
        float elapsed = Time.time - stageStartedAt;
        switch (stage)
        {
            case 0:
                var realityBounds = GameObject.Find("Ground").GetComponent<Renderer>().bounds;
                var spiritBounds = GameObject.Find("SpiritGround").GetComponent<Renderer>().bounds;
                realityBounds.Encapsulate(spiritBounds);
                Check("Map aspect ratio 2:1", Mathf.Approximately(realityBounds.size.x / realityBounds.size.z, 2f), realityBounds.size.ToString());
                Check("Boss capsule scale 3x", boss.transform.localScale == player.transform.localScale * 3f, boss.transform.localScale.ToString());
                Check("Exactly two gate pillars", gate.GetComponentsInChildren<MeshRenderer>().Length == 2, gate.transform.childCount);
                PlacePlayer(new Vector3(-3f, 1.05f, 0.6f), Vector3.right);
                SetKeys(UnityEngine.InputSystem.Key.D, UnityEngine.InputSystem.Key.S);
                NextStage(1);
                break;
            case 1:
                if (player.transform.position.x < 1f && elapsed < 3f) break;
                SetKeys();
                NextStage(2);
                break;
            case 2:
                if (elapsed < 0.15f) break;
                Check("Walk through gate to spirit world", gate.IsSpiritWorld && player.transform.position.x > 0f, player.transform.position.ToString());
                SetKeys(UnityEngine.InputSystem.Key.A, UnityEngine.InputSystem.Key.W);
                NextStage(3);
                break;
            case 3:
                if (player.transform.position.x > -1f && elapsed < 3f) break;
                SetKeys();
                NextStage(4);
                break;
            case 4:
                if (elapsed < 0.15f) break;
                Check("Walk back to reality", !gate.IsSpiritWorld && player.transform.position.x < 0f, player.transform.position.ToString());
                PlacePlayer(new Vector3(-2f, 1.05f, 6f), Vector3.right);
                SetKeys(UnityEngine.InputSystem.Key.D, UnityEngine.InputSystem.Key.S);
                NextStage(5);
                break;
            case 5:
                if (elapsed < 0.8f) break;
                SetKeys();
                Check("Cannot bypass gate at side boundary", player.transform.position.x < -0.8f && !gate.IsSpiritWorld, player.transform.position.ToString());
                PlacePlayer(new Vector3(19f, 1.05f, -8f), Vector3.right);
                SetKeys(UnityEngine.InputSystem.Key.D, UnityEngine.InputSystem.Key.S);
                NextStage(6);
                break;
            case 6:
                if (elapsed < 1.5f) break;
                SetKeys();
                Check("Stay inside plane edges", player.transform.position.x <= 19.5f && player.transform.position.z >= -9.5f && player.transform.position.y > 0.95f, player.transform.position.ToString());
                PlacePlayer(new Vector3(-14f, 1.05f, -5f), Vector3.right);
                dodgeStart = player.transform.position;
                SetKeys(UnityEngine.InputSystem.Key.D, UnityEngine.InputSystem.Key.Space);
                NextStage(7);
                break;
            case 7:
                wasDodging |= movement.IsDodging;
                if (elapsed < 0.15f) break;
                SetKeys();
                Check("Space performs a short dodge", wasDodging && Vector3.Distance(dodgeStart, player.transform.position) > 1.3f,
                    new { wasDodging, distance = Vector3.Distance(dodgeStart, player.transform.position) });
                NextStage(8);
                break;
            case 8:
                if (elapsed < 0.5f) break;
                PlacePlayer(new Vector3(8.6f, 1.05f, 0f), Vector3.right);
                SetKeys(UnityEngine.InputSystem.Key.J);
                NextStage(9);
                break;
            case 9:
                if (elapsed < 0.1f) break;
                SetKeys();
                Check("J damages dummy once", boss.HitCount == initialHitCount + 1 && boss.CurrentHealth == initialHealth - 10,
                    new { boss.HitCount, boss.CurrentHealth });
                Check("Dummy remains stationary", boss.transform.position == bossStart, boss.transform.position.ToString());
                NextStage(10);
                break;
            case 10:
                if (elapsed < 0.2f) break;
                Check("Hit flash clears", !boss.GetComponent<Renderer>().HasPropertyBlock(), "Base material restored");
                Finish(null);
                break;
        }
    }
    catch (System.Exception exception)
    {
        Finish(exception.Message);
    }
};
UnityEditor.EditorApplication.update += tick;
return "Scheduled Play Mode input validation; report: " + reportPath;
