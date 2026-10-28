var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var view = game.View;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var type = view.GetType();
var focus = (UnityEngine.Vector3)type.GetField("focus", flags).GetValue(view);
float extra = (float)type.GetField("extraDistance", flags).GetValue(view);
var desired = (UnityEngine.Vector3)type.GetMethod("GetOrbitPosition", flags).Invoke(view, new object[] { extra });
var delta = desired - focus;
var records = new System.Collections.Generic.List<object>();
foreach (var hit in UnityEngine.Physics.SphereCastAll(focus, .22f, delta.normalized, delta.magnitude, UnityEngine.Physics.DefaultRaycastLayers, UnityEngine.QueryTriggerInteraction.Ignore))
    records.Add(new { name = hit.collider.name, type = hit.collider.GetType().Name, hit.distance, bounds = hit.collider.bounds.ToString(), layer = hit.collider.gameObject.layer });
return new { player = game.Player.position.ToString(), camera = view.transform.position.ToString(), focus = focus.ToString(), desired = desired.ToString(), records };
