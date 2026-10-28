var names = new[] { "ShrineStairWalkwayCollider", "ShrineEntrance_StairLanding" };
return names.Select(n => GameObject.Find(n)).Where(o => o != null).Select(o => new {
    o.name, position = o.transform.position.ToString(), colliders = o.GetComponentsInChildren<Collider>().Select(c => new {
        c.name, center = c.bounds.center.ToString(), size = c.bounds.size.ToString() }).ToArray() }).ToArray();
