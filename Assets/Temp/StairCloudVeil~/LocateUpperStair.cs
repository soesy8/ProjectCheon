Vector3 top = new Vector3(5.654829f, 27.145f, 36.23096f);
Vector3 forward = new Vector3(0.77002144f, 0f, -0.638018f);
return new[] { 0f, 4f, 8f, 12f, 16f }.Select(distance => {
    var origin = top - forward * distance + Vector3.up * 60f;
    return new { distance, origin = origin.ToString(), hits = Physics.RaycastAll(origin, Vector3.down, 90f)
        .Where(h => h.normal.y > 0.8f).OrderByDescending(h => h.point.y).Take(4)
        .Select(h => new { h.collider.name, point = h.point.ToString() }).ToArray() };
}).ToArray();
