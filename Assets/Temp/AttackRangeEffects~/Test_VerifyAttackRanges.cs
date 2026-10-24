if (!Application.isPlaying) throw new Exception("플레이 모드가 필요합니다.");
var movement = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control = movement.AnimationControl;
var root = movement.transform.Find("Test_AttackRangeEffects");
var sectors = root.GetComponentsInChildren<Cheon.Testing.PlayerFan.Test_AttackRangeSector>();
var camera = Camera.main;
var reports = new List<object>();
var checks = new List<string>();
int phase = 0, phaseFrame = Time.frameCount, lastFrame = -1, startedFrame = Time.frameCount;
float pauseTime = 0f;
double began = EditorApplication.timeSinceStartup;
EditorApplication.CallbackFunction tick = null;
void Check(bool value, string label)
{
    if (!value) throw new Exception(label);
    checks.Add(label);
}
void Capture(int attack)
{
    var target = RenderTexture.GetTemporary(1024, 576, 24);
    var oldTarget = camera.targetTexture;
    var oldActive = RenderTexture.active;
    Texture2D texture = null;
    try
    {
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        texture = new Texture2D(1024, 576, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1024, 576), 0, 0);
        texture.Apply();
        System.IO.File.WriteAllBytes("Assets/Temp/AttackRangeEffects~/Test_Attack" + attack + "_Game.png", texture.EncodeToPNG());
        if (attack == 3)
        {
            var overview = new GameObject("Test_TemporaryRangeOverview", typeof(Camera));
            overview.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var overviewCamera = overview.GetComponent<Camera>();
                overviewCamera.CopyFrom(camera);
                overviewCamera.enabled = false;
                overviewCamera.orthographic = true;
                overviewCamera.orthographicSize = 6.7f;
                overviewCamera.targetTexture = target;
                Vector3 focus = movement.transform.position + movement.transform.forward * 2.8f;
                overview.transform.position = focus - movement.transform.forward * 9f + Vector3.up * 15f;
                overview.transform.LookAt(focus);
                overviewCamera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1024, 576), 0, 0);
                texture.Apply();
                System.IO.File.WriteAllBytes("Assets/Temp/AttackRangeEffects~/Test_Attack3_Overview.png", texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.Destroy(overview); }
        }
    }
    finally
    {
        camera.targetTexture = oldTarget;
        RenderTexture.active = oldActive;
        RenderTexture.ReleaseTemporary(target);
        if (texture != null) UnityEngine.Object.Destroy(texture);
    }
}
void Inspect(int attack)
{
    var active = sectors.Where(s => s.IsVisible).ToArray();
    Check(active.Length == (attack == 3 ? 2 : 1), "공격 " + attack + " 범위 수");
    foreach (var sector in active)
    {
        Check(sector.name.Contains("Attack" + attack + "_"), "타격별 범위 분리 " + sector.name);
        var vertices = sector.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v => sector.transform.TransformPoint(v)).ToArray();
        var offsets = vertices.Skip(1).Select(v => Vector3.ProjectOnPlane(v - sector.transform.position, Vector3.up)).ToArray();
        float distance = offsets.Max(v => v.magnitude);
        var angles = offsets.Select(v => Vector3.SignedAngle(sector.transform.forward, v, Vector3.up)).ToArray();
        Check(Mathf.Abs(distance - sector.Radius) < 0.002f, "수평 거리 " + sector.Radius + "m");
        Check(Mathf.Abs(angles.Max() - angles.Min() - sector.Angle) < 0.02f, "전체 각도 " + sector.Angle + "도");
        Check(Mathf.Abs(angles.Max() + angles.Min()) < 0.02f, "캐릭터 전방 중심 대칭");
        Check(sector.GetComponentsInChildren<Collider>().Length == 0, "이펙트에 충돌체 없음");
        reports.Add(new { sector.name, distance, angle = angles.Max() - angles.Min(), frame = control.NormalizedTime * control.GetAttackMaximumFrame(attack), vertices = vertices.Length });
    }
    Capture(attack);
}
void Finish(bool success, string reason)
{
    EditorApplication.update -= tick;
    if (control != null)
    {
        if (control.IsPaused) control.TogglePause();
        movement.ResetToSpawn();
    }
    System.IO.File.WriteAllText("Assets/Temp/AttackRangeEffects~/Test_Verification.json",
        Newtonsoft.Json.JsonConvert.SerializeObject(new { success, reason, checks, reports, advancedFrames = Time.frameCount - startedFrame, elapsed = EditorApplication.timeSinceStartup - began }, Newtonsoft.Json.Formatting.Indented));
}
control.PlayIdle();
if (!control.IsCombatRequested) control.EnterCombat();
tick = () =>
{
    try
    {
        if (!Application.isPlaying || control == null) { Finish(false, "플레이 종료"); return; }
        if (EditorApplication.timeSinceStartup - began > 35) { Finish(false, "검증 시간 초과"); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        if (phase == 0 && control.SelectedState == "DRAWN_IDLE" && !control.CharacterAnimator.IsInTransition(0))
        {
            control.PlayAttack1(); phase = 1;
        }
        else if (phase == 1 && sectors.Any(s => s.IsVisible))
        {
            Inspect(1);
            pauseTime = control.NormalizedTime;
            control.TogglePause(); phase = 2; phaseFrame = Time.frameCount;
        }
        else if (phase == 2 && Time.frameCount >= phaseFrame + 10)
        {
            Check(Mathf.Abs(control.NormalizedTime - pauseTime) < 0.001f, "일시정지 중 모션 유지");
            Check(sectors.Count(s => s.IsVisible) == 1, "일시정지 중 범위 유지");
            control.TogglePause(); phase = 3;
        }
        else if (phase == 3 && control.SelectedState == "DRAWN_IDLE" && !control.CharacterAnimator.IsInTransition(0))
        {
            Check(sectors.All(s => !s.IsVisible), "1타 종료 후 범위 숨김");
            movement.transform.rotation *= Quaternion.Euler(0, 90f, 0);
            control.PlayAttack2(); phase = 4;
        }
        else if (phase == 4 && sectors.Any(s => s.IsVisible))
        {
            Inspect(2);
            control.PlayHit(); phase = 5; phaseFrame = Time.frameCount;
        }
        else if (phase == 5 && Time.frameCount > phaseFrame + 1)
        {
            Check(sectors.All(s => !s.IsVisible), "피격 취소 시 범위 즉시 숨김");
            phase = 6;
        }
        else if (phase == 6 && control.SelectedState == "DRAWN_IDLE" && !control.CharacterAnimator.IsInTransition(0))
        {
            control.PlayAttack3(); phase = 7;
        }
        else if (phase == 7 && sectors.Count(s => s.IsVisible) == 2)
        {
            Inspect(3); phase = 8;
        }
        else if (phase == 8 && control.SelectedState == "DRAWN_IDLE" && !control.CharacterAnimator.IsInTransition(0))
        {
            Check(sectors.All(s => !s.IsVisible), "3타 종료 후 두 범위 숨김");
            Finish(true, "공격 범위·전방 방향·일시정지·피격 취소·종료 처리 확인");
        }
    }
    catch (Exception exception) { Finish(false, exception.ToString()); }
};
EditorApplication.update += tick;
return "공격 범위 플레이 검증 시작";
