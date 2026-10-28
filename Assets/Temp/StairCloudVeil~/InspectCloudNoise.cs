var veil = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
var canvas = veil.GetComponentsInChildren<Canvas>(true).First(c => c.name == "StairCloudVeil");
var images = canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>();
var material = images[0].material;
return new { frame = Time.frameCount, layers = images.Length,
    distinctSizes = images.Select(i => i.rectTransform.rect.size).Distinct().Count(),
    minimumLayerAlpha = images.Min(i => i.color.a), maximumLayerAlpha = images.Max(i => i.color.a),
    noiseStrength = material.GetFloat("_NoiseStrength"), noiseScale = material.GetFloat("_NoiseScale"),
    noiseTime = material.GetFloat("_NoiseTime"), raycastTargets = images.Count(i => i.raycastTarget),
    canvasOrder = canvas.sortingOrder };
