using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Clawbles
{
    // Low polygon sewn toys. All surfaces are real mesh geometry; no per-toy physics or Update.
    public static class ClawPlushMesh
    {
        public static Mesh Create(int kind, int detail = 10) => Build(kind, detail, true, out _);
        public static Vector3[] ShapePoints(int kind) { Build(kind,10,false,out var points);return points; }
        static Mesh Build(int kind,int detail,bool createMesh,out Vector3[] points)
        {
            var v=new List<Vector3>(); var uv=new List<Vector2>(); var indices=new[]{new List<int>(),new List<int>(),new List<int>()};
            void Ball(Vector3 p,Vector3 size,int material)
            {
                int sides=Mathf.Clamp(detail,8,24),rings=Mathf.Max(6,sides*2/3); int start=v.Count;
                for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++){
                    float a=x*Mathf.PI*2/sides,b=y*Mathf.PI/rings;
                    v.Add(p+Vector3.Scale(new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a)),size*.5f));
                    uv.Add(new Vector2(x/(float)sides,y/(float)rings));
                }
                for(int y=0;y<rings;y++)for(int x=0;x<sides;x++){
                    int a=start+y*(sides+1)+x,b=a+sides+1;
                    indices[material].AddRange(new[]{a,a+1,b,a+1,b+1,b});
                }
            }
            if(kind==2){
                Ball(new Vector3(0,.55f,0),new Vector3(2.6f,1.2f,1.3f),0);
                Ball(new Vector3(.7f,1.1f,0),Vector3.one*.85f,0);
                Ball(new Vector3(1.13f,.94f,0),new Vector3(.6f,.21f,.58f),1);
                foreach(float side in new[]{-1f,1f}){
                    Ball(new Vector3(.82f,1.25f,side*.365f),Vector3.one*.12f,2);
                    Ball(new Vector3(-.1f,.62f,side*.58f),new Vector3(1,.55f,.2f),0);
                    Ball(new Vector3(.3f,.07f,side*.36f),new Vector3(.6f,.12f,.36f),1);
                }
            }else{
                Ball(new Vector3(0,.58f,0),new Vector3(.96f,1.05f,.72f),0);
                Ball(new Vector3(0,1.22f,.02f),new Vector3(1.02f,.85f,.8f),0);
                Ball(new Vector3(0,.59f,.31f),new Vector3(.64f,.69f,.13f),1);
                Ball(new Vector3(0,1.06f,.38f),new Vector3(.55f,.32f,.18f),1);
                Ball(new Vector3(0,1.13f,.48f),new Vector3(.19f,.12f,.1f),2);
                foreach(float side in new[]{-1f,1f}){
                    Ball(new Vector3(side*.38f,kind==1?1.85f:1.57f,0),kind==1?new Vector3(.29f,.88f,.26f):Vector3.one*.4f,0);
                    Ball(new Vector3(side*.38f,kind==1?1.85f:1.57f,.12f),kind==1?new Vector3(.13f,.59f,.04f):new Vector3(.22f,.22f,.05f),1);
                    Ball(new Vector3(side*.51f,.58f,0),new Vector3(.35f,.7f,.4f),0);
                    Ball(new Vector3(side*.3f,.15f,.17f),new Vector3(.42f,.32f,.55f),0);
                    Ball(new Vector3(side*.24f,1.29f,.38f),Vector3.one*.115f,2);
                    Ball(new Vector3(side*.25f,1.31f,.43f),Vector3.one*.025f,1);
                }
                for(int i=0;i<5;i++)Ball(new Vector3(0,.39f+i*.085f,.381f),new Vector3(.055f,.012f,.018f),2);
            }
            points=v.ToArray();if(!createMesh)return null;
            var m=new Mesh{name=ClawFigureStyle.Name(kind),indexFormat=IndexFormat.UInt32};
            m.SetVertices(v);m.SetUVs(0,uv);m.subMeshCount=3;
            for(int i=0;i<3;i++)m.SetTriangles(indices[i],i);
            m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        public static int Scatter(Transform parent,ClawLayout map,Mesh[] toys,Material[] materials,List<Mesh> owned)
        {
            var field=new GameObject("Touchable plush field").AddComponent<ClawToyField>();
            field.transform.SetParent(parent,false);field.Build(map,toys,materials);
            return map.Toys.Count;
        }
    }
}




