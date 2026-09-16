using UnityEngine;
namespace Clawbles
{
    public static class ClawSurfaceArt
    {
        public static Texture2D Texture(bool wall)
        {
            const int n=256;var image=new Texture2D(n,n,TextureFormat.RGB24,true);
            image.name=wall?"Printed cream and mint enamel":"Soft terrazzo play floor";
            var pixels=new Color[n*n];var random=new System.Random(wall?84:29);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){
                float u=x/(float)n,v=y/(float)n;
                Color color;
                if(wall){
                    float wave=.25f+.035f*Mathf.Sin(u*Mathf.PI*4);
                    color=v<wave?new Color(.35f,.64f,.61f):new Color(.94f,.88f,.75f);
                    if(Mathf.Abs(v-wave)<.009f)color=new Color(.9f,.66f,.32f);
                    if(x<2||x>n-3)color*=.88f;
                    float dx=u-.5f,dy=v-.66f;
                    float diamond=Mathf.Abs(dx)*1.2f+Mathf.Abs(dy);
                    if(diamond<.052f)color=new Color(.82f,.57f,.28f);
                    if(Mathf.Abs(dx)<.008f&&Mathf.Abs(dy)<.08f)color=new Color(.82f,.57f,.28f);
                    color*=.985f+(float)random.NextDouble()*.025f;
                }else{
                    color=new Color(.69f,.78f,.75f);
                    // Fine terrazzo flecks and understated tile joints, replacing the checkerboard.
                    float grit=(float)random.NextDouble();
                    color*=.98f+grit*.04f;
                    if(x<2||y<2)color=new Color(.52f,.63f,.61f);
                    int cellX=x/16,cellY=y/16;
                    float cx=cellX*16+4+(cellX*17+cellY*7)%9,cy=cellY*16+4+(cellX*3+cellY*13)%9;
                    if(Mathf.Abs(x-cx)+Mathf.Abs(y-cy)*1.4f<2.5f)
                        color=(cellX+cellY)%3==0?new Color(.86f,.81f,.68f):new Color(.57f,.69f,.67f);
                }
                pixels[y*n+x]=color;
            }
            image.SetPixels(pixels);image.Apply(true);image.wrapMode=TextureWrapMode.Repeat;
            image.filterMode=FilterMode.Trilinear;image.anisoLevel=4;return image;
        }
        public static Mesh WorldUV(Mesh source,Vector3 position,Vector3 size,float tile)
        {
            var mesh=Object.Instantiate(source);mesh.name="World aligned surface UV";
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
            for(int i=0;i<vertices.Length;i++){
                var p=position+Vector3.Scale(vertices[i],size);var n=normals[i];
                uv[i]=Mathf.Abs(n.y)>.5f?new Vector2(p.x,p.z)/tile:
                    Mathf.Abs(n.x)>.5f?new Vector2(p.z,p.y)/tile:new Vector2(p.x,p.y)/tile;
            }
            mesh.uv=uv;return mesh;
        }
    }
}
