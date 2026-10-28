using UnityEngine;
using UnityEngine.UI;

namespace Cheon.MountainPass.Presentation
{
    // 작은 화면에서도 읽히는 색면 아이콘. 텍스처와 런타임 폰트 아틀라스를 추가하지 않는다.
    [RequireComponent(typeof(CanvasRenderer))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Cheon.Tests.CombatPresentation", "Assembly-CSharp", "Test_HudIcon")]
    public class PassHudIcon : MaskableGraphic
    {
        public enum IconKind { Gourd, Vitality, Fan }
        [SerializeField, InspectorName("아이콘 종류")] private IconKind kind;
        public void SetKind(IconKind value) { kind = value; raycastTarget = false; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Color ink = new Color(.09f, .14f, .15f, 1);
            Color jade = new Color(.32f, .73f, .64f, 1);
            if (kind == IconKind.Gourd)
            {
                Color ochre = new Color(.7f, .41f, .12f, 1), light = new Color(.84f, .57f, .22f, 1);
                Ellipse(mesh, .43f, .32f, .3f, .29f, ink);
                Ellipse(mesh, .44f, .33f, .265f, .255f, ochre);
                Ellipse(mesh, .49f, .65f, .18f, .2f, ink);
                Ellipse(mesh, .49f, .65f, .15f, .17f, light);
                Quad(mesh, new Vector2(.43f,.8f),new Vector2(.55f,.8f),new Vector2(.56f,.92f),new Vector2(.45f,.92f),ink);
                Quad(mesh, new Vector2(.46f,.81f),new Vector2(.52f,.81f),new Vector2(.53f,.89f),new Vector2(.47f,.89f),ochre);
                Line(mesh, new Vector2(.33f,.52f),new Vector2(.6f,.49f),.052f,ink);
                Line(mesh, new Vector2(.33f,.52f),new Vector2(.6f,.49f),.028f,jade);
                Line(mesh, new Vector2(.6f,.49f),new Vector2(.72f,.28f),.025f,jade);
                Quad(mesh,new Vector2(.67f,.31f),new Vector2(.76f,.3f),new Vector2(.84f,.12f),new Vector2(.66f,.1f),ink);
                Quad(mesh,new Vector2(.69f,.29f),new Vector2(.74f,.28f),new Vector2(.8f,.14f),new Vector2(.69f,.13f),jade);
            }
            else if (kind == IconKind.Vitality)
            {
                Quad(mesh,new Vector2(.2f,.3f),new Vector2(.27f,.68f),new Vector2(.83f,.88f),new Vector2(.72f,.36f),jade);
                Line(mesh,new Vector2(.2f,.18f),new Vector2(.7f,.72f),.06f,ink);
            }
            else
            {
                Vector2 root = new Vector2(.5f,.15f);
                Color paper = new Color(.78f,.88f,.83f,1);
                for(int i=0;i<7;i++)
                {
                    float a = (28 + i * 18) * Mathf.Deg2Rad, b = (46 + i * 18) * Mathf.Deg2Rad;
                    Triangle(mesh,root,root+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.75f,root+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.75f,i%2==0?paper:jade);
                    Line(mesh,root,root+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.7f,.02f,ink);
                }
            }
        }
        private Vector3 Point(Vector2 value)
        { Rect r=rectTransform.rect; return new Vector3(r.xMin+value.x*r.width,r.yMin+value.y*r.height); }
        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int first=mesh.currentVertCount;
            mesh.AddVert(Point(a),tint,Vector2.zero); mesh.AddVert(Point(b),tint,Vector2.zero); mesh.AddVert(Point(c),tint,Vector2.zero);
            mesh.AddTriangle(first,first+1,first+2);
        }
        private void Quad(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
        { Triangle(mesh,a,b,c,tint); Triangle(mesh,a,c,d,tint); }
        private void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color tint)
        { Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f; Quad(mesh,a-n,a+n,b+n,b-n,tint); }
        private void Ellipse(VertexHelper mesh,float x,float y,float width,float height,Color tint)
        {
            Vector2 center=new Vector2(x,y);
            for(int i=0;i<24;i++)
            { float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12; Triangle(mesh,center,center+new Vector2(Mathf.Cos(a)*width,Mathf.Sin(a)*height),center+new Vector2(Mathf.Cos(b)*width,Mathf.Sin(b)*height),tint); }
        }
    }
}
