using UnityEngine;
using UnityEngine.UI;

namespace VanishingDays
{
    // A charcoal silhouette that stands in for a character portrait until the drawn art exists.
    // Each person gets a slightly different head, hair and shoulder line so speakers read apart.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PortraitGraphic : MaskableGraphic
    {
        protected override void Awake()
        {
            useLegacyMeshGeneration=false;
            base.Awake();
        }
        public string PortraitId="stranger";
        public Color32 Figure=new Color32(11,17,19,255), Hair=new Color32(26,36,39,255), Line=new Color32(110,119,114,255);

        VertexHelper mesh;
        // Coordinates are fractions of the rect height so the figure scales with the panel; x is centred.
        Vector2 P(float x,float y) { var r=rectTransform.rect; return new Vector2(r.center.x+x*r.height,r.yMin+y*r.height); }
        // The graphic's color (alpha) dims the whole figure when the person is only listening.
        Color32 Tint(Color32 c) => (Color)c*color;
        void Tri(Vector2 a,Vector2 b,Vector2 c,Color32 tint)
        {
            tint=Tint(tint);
            int n=mesh.currentVertCount; mesh.AddVert(a,tint,Vector2.zero); mesh.AddVert(b,tint,Vector2.zero); mesh.AddVert(c,tint,Vector2.zero); mesh.AddTriangle(n,n+1,n+2);
        }
        // Part of an ellipse as a fan; angles in degrees, 0 = right, counter-clockwise.
        void Ellipse(float cx,float cy,float rx,float ry,float from,float to,Color32 tint,int steps=28)
        {
            var centre=P(cx,cy);
            for(int i=0;i<steps;i++)
            {
                float a=Mathf.Deg2Rad*Mathf.Lerp(from,to,i/(float)steps), b=Mathf.Deg2Rad*Mathf.Lerp(from,to,(i+1)/(float)steps);
                Tri(centre,P(cx+Mathf.Cos(a)*rx,cy+Mathf.Sin(a)*ry),P(cx+Mathf.Cos(b)*rx,cy+Mathf.Sin(b)*ry),tint);
            }
        }
        void Bar(float x0,float y0,float x1,float y1,float width,Color32 tint)
        {
            tint=Tint(tint);
            // width is a fraction of the rect height, like every other coordinate here.
            Vector2 a=P(x0,y0), b=P(x1,y1), n=Vector2.Perpendicular((b-a).normalized)*width*.5f*rectTransform.rect.height;
            int i=mesh.currentVertCount; mesh.AddVert(a-n,tint,Vector2.zero); mesh.AddVert(a+n,tint,Vector2.zero); mesh.AddVert(b+n,tint,Vector2.zero); mesh.AddVert(b-n,tint,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); mesh=vh;
            if(string.IsNullOrEmpty(PortraitId)) return;
            // Proportions per person: shoulder half-width, head radius, and a hair style.
            float shoulders=.42f, head=.17f; int hair=0;
            switch(PortraitId)
            {
                case "donglim": shoulders=.5f; head=.18f; hair=0; break;   // broad, cropped
                case "jiwoo": shoulders=.38f; head=.165f; hair=1; break;    // narrow, hair to the jaw
                case "seokhyeon": shoulders=.44f; head=.17f; hair=2; break; // hood up
                case "neighbor": shoulders=.4f; head=.16f; hair=3; break;   // stooped, thin hair
                case "yeongsuk": shoulders=.4f; head=.16f; hair=1; break;
            }
            float stoop=hair==3?-.04f:0, neck=.34f+stoop, headY=.55f+stoop;
            // Shoulders: a half ellipse rising from the bottom edge.
            Ellipse(0,0,shoulders,neck+.04f,0,180,Figure,36);
            Bar(0,neck-.02f,0,headY-head*.6f,head*.9f,Figure);
            if(hair==2) Ellipse(0,headY,head*1.45f,head*1.45f,0,360,Hair);
            Ellipse(0,headY,head*.88f,head*1.08f,0,360,Figure);
            if(hair==0) Ellipse(0,headY+head*.35f,head*.9f,head*.78f,10,170,Hair);
            else if(hair==1) { Ellipse(0,headY+head*.2f,head*1.02f,head*.96f,0,180,Hair); Bar(-head*.95f,headY+head*.25f,-head*.92f,headY-head*.75f,head*.28f,Hair); Bar(head*.95f,headY+head*.25f,head*.92f,headY-head*.75f,head*.28f,Hair); }
            else if(hair==3) Ellipse(0,headY+head*.55f,head*.7f,head*.5f,20,160,Hair);
            // A dashed jaw line, the only light stroke on the figure.
            for(int i=0;i<6;i++)
            {
                float a=Mathf.Lerp(200,340,i/6f)*Mathf.Deg2Rad, b=Mathf.Lerp(200,340,(i+.5f)/6f)*Mathf.Deg2Rad;
                Bar(Mathf.Cos(a)*head*.8f,headY+Mathf.Sin(a)*head,Mathf.Cos(b)*head*.8f,headY+Mathf.Sin(b)*head,2.2f/Mathf.Max(1,rectTransform.rect.height),Line);
            }
        }
    }
}
