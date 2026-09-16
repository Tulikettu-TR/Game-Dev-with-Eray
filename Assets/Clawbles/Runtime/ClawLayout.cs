using System.Collections.Generic;
using UnityEngine;
namespace Clawbles
{
    public sealed class ClawLayout
    {
        public const float HalfWidth = 36, HalfDepth = 30, Ceiling = 14, HookMax = 6.8f, WallHeight = 8.5f, HoleHalf = 2.2f;
        public static readonly Vector3 Console = new Vector3(-14, ClawSimulation.Floor, -11);
        public static readonly Vector3 Delivery = new Vector3(29, 0, -24);
        public readonly int Seed;
        public readonly int RoomCount;
        public readonly List<Bounds> Rooms=new List<Bounds>();
        readonly List<Vector2> laserTravel=new List<Vector2>();
        public Vector3 RigSpawn=>RoomCount>0?new Vector3(Rooms[0].center.x,6,Rooms[0].center.z):new Vector3(0,6,12);
        public Vector3 CrewSpawn=>RoomCount>0?new Vector3(Rooms[0].center.x,ClawSimulation.Floor,Rooms[0].center.z):Console;
        public static readonly Vector3 PilotSeat=new Vector3(-48,ClawSimulation.Floor,0);
        public readonly List<Bounds> Obstacles = new List<Bounds>();
        readonly List<Bounds> previousLasers=new List<Bounds>();
        public readonly List<Bounds> Lasers = new List<Bounds>();
        public readonly List<Bounds> Floors = new List<Bounds>();
        public readonly int NoiseCount=0;
        public readonly List<ClawLooseToy> Toys=new List<ClawLooseToy>();
        public readonly float SouthGateX;
        public ClawLayout(int seed,int rooms=0)
        {
            Seed = seed;RoomCount=rooms<=0?0:rooms<=9?9:rooms<=16?16:25;
            if(RoomCount>0){BuildRooms();return;}
            SouthGateX=20+Mathf.Abs(seed%3);
            // Enclosed starting bay: only the window admits a mission load.
            Box(new Vector3(-24,WallHeight/2,18),new Vector3(.6f,WallHeight,16.6f));
            Box(new Vector3(-8,WallHeight/2,18),new Vector3(.6f,WallHeight,16.6f));
            Barrier(-24,-8,10,-16,6,1.7f,6.3f);
            Barrier(-24,-8,26,-16,1,0,2.1f);
            // This partition spans the entire cabinet. The narrow side door is for crew only.
            Barrier(-36,-29.5f,-12,-30,1,0,2.1f);
            Barrier(-29.5f,36,-12,SouthGateX,6,1,6.5f);
            // A side alcove beyond the gantry stop: the suspended load must swing sideways.
            Box(new Vector3(32.5f, 2, 15), new Vector3(7, 4, .6f));
            Box(new Vector3(32.5f, 11, 15), new Vector3(7, 6, .6f));
            Box(new Vector3(29.5f, 6, 15), new Vector3(1, 4, .6f));
            // Enclose the swing bay. Its back door admits a player, but is narrower than the duck.
            Box(new Vector3(29.3f,7,19.5f),new Vector3(.6f,14,9));
            Box(new Vector3(30.2f,7,24),new Vector3(2.4f,14,.6f));
            Box(new Vector3(34.3f,7,24),new Vector3(3.4f,14,.6f));
            Box(new Vector3(32,8.1f,24),new Vector3(1.2f,11.8f,.6f));
            Lasers.Add(new Bounds(new Vector3(-16,3.3f,10),new Vector3(6,.12f,.12f)));
            Lasers.Add(new Bounds(new Vector3(SouthGateX,2.4f,-12),new Vector3(6,.12f,.12f)));
            Lasers.Add(new Bounds(new Vector3(33,4.5f,15),new Vector3(6,.12f,.12f)));
            previousLasers.AddRange(Lasers);
            var d = Delivery; float h = HoleHalf;
            Box(d + new Vector3(-h-.15f,.25f,0),new Vector3(.3f,.5f,h*2+.6f));
            Box(d + new Vector3(h+.15f,.25f,0),new Vector3(.3f,.5f,h*2+.6f));
            Box(d + new Vector3(0,.25f,-h-.15f),new Vector3(h*2,.5f,.3f));
            Box(d + new Vector3(0,.25f,h+.15f),new Vector3(h*2,.5f,.3f));
            // Four slabs leave a real opening; the bottom is three metres below ground.
            Floor(-HalfWidth,d.x-h,-HalfDepth,HalfDepth);
            Floor(d.x+h,HalfWidth,-HalfDepth,HalfDepth);
            Floor(d.x-h,d.x+h,-HalfDepth,d.z-h);
            Floor(d.x-h,d.x+h,d.z+h,HalfDepth);
            Floors.Add(new Bounds(d+Vector3.down*3.15f,new Vector3(h*2,.3f,h*2)));
            ClawToyPlacement.Generate(this);
        }
        void BuildRooms()
        {
            int cells=Mathf.RoundToInt(Mathf.Sqrt(RoomCount));float w=72f/cells,d=60f/cells;
            for(int row=0;row<cells;row++)for(int col=0;col<cells;col++)
                Rooms.Add(new Bounds(new Vector3(-36+(col+.5f)*w,WallHeight/2,30-(row+.5f)*d),new Vector3(w,WallHeight,d)));
            int passage=0;
            for(int row=0;row<cells;row++)for(int col=0;col<cells;col++){
                var room=Rooms[row*cells+col];
                if(row<cells-1)RoomWall(room.center.x,room.min.z,w,false,passage++);
                if(col<cells-1)RoomWall(room.max.x,room.center.z,d,true,passage++);
            }
            // The enclosure is opaque: the operator only sees live room feeds.
            Box(new Vector3(-36,WallHeight/2,0),new Vector3(.3f,WallHeight,60));
            Box(new Vector3(36,WallHeight/2,0),new Vector3(.3f,WallHeight,60));
            Box(new Vector3(0,WallHeight/2,30),new Vector3(72,WallHeight,.3f));
            Box(new Vector3(0,WallHeight/2,-30),new Vector3(72,WallHeight,.3f));
            var p=Delivery;float h=HoleHalf;
            Box(p+new Vector3(-h-.15f,.25f,0),new Vector3(.3f,.5f,h*2+.6f));
            Box(p+new Vector3(h+.15f,.25f,0),new Vector3(.3f,.5f,h*2+.6f));
            Box(p+new Vector3(0,.25f,-h-.15f),new Vector3(h*2,.5f,.3f));
            Box(p+new Vector3(0,.25f,h+.15f),new Vector3(h*2,.5f,.3f));
            Floor(-36,p.x-h,-30,30);Floor(p.x+h,36,-30,30);
            Floor(p.x-h,p.x+h,-30,p.z-h);Floor(p.x-h,p.x+h,p.z+h,30);
            Floors.Add(new Bounds(p+Vector3.down*3.15f,new Vector3(h*2,.3f,h*2)));
            previousLasers.AddRange(Lasers);ClawToyPlacement.Generate(this);
        }
        void RoomWall(float x,float z,float length,bool rotated,int number)
        {
            float gap=Mathf.Min(5.5f,length*.55f),side=(length-gap)/2;
            Vector3 Position(float horizontal,float y)=>rotated?new Vector3(x,y,z+horizontal):new Vector3(x+horizontal,y,z);
            Vector3 Size(float width,float height)=>rotated?new Vector3(.4f,height,width):new Vector3(width,height,.4f);
            foreach(float sign in new[]{-1f,1f})Box(Position(sign*(gap/2+side/2),WallHeight/2),Size(side,WallHeight));
            Box(Position(0,.225f),Size(gap,.45f));
            Box(Position(0,(6.2f+WallHeight)/2),Size(gap,WallHeight-6.2f));
            if(number%2==0){Lasers.Add(new Bounds(Position(0,2.1f),rotated?new Vector3(.12f,.12f,gap):new Vector3(gap,.12f,.12f)));laserTravel.Add(new Vector2(.45f,6.2f));}
        }
        void Barrier(float left,float right,float z,float opening,float width,float bottom,float top)
        {
            float a=opening-width/2,b=opening+width/2;
            if(a>left)Box(new Vector3((left+a)/2,WallHeight/2,z),new Vector3(a-left,WallHeight,.6f));
            if(b<right)Box(new Vector3((right+b)/2,WallHeight/2,z),new Vector3(right-b,WallHeight,.6f));
            if(bottom>0)Box(new Vector3(opening,bottom/2,z),new Vector3(width,bottom,.6f));
            Box(new Vector3(opening,(top+WallHeight)/2,z),new Vector3(width,WallHeight-top,.6f));
        }
        void Floor(float x0,float x1,float z0,float z1) => Floors.Add(new Bounds(new Vector3((x0+x1)/2,-.15f,(z0+z1)/2),new Vector3(x1-x0,.3f,z1-z0)));
        void Box(Vector3 p,Vector3 size) => Obstacles.Add(new Bounds(p,size));
        void Window(float x,float z,float width,float bottom,float opening)
        {
            Box(new Vector3(x-width/2-.4f,7,z),new Vector3(.8f,14,.6f));
            Box(new Vector3(x+width/2+.4f,7,z),new Vector3(.8f,14,.6f));
            Box(new Vector3(x,bottom/2,z),new Vector3(width,bottom,.6f));
            float top=bottom+opening;
            Box(new Vector3(x,(top+14)/2,z),new Vector3(width,14-top,.6f));
        }
        public float Noise(float x,float z) => Mathf.PerlinNoise(x*.075f+(Seed&1023)*.37f+100,z*.075f+((Seed>>10)&1023)*.41f+100);
        static float Hash01(int x,int z,int seed) { unchecked { uint h=(uint)(x*73856093^z*19349663^seed);h^=h>>13;h*=1274126177;return(h&65535)/65535f; } }
        public Vector3 PrizeSpawn(int round) => RoomCount>0?Rooms[Mathf.Abs(round)%Mathf.RoundToInt(Mathf.Sqrt(RoomCount))].center+new Vector3(0,-WallHeight/2+.85f,2):round % 2 == 0 ? new Vector3(-16,.85f,21) : new Vector3(32.4f,.85f,18);
        public bool InsideHole(Vector3 p,Vector3 ext) => Mathf.Abs(p.x-Delivery.x)+ext.x < HoleHalf && Mathf.Abs(p.z-Delivery.z)+ext.z < HoleHalf;
        public Vector2 LaserTravel(int index)=>RoomCount>0?laserTravel[index]:index==0?new Vector2(1.7f,6.3f):index==1?new Vector2(1,6.5f):new Vector2(4,8);
        public void UpdateLasers(int stage,float clock,bool reset=false)
        {
            for(int i=0;i<Lasers.Count;i++){
                previousLasers[i]=Lasers[i];var b=Lasers[i];var center=b.center;
                if(RoomCount>0){var span=LaserTravel(i);center.y=stage==0?2.1f:Mathf.Lerp(span.x+1f,span.y-1.5f,(1-Mathf.Cos(clock*(stage==1?.45f:.75f)+i*.8f))*.5f);}
                else if(stage==0)center.y=i==0?3.3f:i==1?2.4f:4.5f;
                else{
                    float phase=clock*(stage==1?.45f:.75f)+i*.8f;
                    float middle=i==0?(stage==1?3.3f:3.5f):i==1?(stage==1?2.8f:3):stage==1?5.25f:5.45f;
                    float amplitude=i==0?(stage==1?.65f:1.15f):i==1?(stage==1?.65f:1.25f):stage==1?.8f:1f;
                    center.y=middle-Mathf.Cos(phase)*amplitude;
                }
                b.center=center;Lasers[i]=b;if(reset)previousLasers[i]=b;
            }
        }
        public bool LaserHit(Vector3 from,Vector3 to,Vector3 ext)
        {
            for(int i=0;i<Lasers.Count;i++){
                var start=from-previousLasers[i].center;var end=to-Lasers[i].center;
                var expanded=new Bounds(Vector3.zero,Lasers[i].size+ext*2);
                var delta=end-start;
                if(expanded.Contains(start)||expanded.Contains(end)||(delta.sqrMagnitude>.000001f&&expanded.IntersectRay(new Ray(start,delta.normalized),out float distance)&&distance<=delta.magnitude))return true;
            }
            return false;
        }
        public bool OverlapsStructure(Vector3 center,Vector3 halfSize)
        {var b=new Bounds(center,halfSize*2);foreach(var o in Obstacles)if(b.Intersects(o))return true;return false;}
        public bool Overlaps(Vector3 center,Vector3 halfSize,int ignoreToy=-1)
        {
            if(OverlapsStructure(center,halfSize))return true; if(ignoreToy==-2)return false;
            var b=new Bounds(center,halfSize*2);
            foreach(var toy in Toys)if(toy.Id!=ignoreToy && !toy.Attached && b.Intersects(toy.Bounds))return true;
            return false;
        }
        public Vector3 Slide(Vector3 from,Vector3 target,Vector3 ext,int ignoreToy=-1)
        {
            // Small swept increments prevent tunnelling; blocked axes do not cancel free axes.
            int steps=Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(from,target)/.12f),1,128);
            Vector3 delta=(target-from)/steps,p=from;
            for(int i=0;i<steps;i++)for(int axis=0;axis<3;axis++){
                var next=p;next[axis]+=delta[axis];
                if(!Overlaps(next,ext,ignoreToy))p=next;
            }
            return p;
        }
        public Vector3 FreePlayerPosition(Vector3 origin)
        {
            var ext=new Vector3(.3f,.64f,.3f);
            bool Valid(Vector3 p)=>p.y>=ClawSimulation.Floor && p.y<Ceiling-.7f &&
                Mathf.Abs(p.x)<HalfWidth-.5f && Mathf.Abs(p.z)<HalfDepth-.5f &&
                !InsideHole(p,ext) && !Overlaps(p,ext);
            if(Valid(origin))return origin;
            for(float r=.25f;r<=4;r+=.25f)
                foreach(var direction in new[]{Vector3.up,Vector3.right,Vector3.left,Vector3.forward,Vector3.back}){
                    var candidate=origin+direction*r;if(Valid(candidate))return candidate;
                }
            return CrewSpawn+Vector3.right;
        }
        public float LandingHeight(Vector3 previous,Vector3 next,Vector3 halfSize,int ignoreToy=-1)
        {
            float floor=halfSize.y-(InsideHole(next,halfSize)?3:0);
            foreach(var b in Obstacles)
                if(Mathf.Abs(next.x-b.center.x)<halfSize.x+b.extents.x&&Mathf.Abs(next.z-b.center.z)<halfSize.z+b.extents.z&&previous.y-halfSize.y>=b.max.y-.02f&&next.y-halfSize.y<=b.max.y)
                    floor=Mathf.Max(floor,b.max.y+halfSize.y+.01f);
            foreach(var toy in Toys)if(toy.Id!=ignoreToy){
                var b=toy.Bounds;
                if(Mathf.Abs(next.x-b.center.x)<halfSize.x+b.extents.x&&Mathf.Abs(next.z-b.center.z)<halfSize.z+b.extents.z&&previous.y-halfSize.y>=b.max.y-.02f&&next.y-halfSize.y<=b.max.y)
                    floor=Mathf.Max(floor,b.max.y+halfSize.y+.01f);
            }
            return floor;
        }
    }
}









