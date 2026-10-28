var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.name == "StairCloudVeil");
return new { canvasSize = ((RectTransform)canvas.transform).rect.size.ToString(), scale = canvas.transform.lossyScale.ToString(),
    images = canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>().Select(i => new { i.name,
        position = i.rectTransform.anchoredPosition.ToString(), size = i.rectTransform.rect.size.ToString(),
        color = i.canvasRenderer.GetColor().ToString(), alpha = i.canvasRenderer.GetInheritedAlpha(),
        culled = i.canvasRenderer.cull, queue = i.material.renderQueue, shader = i.material.shader.name }).ToArray() };
