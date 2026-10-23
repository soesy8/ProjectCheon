var effect = UnityEngine.Object.FindFirstObjectByType<Cheon.World.GateEnvironmentTransition>();
return new { frame = Time.frameCount, effect.IsSpiritWorld, effect.SpiritAmount, sunIntensity = RenderSettings.sun.intensity, sunColor = RenderSettings.sun.color.ToString(), ambient = RenderSettings.ambientSkyColor.ToString(), background = Camera.main.backgroundColor.ToString() };
