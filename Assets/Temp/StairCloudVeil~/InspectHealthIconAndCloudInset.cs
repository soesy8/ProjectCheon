var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var veil = game.GetComponent<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
var vitality = game.transform.Find("MountainPassHUD/Gameplay/PlayerVitality");
var health = vitality.Find("HealthIcon");
var duplicate = vitality.Find("Test_VitalityIcon");
var canvas = veil.GetComponentsInChildren<Canvas>(true).First(c => c.name == "StairCloudVeil");
var cloudImage = canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>().First();
return new { heartCount = vitality.GetComponentsInChildren<UnityEngine.UI.Image>(true).Count(i => i.name == "HealthIcon"),
    legacyHealthIconCount = vitality.GetComponentsInChildren<Cheon.MountainPass.Presentation.PassHudIcon>(true).Count(i => i.name == "Test_VitalityIcon"),
    heartSprite = health != null ? AssetDatabase.GetAssetPath(health.GetComponent<UnityEngine.UI.Image>().sprite) : "missing",
    energyIconPresent = vitality.Find("Test_EnergyIcon") != null,
    inset = new SerializedObject(veil).FindProperty("inwardAmount").floatValue,
    clouds = canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>().Length,
    cloudSortOrder = canvas.sortingOrder, hudSortOrder = vitality.GetComponentInParent<Canvas>().sortingOrder,
    clearRadius = cloudImage.material.GetVector("_ClearRadius").ToString(),
    fogClarity = game.GetComponent<Cheon.Prototype.MountainPass.PassMountainMist>().SummitClarity };
