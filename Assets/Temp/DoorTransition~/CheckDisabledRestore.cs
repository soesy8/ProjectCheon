var effect = UnityEngine.Object.FindFirstObjectByType<Cheon.World.GateEnvironmentTransition>();
if (!effect.IsSpiritWorld || effect.SpiritAmount < 0.99f)
    throw new System.InvalidOperationException("영계 전환 완료 후 복원을 검증하세요.");
effect.enabled = false;
bool restored = Mathf.Approximately(RenderSettings.sun.intensity, 1.5f) &&
    Vector4.Distance(Camera.main.backgroundColor, new Color(0.6f, 0.7f, 0.74f)) < 0.001f;
if (!restored) throw new System.InvalidOperationException("비활성화 후 조명 복원 실패");
return new { restored, sunIntensity = RenderSettings.sun.intensity, background = Camera.main.backgroundColor.ToString() };
