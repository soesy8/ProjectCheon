var root = GameObject.Find("Test_ShrineEntranceWalls");
var player = GameObject.Find("Test_MountainPlayer");
var controller = player.GetComponent<CharacterController>();
Physics.SyncTransforms();
var colliders = root.GetComponentsInChildren<Collider>();
var bodies = root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Test_Wall_Middle_")).ToArray();

// 계단 중앙과 좌우 이동선에서 캐릭터 크기의 캡슐이 담과 겹치지 않는지 확인합니다.
var center = new Vector3(2.13f, 27.16f, 39.15f);
var ascent = new Vector3(0.77f, 0f, -0.64f).normalized;
var across = new Vector3(0.64f, 0f, 0.77f).normalized;
var radius = controller.radius * Mathf.Max(player.transform.lossyScale.x, player.transform.lossyScale.z);
var height = controller.height * player.transform.lossyScale.y;
var blocked = new List<object>();
var checks = 0;
foreach (var lane in new [] {-1.2f, 0f, 1.2f})
{
    for (var sample=0; sample<=28; sample++)
    {
        var foot = center + ascent * (-3f+sample*0.25f) + across*lane;
        var overlaps=Physics.OverlapCapsule(foot+Vector3.up*radius, foot+Vector3.up*(height-radius), radius)
            .Where(c=>c.transform.IsChildOf(root.transform)).Select(c=>c.name).ToArray();
        checks++;
        if(overlaps.Length>0) blocked.Add(new {foot=foot.ToString("F2"),overlaps=overlaps});
    }
}

var collisionChecks = bodies.Select(body => {
    var collider=body.GetComponentInChildren<MeshCollider>();
    var point=body.TransformPoint(new Vector3(-1.2f,1f,0f));
    var normal=body.TransformDirection(Vector3.forward);
    RaycastHit hit;
    var front=collider.Raycast(new Ray(point+normal*2f,-normal),out hit,4f);
    var back=collider.Raycast(new Ray(point-normal*2f,normal),out hit,4f);
    return new {name=body.parent.name+"/"+body.name,front=front,back=back};
}).ToArray();

var report = new {
    scene=root.scene.path,
    wallBodies=bodies.Length,
    endPosts=root.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Test_Wall_End_")),
    colliders=colliders.Length,
    invalidColliders=colliders.Count(c=>!c.enabled||c.isTrigger),
    connectedPrefabs=root.GetComponentsInChildren<Transform>().Count(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&PrefabUtility.GetPrefabInstanceStatus(t.gameObject)==PrefabInstanceStatus.Connected),
    passageChecks=checks,
    capsuleRadius=radius,
    capsuleHeight=height,
    blocked=blocked,
    failedWallCollisions=collisionChecks.Where(c=>!c.front||!c.back).ToArray()
};
System.IO.File.WriteAllText("Assets/Temp/ShrineEntranceWalls~/Test_WallVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
if(blocked.Count>0||collisionChecks.Any(c=>!c.front||!c.back))
    throw new InvalidOperationException("통로 또는 담 충돌 검증 실패: 검증 보고서를 확인하세요.");
var saved=UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.scene);
return new {saved=saved,report=report};
