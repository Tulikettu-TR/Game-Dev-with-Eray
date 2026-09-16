using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Clawbles
{
    public sealed class ClawLooseToy
    {
        public int Id,Kind, Pole=1;
        public Vector3 Root,OriginalCenter,Velocity;
        public Quaternion Rotation;
        public float Scale;
        public Bounds Bounds;
        public bool Moving,Changed,Attached,PhysicsManaged;
        public Vector3 Target=>Bounds.center+Vector3.up*(Bounds.extents.y+.15f);
        public void SetCenter(Vector3 center){Root+=center-Bounds.center;Bounds=new Bounds(center,Bounds.size);Changed=true;}
        public void Reset(){SetCenter(OriginalCenter);Velocity=Vector3.zero;Moving=false;}
    }
    public static class ClawToyPlacement
    {
        static Vector3[][] points;
        public static void Generate(ClawLayout map)
        {
            if(points==null){
                points=new Vector3[3][];
                for(int i=0;i<3;i++){points[i]=ClawPlushMesh.ShapePoints(i);}
            }
            var random=new System.Random(map.Seed);float R()=>(float)random.NextDouble();
            var centers=new List<Vector2>(new[]{new Vector2(-29,21),new Vector2(-29,-2),new Vector2(-4,23),new Vector2(13,23),new Vector2(28,1),new Vector2(-20,-23),new Vector2(0,-24),new Vector2(11,-3),new Vector2(-16,20)});
            if(map.RoomCount>0){centers.Clear();foreach(var room in map.Rooms)centers.Add(new Vector2(room.center.x,room.center.z));}
            foreach(var center in centers){
                int accepted=0;
                for(int attempt=0;attempt<330 && accepted<(map.RoomCount>0?Mathf.Max(6,180/map.RoomCount):22);attempt++){
                    float a=R()*Mathf.PI*2,r=Mathf.Sqrt(R())*5;
                    var root=new Vector3(center.x+Mathf.Cos(a)*r,0,center.y+Mathf.Sin(a)*r);
                    if(Mathf.Abs(root.x)>33||Mathf.Abs(root.z)>27||Mathf.Abs(root.x)<2 ||
                        Vector3.Distance(root,map.CrewSpawn)<3.5f || Vector3.Distance(root,ClawLayout.Delivery)<4.5f)continue;
                    bool spawn=false;for(int i=0;i<2;i++){var p=map.PrizeSpawn(i);p.y=0;if(Vector3.Distance(root,p)<2.6f)spawn=true;}if(spawn)continue;
                    int kind=random.Next(3);float scale=.65f+R()*.38f;
                    var q=Quaternion.Euler(kind==2?(R()-.5f)*35:65+R()*55,R()*360,(R()-.5f)*65);
                    var bound=new Bounds(q*(points[kind][0]*scale),Vector3.zero);
                    foreach(var vertex in points[kind])bound.Encapsulate(q*(vertex*scale));
                    root.y=-bound.min.y+.002f;var world=new Bounds(root+bound.center,bound.size);
                    // Every visible mesh rests directly on the floor. AABB stacking
                    // leaves air gaps under curved surfaces and unsupported toys after pickup.
                    bool reserved=false;for(int bay=0;bay<2;bay++)if(world.Intersects(new Bounds(map.PrizeSpawn(bay),new Vector3(6.8f,5,5.5f))))reserved=true;
                    if(reserved)continue;
                    bool occupied=false;
                    foreach(var other in map.Toys)if(world.Intersects(other.Bounds)){occupied=true;break;}
                    if(occupied)continue;
                    if(map.OverlapsStructure(world.center,world.extents+Vector3.one*.15f))continue;
                    var toy=new ClawLooseToy{Id=map.Toys.Count,Kind=kind,Pole=map.Toys.Count%2==0?1:-1,Root=root,Rotation=q,Scale=scale,Bounds=world,OriginalCenter=world.center};
                    map.Toys.Add(toy);accepted++;
                }
            }
        }
    }
    public sealed class ClawToyField:MonoBehaviour
    {
        ClawLayout map;Mesh[] meshes;Material[] materials;
        public ClawLayout BoundLayout=>map;
        public Vector3 PresentedCenter(int id)=>colliders[id].position;
        readonly Matrix4x4[][] matrices={new Matrix4x4[1023],new Matrix4x4[1023],new Matrix4x4[1023]};
        readonly int[] counts=new int[3];
        Transform[] colliders;
        public void Build(ClawLayout layout,Mesh[] geometry,Material[] palette)
        {
            map=layout;meshes=geometry;materials=palette;foreach(var material in palette)material.enableInstancing=true;
            colliders=new Transform[map.Toys.Count];
            foreach(var toy in map.Toys){
                var go=new GameObject("Touchable plush "+toy.Id);go.transform.SetParent(transform,false);
                go.transform.position=toy.Bounds.center;go.AddComponent<BoxCollider>().size=toy.Bounds.size;colliders[toy.Id]=go.transform;
            }
        }
        void LateUpdate(){Render();}
        public void Render()
        {
            if(map==null)return;
            for(int i=0;i<3;i++)counts[i]=0;
            foreach(var toy in map.Toys){
                colliders[toy.Id].position=toy.Bounds.center;
                matrices[toy.Kind][counts[toy.Kind]++]=Matrix4x4.TRS(toy.Root,toy.Rotation,Vector3.one*toy.Scale);
            }
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            for(int kind=0;kind<3;kind++)for(int sub=0;sub<3;sub++){
                int material=kind*3+sub;
                if(counts[kind]==0)continue;
                if(SystemInfo.supportsInstancing)
                    Graphics.DrawMeshInstanced(meshes[kind],sub,materials[material],matrices[kind],counts[kind],null,ShadowCastingMode.Off,false,0,null,LightProbeUsage.Off);
                else for(int i=0;i<counts[kind];i++)Graphics.DrawMesh(meshes[kind],matrices[kind][i],materials[material],0,null,sub,null,false,false,false);
            }
        }
    }
}








