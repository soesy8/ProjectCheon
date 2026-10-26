var game=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var player=game.Player;var control=player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>().AnimationControl;
game.View.enabled=false;var camera=game.View.GetComponent<Camera>();camera.fieldOfView=35f;
camera.transform.position=player.position+new Vector3(-3f,1.05f,0f);camera.transform.LookAt(player.position+Vector3.up*0.85f);
return new{control.SelectedState,control.ReactionFrame};
