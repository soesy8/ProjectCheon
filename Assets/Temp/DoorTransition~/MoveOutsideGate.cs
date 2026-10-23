var effect = UnityEngine.Object.FindFirstObjectByType<Cheon.World.GateEnvironmentTransition>();
var preview = GameObject.Find("Gate Crossing Preview").transform;
Vector3 current = effect.transform.InverseTransformPoint(preview.position);
preview.position = effect.transform.TransformPoint(new Vector3(8f, -3f, -current.z));
return new { frame = Time.frameCount, position = preview.position.ToString() };
