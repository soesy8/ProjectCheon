var group = GameObject.Find("ApproachLanterns").transform;
Vector3 third = (group.Find("LeftLantern_74").position + group.Find("RightLantern_74").position) * 0.5f;
Vector3 top = (group.Find("LeftLantern_92").position + group.Find("RightLantern_92").position) * 0.5f;
Vector3 second = (group.Find("LeftLantern_83").position + group.Find("RightLantern_83").position) * 0.5f;
return new[] { third, (top + second) * 0.5f }.Select(position => new {
    lanternCenter = position.ToString("F4"), ground = Physics.RaycastAll(new Vector3(position.x, 100f, position.z), Vector3.down, 120f)
        .Where(h => h.normal.y > 0.8f).OrderByDescending(h => h.point.y).Take(3)
        .Select(h => new { h.collider.name, point = h.point.ToString("F4") }).ToArray() }).ToArray();
