using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cheon.Testing
{
    public class Test_CheonWorld : MonoBehaviour
    {
        [Serializable] public class Palette
        {
            public Material material;
            public Color reality;
            public Color spirit;
            [NonSerialized] public Material runtimeMaterial;
        }
        [SerializeField] private Palette[] palettes;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Light sun;
        [SerializeField] private Transform[] motes;
        [SerializeField] private Transform gateHalo;
        [SerializeField] private Transform gateLintel;
        [SerializeField] private Color realityFog = new Color(.55f, .73f, .72f);
        [SerializeField] private Color spiritFog = new Color(.12f, .13f, .25f);
        [SerializeField] private float transitionSpeed = .65f;
        [Header("영역별 조명과 하늘")]
        [SerializeField] private Material skyMaterial;
        [SerializeField] private Color realityAmbient = new Color(.68f,.77f,.76f);
        [SerializeField] private Color spiritAmbient = new Color(.4f,.36f,.58f);
        [SerializeField] private Color realitySun = new Color(1,.88f,.69f);
        [SerializeField] private Color spiritSun = new Color(.64f,.66f,1);
        [SerializeField] private float realitySunIntensity = 1.2f;
        [SerializeField] private float spiritSunIntensity = .85f;
        [SerializeField] private Color realitySky = new Color(.28f,.62f,.9f);
        [SerializeField] private Color spiritSky = new Color(.06f,.14f,.28f);
        private Material runtimeSky, previousSky;
        public float SpiritAmount { get; private set; }
        public float BoundaryZ => gateHalo != null ? gateHalo.position.z : 0;
        private bool spirit;
        private Vector3[] moteHomes;
        private float lastAmount = -1;

        private void Awake()
        {
            if (skyMaterial != null)
            {
                previousSky = RenderSettings.skybox; runtimeSky = new Material(skyMaterial);
                RenderSettings.skybox = runtimeSky;
            }
            // 공유 에셋을 직접 물들이지 않고 이 씬의 머티리얼 사본에만 전환을 적용한다.
            var replacements = new Dictionary<Material, Material>();
            foreach (var palette in palettes)
            {
                palette.runtimeMaterial = new Material(palette.material);
                replacements[palette.material] = palette.runtimeMaterial;
            }
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && replacements.TryGetValue(materials[i], out var replacement)) { materials[i] = replacement; changed = true; }
                if (changed) renderer.sharedMaterials = materials;
            }
            moteHomes = new Vector3[motes.Length];
            for (int i = 0; i < motes.Length; i++) moteHomes[i] = motes[i].position;
        }

        public void SetSpirit(bool value) { spirit = value; }
        private void Update()
        {
            SpiritAmount = Mathf.MoveTowards(SpiritAmount, spirit ? 1 : 0, Time.deltaTime * transitionSpeed);
            if (!Mathf.Approximately(lastAmount, SpiritAmount))
            {
                foreach (var palette in palettes) palette.runtimeMaterial.SetColor("_BaseColor", Color.Lerp(palette.reality, palette.spirit, SpiritAmount));
                Color fog = Color.Lerp(realityFog, spiritFog, SpiritAmount);
                RenderSettings.fogColor = fog; viewCamera.backgroundColor = fog;
                RenderSettings.ambientLight = Color.Lerp(realityAmbient, spiritAmbient, SpiritAmount);
                sun.color = Color.Lerp(realitySun, spiritSun, SpiritAmount);
                sun.intensity = Mathf.Lerp(realitySunIntensity, spiritSunIntensity, SpiritAmount);
                if (runtimeSky != null)
                {
                    runtimeSky.SetColor("_ZenithColor",Color.Lerp(realitySky,spiritSky,SpiritAmount));
                    runtimeSky.SetColor("_HorizonColor",fog);
                    runtimeSky.SetColor("_CloudColor",Color.Lerp(new Color(1,.95f,.83f),new Color(.27f,.40f,.56f),SpiritAmount));
                }
                lastAmount = SpiritAmount;
            }
            for (int i = 0; i < motes.Length; i++)
            {
                if (motes[i] == null) continue;
                motes[i].position = moteHomes[i] + new Vector3(Mathf.Sin(Time.time * .36f + i) * .8f, Mathf.Sin(Time.time * .8f + i * 3) * .45f, Mathf.Cos(Time.time * .3f + i) * .6f);
                motes[i].Rotate(0, Time.deltaTime * 20, 0);
            }
            if (gateHalo != null) gateHalo.Rotate(0, 0, Time.deltaTime * 8, Space.Self);
            // 경계를 넘으면 문의 실체가 영체화되어 도사와 붉은 공격 예고를 가리지 않는다.
            if (gateLintel != null) gateLintel.gameObject.SetActive(SpiritAmount < .4f);
        }
        private void OnDestroy()
        {
            foreach (var palette in palettes) if (palette.runtimeMaterial != null) Destroy(palette.runtimeMaterial);
            if (runtimeSky != null)
            {
                if (RenderSettings.skybox == runtimeSky) RenderSettings.skybox = previousSky;
                Destroy(runtimeSky);
            }
        }
    }
}
