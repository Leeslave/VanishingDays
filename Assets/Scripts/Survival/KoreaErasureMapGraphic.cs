using System;
using UnityEngine;
using UnityEngine.UI;

namespace VanishingDays
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KoreaErasureMapGraphic : MaskableGraphic
    {
        public ErasureLocationDefinition[] Places=new ErasureLocationDefinition[0];
        public string SelectedId="";
        VertexHelper mesh;
        protected override void Awake() { useLegacyMeshGeneration=false; base.Awake(); }
        Vector2 Point(float longitude,float latitude)
        {
            float scale=Mathf.Min(rectTransform.rect.width/4.5f,rectTransform.rect.height/6);
            return rectTransform.rect.center+new Vector2((longitude-127.75f)*.8f,(latitude-36))*scale;
        }
        void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color32 color)
        {
            int n=mesh.currentVertCount; mesh.AddVert(a,color,Vector2.zero); mesh.AddVert(b,color,Vector2.zero); mesh.AddVert(c,color,Vector2.zero); mesh.AddVert(d,color,Vector2.zero); mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
        }
        void Line(Vector2 a,Vector2 b,float width,Color32 color) { var side=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f; Quad(a+side,b+side,b-side,a-side,color); }
        void Ring(Vector2 center,float radius,float width,Color32 color)
        {
            for(int i=0;i<24;i++) { float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12; Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,color); }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            mesh=vh; mesh.Clear();
            var coast=new[]{new Vector2(126.2f,38.0f),new Vector2(126.75f,37.8f),new Vector2(127.1f,38.3f),new Vector2(127.65f,38.6f),new Vector2(128.35f,38.55f),new Vector2(128.7f,38.1f),new Vector2(129.35f,37.2f),new Vector2(129.5f,36.1f),new Vector2(129.3f,35.3f),new Vector2(128.85f,35.1f),new Vector2(128.55f,34.8f),new Vector2(128.1f,34.95f),new Vector2(127.8f,34.7f),new Vector2(127.3f,34.5f),new Vector2(126.7f,34.35f),new Vector2(126.3f,34.65f),new Vector2(126.1f,35.1f),new Vector2(126.4f,35.8f),new Vector2(126.2f,36.5f),new Vector2(126.7f,37.1f),new Vector2(126.2f,37.4f)};
            var tint=new Color32(98,117,104,255);
            for(int i=0;i<coast.Length;i++) Line(Point(coast[i].x,coast[i].y),Point(coast[(i+1)%coast.Length].x,coast[(i+1)%coast.Length].y),3,tint);
            var jeju=Point(126.55f,33.4f);
            for(int i=0;i<24;i++) { float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12; Line(jeju+new Vector2(Mathf.Cos(a)*17,Mathf.Sin(a)*6),jeju+new Vector2(Mathf.Cos(b)*17,Mathf.Sin(b)*6),3,tint); }
            bool seoulDrawn=false;
            var selected=Array.Find(Places,x=>x.id==SelectedId);
            foreach(var place in Places)
            {
                if(place.region=="서울") { if(seoulDrawn) continue; seoulDrawn=true; }
                bool active=place.id==SelectedId || place.region=="서울" && selected?.region=="서울";
                var p=place.region=="서울"?Point(126.98f,37.57f):Point(place.longitude,place.latitude);
                Ring(p,active?9:5,active?4:3,active?new Color32(234,196,118,255):new Color32(177,102,83,255));
            }
        }
    }
}
