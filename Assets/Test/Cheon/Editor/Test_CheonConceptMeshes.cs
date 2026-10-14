using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 기존 메시의 경계 상자에 맞춰 아트 디테일만 교체하는 메시 제작 도구다.
public static class Test_CheonConceptMeshes
{
    private class MeshData
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int start = vertices.Count; vertices.AddRange(new[] { a,b,c }); colors.AddRange(new[] { color,color,color });
            triangles.AddRange(new[] { start,start+1,start+2 });
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        { Triangle(a,b,c,color); Triangle(a,c,d,color); }
        public void Sphere(Vector3 center, Vector3 radius, Color color, int segments = 10, int rings = 6, float roughness = .05f)
        {
            for (int y = 0; y < rings; y++) for (int x = 0; x < segments; x++)
            {
                Vector3 a = SpherePoint(center,radius,x,y,segments,rings,roughness);
                Vector3 b = SpherePoint(center,radius,x,y+1,segments,rings,roughness);
                Vector3 c = SpherePoint(center,radius,x+1,y+1,segments,rings,roughness);
                Vector3 d = SpherePoint(center,radius,x+1,y,segments,rings,roughness);
                float tint = .86f + .14f * Mathf.PerlinNoise(x*.7f+center.x*9,y*.7f+center.z*9);
                Quad(a,b,c,d,color*tint);
            }
        }
        private static Vector3 SpherePoint(Vector3 center, Vector3 radius, int x, int y, int segments, int rings, float roughness)
        {
            float angle = x * Mathf.PI * 2 / segments, elevation = y * Mathf.PI / rings;
            var direction = new Vector3(Mathf.Sin(angle)*Mathf.Sin(elevation),Mathf.Cos(elevation),Mathf.Cos(angle)*Mathf.Sin(elevation));
            float noise = Mathf.PerlinNoise(direction.x*3+center.x*11,direction.z*3+direction.y*5+center.z*11);
            return center + Vector3.Scale(direction,radius)*(1+(noise-.5f)*roughness);
        }
        public Mesh Finish(Bounds bounds, bool fillBounds = true)
        {
            if (vertices.Any(p => float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z)))
                throw new System.InvalidOperationException("메시에 유효하지 않은 좌표가 포함되어 있습니다.");
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
            Bounds current = mesh.bounds;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i]-current.center;
                p = new Vector3(p.x/Mathf.Max(.0001f,current.size.x),p.y/Mathf.Max(.0001f,current.size.y),p.z/Mathf.Max(.0001f,current.size.z));
                vertices[i] = fillBounds ? bounds.center+Vector3.Scale(p,bounds.size) : new Vector3(bounds.center.x+vertices[i].x*bounds.extents.x,bounds.min.y+vertices[i].y*bounds.size.y,bounds.center.z+vertices[i].z*bounds.extents.z);
            }
            mesh.SetVertices(vertices); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }

    public static Mesh PineCrown(Bounds bounds, int variant)
    {
        var mesh = new MeshData();
        Color deep = new Color(.42f,.68f,.58f), light = new Color(.78f,.91f,.60f);
        mesh.Sphere(new Vector3(0,.65f,0),new Vector3(.52f,.35f,.46f),light,12,7,.12f);
        for (int i = 0; i < 9; i++)
        {
            float angle = i*Mathf.PI*2/9 + variant*.31f;
            var center = new Vector3(Mathf.Cos(angle)*.58f,.34f+.10f*Mathf.Sin(i*2.7f),Mathf.Sin(angle)*.53f);
            mesh.Sphere(center,new Vector3(.42f,.24f,.38f),Color.Lerp(deep,light,(i%3)*.25f),10,6,.12f);
        }
        // 수관 끝의 짧은 잎을 모델링해 둥근 덩어리 위에 소나무 결을 만든다.
        for (int i = 0; i < 100; i++)
        {
            float angle = i*2.39996f + variant, radius = Mathf.Sqrt((i+.5f)/100)*.94f;
            var root = new Vector3(Mathf.Cos(angle)*radius,.48f+(1-radius)*.46f,Mathf.Sin(angle)*radius*.9f);
            Vector3 tip = root+new Vector3(Mathf.Cos(angle)*.07f,.13f,Mathf.Sin(angle)*.07f);
            Vector3 side = new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle))*.035f;
            mesh.Triangle(root-side,tip,root+side,light);
            mesh.Triangle(root+side,tip,root-side,light);
        }
        return mesh.Finish(bounds);
    }

    public static Mesh CurvedTrunk(Bounds bounds)
    {
        var mesh = new MeshData();
        for (int y = 0; y < 12; y++) for (int x = 0; x < 10; x++)
        {
            float a = x*Mathf.PI/5, b = (x+1)*Mathf.PI/5;
            float low = y/12f, high = (y+1)/12f;
            mesh.Quad(TrunkPoint(low,a),TrunkPoint(high,a),TrunkPoint(high,b),TrunkPoint(low,b),Color.white);
        }
        return mesh.Finish(bounds);
    }
    private static Vector3 TrunkPoint(float t, float angle)
    {
        float radius = .32f*(1-.62f*t);
        return new Vector3(Mathf.Sin(t*4)*.15f+Mathf.Cos(angle)*radius,t*2-1,Mathf.Sin(t*3)*.06f+Mathf.Sin(angle)*radius);
    }

    public static Mesh Rock(Bounds bounds, int variant)
    {
        var mesh = new MeshData();
        mesh.Sphere(new Vector3(0,.5f,0),new Vector3(.95f,.55f,.85f),Color.white,9+variant%3,6,.3f);
        return mesh.Finish(bounds);
    }

    public static Mesh Karst(Bounds bounds, int variant)
    {
        var mesh = new MeshData(); const int sides = 9, levels = 7;
        for (int level = 0; level < levels; level++) for (int side = 0; side < sides; side++)
        {
            float lo = level/(float)levels, hi = (level+1f)/levels;
            Color color = Color.Lerp(new Color(.63f,.72f,.72f),new Color(.94f,.85f,.65f),hi);
            mesh.Quad(KarstPoint(lo,side,sides,variant),KarstPoint(hi,side,sides,variant),KarstPoint(hi,side+1,sides,variant),KarstPoint(lo,side+1,sides,variant),color);
        }
        for (int side = 0; side < sides; side++)
            mesh.Triangle(new Vector3(0,1,0),KarstPoint(1,side+1,sides,variant),KarstPoint(1,side,sides,variant),new Color(.46f,.64f,.29f));
        for (int i = 0; i < 7; i++)
        {
            float angle = i*2.4f+variant;
            mesh.Sphere(new Vector3(Mathf.Cos(angle)*.35f,1.02f,Mathf.Sin(angle)*.35f),new Vector3(.24f,.08f,.22f),new Color(.22f,.48f,.32f),7,4);
        }
        return mesh.Finish(bounds);
    }
    private static Vector3 KarstPoint(float t, int side, int sides, int variant)
    {
        float angle = side*Mathf.PI*2/sides;
        float radius = .7f + Mathf.Sin(t*16+variant)*.065f - Mathf.Sin(t*Mathf.PI)*.25f;
        radius *= .9f+.15f*Mathf.PerlinNoise(Mathf.Cos(angle)*3+variant,Mathf.Sin(angle)*3+variant);
        return new Vector3(Mathf.Cos(angle)*radius,t,Mathf.Sin(angle)*radius);
    }

    public static Mesh Cloud(Bounds bounds, int variant)
    {
        var mesh = new MeshData();
        for (int i = 0; i < 9; i++)
        {
            float x = (i-4)*.18f, z = Mathf.Sin(i*2.4f+variant)*.12f;
            mesh.Sphere(new Vector3(x,Mathf.Sin(i*1.7f)*.05f,z),new Vector3(.30f,.20f+(i%3)*.04f,.32f),Color.white,10,6,.04f);
        }
        return mesh.Finish(bounds);
    }

    public static Mesh Botanical(Bounds bounds, int variant)
    {
        var mesh = new MeshData();
        for (int i = 0; i < 10; i++)
        {
            float angle = i*2.39996f+variant;
            Vector3 tip = new Vector3(Mathf.Cos(angle)*.7f,.35f+(i%4)*.14f,Mathf.Sin(angle)*.7f);
            Vector3 mid = tip*.55f+Vector3.up*.15f, side = new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle))*.13f;
            Color color = i%2==0 ? new Color(.35f,.58f,.13f) : new Color(.56f,.73f,.21f);
            mesh.Quad(Vector3.zero,mid-side,tip,mid+side,color); mesh.Quad(Vector3.zero,mid+side,tip,mid-side,color);
        }
        if (variant%3 != 2)
        {
            var center = new Vector3(.04f,.80f,0); Color petals = variant%3==0 ? new Color(1,.94f,.74f) : new Color(1,.46f,.12f);
            for (int i = 0; i < 6; i++)
            {
                float a = i*Mathf.PI/3;
                mesh.Sphere(center+new Vector3(Mathf.Cos(a)*.21f,0,Mathf.Sin(a)*.21f),new Vector3(.17f,.045f,.12f),petals,6,3);
            }
            mesh.Sphere(center+Vector3.up*.04f,new Vector3(.11f,.065f,.11f),new Color(1,.65f,.10f),7,3);
        }
        return mesh.Finish(bounds);
    }

    public static Mesh Roof(Bounds bounds)
    {
        var mesh = new MeshData();const int across = 60, along = 24;
        for (int z = 0; z < along; z++) for (int x = 0; x < across; x++)
        {
            Vector3 a = RoofPoint(x,z,across,along), b = RoofPoint(x,z+1,across,along);
            Vector3 c = RoofPoint(x+1,z+1,across,along), d = RoofPoint(x+1,z,across,along);
            float shade = .85f+.15f*Mathf.Cos(x*Mathf.PI/3);
            mesh.Quad(a,b,c,d,new Color(shade,shade,shade));
        }
        mesh.Sphere(new Vector3(0,.92f,0),new Vector3(1.02f,.05f,.07f),Color.white,20,5);
        foreach (int side in new[] { -1,1 })
            mesh.Sphere(new Vector3(0,.18f,side*.98f),new Vector3(1.2f,.04f,.045f),Color.white,24,4);
        return mesh.Finish(bounds);
    }
    private static Vector3 RoofPoint(int ix, int iz, int across, int along)
    {
        float x = (ix/(float)across*2-1)*1.2f, z = iz/(float)along*2-1;
        float hip = Mathf.Clamp01(Mathf.Max(Mathf.Abs(z),Mathf.Max(0,(Mathf.Abs(x)-.80f)/.4f)));
        float y = .12f+.75f*Mathf.Pow(1-hip,1.65f)+.13f*Mathf.Pow(hip,7);
        y += .11f*Mathf.Pow(Mathf.Abs(x)/1.2f,8)*Mathf.Pow(Mathf.Abs(z),5);
        y += .023f*Mathf.Cos(ix*Mathf.PI/3);
        return new Vector3(x,y,z);
    }

    public static Mesh BeveledBox(Bounds bounds)
    {
        var mesh = new MeshData(); const int segments = 5;
        for (int axis = 0; axis < 3; axis++) foreach (int sign in new[] { -1,1 })
            for (int y = 0; y < segments; y++) for (int x = 0; x < segments; x++)
            {
                Vector3 a = BoxPoint(axis,sign,x,y,segments), b = BoxPoint(axis,sign,x+1,y,segments);
                Vector3 c = BoxPoint(axis,sign,x+1,y+1,segments), d = BoxPoint(axis,sign,x,y+1,segments);
                Vector3 normal = Vector3.zero; normal[axis] = sign;
                if (Vector3.Dot(Vector3.Cross(b-a,c-a),normal)>0) mesh.Quad(a,b,c,d,Color.white);
                else mesh.Quad(a,d,c,b,Color.white);
            }
        return mesh.Finish(bounds);
    }
    private static Vector3 BoxPoint(int axis, int sign, int x, int y, int segments)
    {
        Vector3 p = Vector3.zero; p[axis] = sign*.5f;p[(axis+1)%3] = x/(float)segments-.5f;p[(axis+2)%3] = y/(float)segments-.5f;
        Vector3 core = new Vector3(Mathf.Clamp(p.x,-.43f,.43f),Mathf.Clamp(p.y,-.43f,.43f),Mathf.Clamp(p.z,-.43f,.43f));
        return core+(p-core).normalized*.07f;
    }

    public static Mesh Obelisk(Bounds bounds)
    {
        var mesh = new MeshData();
        Vector2[] shape = { new Vector2(-.65f,-.9f),new Vector2(.65f,-.9f),new Vector2(.9f,-.65f),new Vector2(.9f,.65f),new Vector2(.65f,.9f),new Vector2(-.65f,.9f),new Vector2(-.9f,.65f),new Vector2(-.9f,-.65f) };
        for (int i = 0; i < shape.Length; i++)
        {
            Vector2 a = shape[i], b = shape[(i+1)%shape.Length];
            Vector3 lowA = new Vector3(a.x,0,a.y), lowB = new Vector3(b.x,0,b.y);
            Vector3 highA = new Vector3(a.x*.5f,1,a.y*.5f), highB = new Vector3(b.x*.5f,1,b.y*.5f);
            mesh.Quad(lowA,highA,highB,lowB,Color.white);
            mesh.Triangle(new Vector3(0,1,0),highB,highA,Color.white);
        }
        return mesh.Finish(bounds);
    }

    public static Mesh SpiritOrb(Bounds bounds)
    {
        var mesh = new MeshData();
        mesh.Sphere(Vector3.zero,Vector3.one*.5f,Color.white,14,10,0);
        return mesh.Finish(bounds);
    }

    public static Mesh Vines(Bounds bounds, int variant)
    {
        var mesh = new MeshData();
        for (int strand = 0; strand < 3; strand++) for (int leaf = 0; leaf < 11; leaf++)
        {
            float t = leaf/11f, x = (strand-1)*.43f+Mathf.Sin(t*9+variant)*.09f;
            var center = new Vector3(x,1-t*.9f,-.72f+Mathf.Abs(x)*.1f);
            mesh.Sphere(center+Vector3.right*(leaf%2==0?.06f:-.06f),new Vector3(.105f,.045f,.045f),new Color(.47f,.69f,.20f),6,3);
        }
        return mesh.Finish(bounds,false);
    }

    public static Mesh LanternFrame(Bounds bounds)
    {
        var mesh = new MeshData();
        foreach (int x in new[] { -1,1 }) foreach (int z in new[] { -1,1 })
        {
            Vector3 center = new Vector3(x*.44f,0,z*.44f), half = new Vector3(.045f,.5f,.045f);
            for (int axis = 0; axis < 3; axis++) foreach (int sign in new[] { -1,1 })
            {
                Vector3 a = center, b = center, c = center, d = center;
                a[axis] += sign*half[axis]; b[axis] += sign*half[axis]; c[axis] += sign*half[axis]; d[axis] += sign*half[axis];
                int u = (axis+1)%3, v = (axis+2)%3;
                a[u] -= half[u]; a[v] -= half[v];b[u] += half[u];b[v] -= half[v];c[u] += half[u];c[v] += half[v];d[u] -= half[u];d[v] += half[v];
                Vector3 normal = Vector3.zero; normal[axis] = sign;
                if (Vector3.Dot(Vector3.Cross(b-a,c-a),normal)>0) mesh.Quad(a,b,c,d,Color.white);else mesh.Quad(a,d,c,b,Color.white);
            }
        }
        return mesh.Finish(bounds);
    }
}
