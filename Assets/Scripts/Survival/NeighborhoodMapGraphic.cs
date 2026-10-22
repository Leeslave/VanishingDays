using UnityEngine;
using UnityEngine.UI;

namespace VanishingDays
{
    // A schematic neighborhood, drawn as UI geometry so it stays sharp in portrait layouts.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NeighborhoodMapGraphic : MaskableGraphic
    {
        protected override void Awake()
        {
            // Match built-in uGUI graphics: explicitly use the VertexHelper path in
            // both Play mode and the player, rather than the legacy Mesh callback.
            useLegacyMeshGeneration=false;
            base.Awake();
        }
        public string CurrentLocation;
        public bool SupplyRumorUnlocked;
        public static Vector2 Position(string id)
        {
            switch(id) { case "home":return new Vector2(.27f,.72f); case "store":return new Vector2(.70f,.55f); case "empty_house":return new Vector2(.25f,.30f); case "supply_site":return new Vector2(.58f,.83f); default:return new Vector2(.73f,.19f); }
        }
        VertexHelper mesh;
        Vector2 Point(float x,float y) => new Vector2(rectTransform.rect.xMin+x*rectTransform.rect.width,rectTransform.rect.yMax-y*rectTransform.rect.height);
        void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color32 tint)
        {
            int n=mesh.currentVertCount; mesh.AddVert(a,tint,Vector2.zero); mesh.AddVert(b,tint,Vector2.zero); mesh.AddVert(c,tint,Vector2.zero); mesh.AddVert(d,tint,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
        }
        void Block(float x,float y,float w,float h,Color32 tint) => Quad(Point(x,y),Point(x+w,y),Point(x+w,y+h),Point(x,y+h),tint);
        void Line(Vector2 a,Vector2 b,float width,Color32 tint)
        {
            Vector2 side=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            Quad(a+side,b+side,b-side,a-side,tint);
        }
        void Ring(Vector2 center,float radius,float width,Color32 tint)
        {
            for(int i=0;i<48;i++) { float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24; Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,tint); }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            mesh=vh; mesh.Clear();
            Block(0,0,1,1,new Color32(24,29,29,255));
            var random=new System.Random(2031);
            for(int row=0;row<13;row++) for(int col=0;col<10;col++)
            {
                float x=.018f+col*.098f,y=.015f+row*.076f;
                byte shade=(byte)random.Next(38,65);
                Block(x,y,.063f+(float)random.NextDouble()*.018f,.045f+(float)random.NextDouble()*.015f,new Color32(shade,shade,(byte)(shade-3),255));
                Block(x+.008f,y+.006f,.042f,.004f,new Color32(76,79,73,200));
            }
            // Main road, residential streets, and narrow connecting lanes.
            var road=new Color32(91,94,85,255); var center=new Color32(151,147,123,125);
            Line(Point(0,.88f),Point(1,.37f),32,road); Line(Point(0,.88f),Point(1,.37f),2,center);
            Line(Point(.05f,0),Point(.60f,1),22,road); Line(Point(.05f,0),Point(.60f,1),1,center);
            Line(Point(.64f,0),Point(.93f,1),24,road); Line(Point(.64f,0),Point(.93f,1),1,center);
            Line(Point(0,.38f),Point(1,.12f),18,road);
            Line(Point(0,.58f),Point(1,.79f),15,road);
            Line(Point(.27f,.72f),Point(.25f,.30f),8,road);
            Line(Point(.70f,.55f),Point(.73f,.19f),8,road);
            // Uncertain edges of the hand-kept map.
            for(int i=0;i<20;i++) { float w=(float)random.NextDouble()*.13f; Block(0,i*.05f,w,.05f,new Color32(12,18,20,190)); Block(1-w,i*.05f,w,.05f,new Color32(12,18,20,190)); }
            foreach(string id in new[]{"home","store","empty_house","school","supply_site"})
            {
                if(id=="supply_site" && !SupplyRumorUnlocked) continue;
                Vector2 p=Position(id),c=Point(p.x,p.y); bool current=id==CurrentLocation;
                Ring(c,36,7,new Color32(12,18,20,255)); Ring(c,31,4,current?new Color32(211,173,110,255):new Color32(222,216,198,255));
                var ink=new Color32(222,216,198,255);
                if(id=="home")
                {
                    Line(c+new Vector2(-16,2),c+new Vector2(0,17),5,ink); Line(c+new Vector2(0,17),c+new Vector2(16,2),5,ink);
                    Line(c+new Vector2(-12,1),c+new Vector2(-12,-16),4,ink); Line(c+new Vector2(12,1),c+new Vector2(12,-16),4,ink);
                    Line(c+new Vector2(-12,-16),c+new Vector2(12,-16),4,ink);
                }
                else
                {
                    Quad(c+new Vector2(-17,14),c+new Vector2(17,14),c+new Vector2(17,-17),c+new Vector2(-17,-17),ink);
                    for(int row=0;row<2;row++) for(int col=0;col<3;col++)
                    {
                        Vector2 window=c+new Vector2(-12+col*10,8-row*12);
                        Quad(window,window+Vector2.right*5,window+new Vector2(5,-6),window+Vector2.down*6,new Color32(24,29,29,255));
                    }
                }
            }
            Ring(Point(.91f,.89f),22,1,new Color32(151,147,123,255));
            Line(Point(.91f,.93f),Point(.91f,.85f),2,new Color32(211,173,110,255));
            Line(Point(.87f,.89f),Point(.95f,.89f),2,new Color32(211,173,110,255));
        }
    }
}
