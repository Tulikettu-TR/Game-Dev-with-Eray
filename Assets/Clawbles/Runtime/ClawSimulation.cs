using System.Collections.Generic;
using UnityEngine;
namespace Clawbles
{
    public enum ClawTool { Scoop, Magnet, Hook, Claw }
    public sealed class ClawSimulation
    {
        public const ulong Nobody = ulong.MaxValue;
        public const float Floor = 0.65f, KnockDuration = 2.4f;
        public static Vector3 ConsolePosition => ClawLayout.Console;
        public static Vector3 Chute => ClawLayout.Delivery;
        public sealed class Player
        {
            public ulong Id; public int Slot; public Vector3 Position;
            public float VerticalSpeed, Yaw, KnockTimer, Invulnerable, SignalTimer;
            public Vector3 KnockVelocity;
            public int Attachment, Signal;
            public Vector2 Move;
            public float Lift, Turn, InputTime;
            public int LastAction;
            public bool Jump, Interact, Leave, Securing, Recover, TogglePole;
            public float RecoveryCooldown, GroundGrace, JumpBuffer;
        }
        public readonly ClawShift Shift = new ClawShift();
        public readonly Dictionary<ulong, Player> Players = new Dictionary<ulong, Player>();
        public ClawLayout Map { get; private set; }
        public Vector3 Claw = new Vector3(0, 6, 12), Anchor = new Vector3(0, 6, 12), Prize, PrizeVelocity;
        public float PrizeYaw, GripQuality;
        public bool Gripped;
        public int HeldToy=-1;
        public int MagnetPole=-1,PrizePole=1;
        public readonly List<int> MagneticToys=new List<int>();
        public readonly List<Vector3> MagneticOffsets=new List<Vector3>();
        public ClawTool Tool=>Map.RoomCount==0?ClawTool.Hook:Shift.Stage==0?ClawTool.Claw:Shift.Stage==1?ClawTool.Magnet:Shift.Stage==2?ClawTool.Hook:ClawTool.Scoop;
        ClawThreadPhysics threadPhysics; public Quaternion PrizeRotation=Quaternion.identity;
        public bool PhysicalHook=>Map.RoomCount>0&&Tool==ClawTool.Hook;
        public bool PhysicalCargo=>PhysicalHook||Tool==ClawTool.Scoop;
        public Quaternion RigRotation=>Quaternion.FromToRotation(Vector3.up,new Vector3(Anchor.x,ClawLayout.Ceiling,Anchor.z)-Claw);
        public void DisposePhysics(){threadPhysics?.Dispose();threadPhysics=null;}
        public float MissionOffset=>Map.RoomCount>0?CurrentExtents(0).y+.55f:.8f;
        public bool HasLoad=>Gripped||HeldToy>=0||MagneticToys.Count>0;
        Vector3 HeldExtents=>HeldToy>=0?Map.Toys[HeldToy].Bounds.extents:CurrentExtents(PrizeYaw);
        float HoldOffset=>HeldToy>=0?HeldExtents.y+.15f:MissionOffset;
        public ulong Operator = Nobody;
        public int Score, KnockEvents, SlipEvents, LaserResets;
        public float ResetTimer, LaserClock;
        public string Notice = "Find the marked figure. Guide the pilot. Drop inside the mint delivery hole.";
        Vector3 clawVelocity, swing, swingVelocity;
        public ClawSimulation(int seed = 7241,int rooms=0) { Map = new ClawLayout(seed,rooms); Prize = Map.PrizeSpawn(0);Claw=Anchor=Map.RigSpawn; }
        public void ReleaseForShift(){DisposePhysics();PrizeRotation=Quaternion.identity;
            foreach(var id in MagneticToys){Map.Toys[id].Moving=true;Map.Toys[id].Attached=false;}MagneticToys.Clear();MagneticOffsets.Clear();Gripped=false;HeldToy=-1;GripQuality=0;Operator=Nobody;
            foreach(var p in Players.Values){p.Attachment=0;p.Move=Vector2.zero;p.Lift=0;p.Securing=false;p.Jump=p.Interact=p.Leave=p.Recover=false;}
        }
        public void ResetRig()
        {
            LaserClock=0;Map.UpdateLasers(Shift.Stage,0,true);ReleaseForShift();Claw=Anchor=Map.RigSpawn;swing=swingVelocity=clawVelocity=Vector3.zero;
            Prize=Map.PrizeSpawn(0);Prize.y=CurrentExtents(0).y;PrizeVelocity=Vector3.zero;PrizeYaw=0;ResetTimer=0;
            foreach(var p in Players.Values){p.Position=Map.CrewSpawn+Vector3.right*(.5f+p.Slot*1.25f);p.KnockTimer=0;p.VerticalSpeed=0;p.Invulnerable=1;}
            Notice="Find the marked load. Helpers secure it; the operator reads crew signals.";
        }
        public void SetSeed(int seed,int rooms=-1) {if(rooms<0)rooms=Map.RoomCount;if(Map.Seed!=seed||Map.RoomCount!=rooms)Map=new ClawLayout(seed,rooms);}
        public Player Add(ulong id)
        {
            if (Players.TryGetValue(id, out var existing)) return existing;
            int slot = 0; while (slot < 4 && HasSlot(slot)) slot++;
            if (slot == 4) return null;
            var p = new Player { Id = id, Slot = slot, Position = Map.RoomCount>0?Map.CrewSpawn+Vector3.right*(slot*1.1f):new Vector3(-13.5f + slot * 1.25f, Floor, -11) };
            Players.Add(id, p); return p;
        }
        bool HasSlot(int slot) { foreach (var p in Players.Values) if (p.Slot == slot) return true; return false; }
        public void Remove(ulong id) { Players.Remove(id); if (Operator == id){Operator = Nobody;if(Map.RoomCount>0)AssignRandomPilot();} }
        public void Input(ulong id, Vector2 move, float lift, int action, int buttons, float now, float yaw = 0, float turn = 0, bool securing = false)
        {
            if (!Players.TryGetValue(id, out var p)) return;
            if (!float.IsFinite(move.x) || !float.IsFinite(move.y) || !float.IsFinite(lift) || !float.IsFinite(yaw) || !float.IsFinite(turn)) return;
            p.Move = Vector2.ClampMagnitude(move, 1); p.Lift = Mathf.Clamp(lift, -1, 1); p.Turn = Mathf.Clamp(turn, -1, 1);
            p.Yaw = Mathf.Repeat(yaw, 360); p.InputTime = now; p.Securing = securing;
            if (action > p.LastAction)
            {
                p.LastAction = action; p.Jump |= (buttons & 1) != 0; p.Interact |= (buttons & 2) != 0; p.Leave |= (buttons & 4) != 0; p.Recover |= (buttons & 8) != 0;p.TogglePole |= (buttons & 16)!=0;
            }
        }
        public void Step(float dt, float now)
        {
            if(Shift.Blocking)return;
            Shift.Tick(this,dt);if(Shift.Blocking)return;
            LaserClock+=dt;Map.UpdateLasers(Shift.Stage,LaserClock);
            foreach (var p in Players.Values)
            {
                if(Map.RoomCount>0&&p.Id==Operator){p.Position=ClawLayout.PilotSeat;p.Attachment=0;p.KnockTimer=0;}
                if(p.TogglePole){p.TogglePole=false;ChangePole(p);}
                p.RecoveryCooldown=Mathf.Max(0,p.RecoveryCooldown-dt);
                if(p.Recover){p.Recover=false;RecoverPlayer(p);}
                if(p.Id!=Operator && p.Attachment==0 && Map.Overlaps(p.Position,new Vector3(.3f,.64f,.3f))) {p.Position=Map.FreePlayerPosition(p.Position);p.VerticalSpeed=0;}
                p.SignalTimer = Mathf.Max(0,p.SignalTimer-dt);
                p.Invulnerable = Mathf.Max(0, p.Invulnerable - dt);
                if (now - p.InputTime > 0.4f) { p.Move = Vector2.zero; p.Lift = p.Turn = 0; p.Securing = false; }
                if (p.KnockTimer > 0)
                {
                    p.KnockTimer = Mathf.Max(0, p.KnockTimer - dt);
                    MovePlayer(p, new Vector2(p.KnockVelocity.x, p.KnockVelocity.z), dt);
                    p.KnockVelocity = Vector3.MoveTowards(p.KnockVelocity, Vector3.zero, 6 * dt);
                    p.Jump = p.Interact = p.Leave = false;
                    if (p.KnockTimer == 0) { p.Invulnerable = 1; Notice = "Back on your feet!"; }
                    continue;
                }
                if (p.Leave) { if (Operator == p.Id && Map.RoomCount==0) Operator = Nobody; p.Attachment = 0; }
                if (p.Interact) Interact(p);
                if(p.Jump)p.JumpBuffer=.12f;else p.JumpBuffer=Mathf.Max(0,p.JumpBuffer-dt);
                float support=Map.LandingHeight(p.Position+Vector3.up*.03f,p.Position-Vector3.up*.08f,new Vector3(.28f,Floor,.28f));
                bool grounded=p.VerticalSpeed<=0 && Mathf.Abs(p.Position.y-support)<.09f;
                p.GroundGrace=grounded?.1f:Mathf.Max(0,p.GroundGrace-dt);
                if(p.JumpBuffer>0 && p.Id!=Operator && (p.GroundGrace>0||p.Attachment!=0)){
                    p.Attachment=0;p.VerticalSpeed=5.4f;p.JumpBuffer=p.GroundGrace=0;
                }
                if (p.Id != Operator && p.Attachment == 0) MovePlayer(p, p.Move * 4.5f, dt);
                if (p.Attachment == 1)
                {
                    float nextYaw = PrizeYaw + p.Turn * 70 * dt;
                    if (CanPrizeOccupy(Prize, nextYaw)) PrizeYaw = nextYaw;
                    if (Gripped && p.Securing) GripQuality = Mathf.Clamp01(GripQuality + dt * Shift.SecureSpeed);
                }
                p.Jump = p.Interact = p.Leave = false;
            }
            var priorPrize = Prize;
            var priorClaw = Claw;
            var priorAnchor = Anchor;
            if (Players.TryGetValue(Operator, out var pilot) && pilot.KnockTimer <= 0)
            {
                Anchor += new Vector3(pilot.Move.x, pilot.Lift, pilot.Move.y) * (Shift.CraneSpeed * dt);
                Anchor.x = Mathf.Clamp(Anchor.x, -30, 30);
                Anchor.z = Mathf.Clamp(Anchor.z, -27, 27);
                Anchor.y = Mathf.Clamp(Anchor.y, Tool==ClawTool.Scoop?1.64f:1.65f, ClawLayout.HookMax);
            }
            var translation = Anchor - priorAnchor; translation.y = 0;
            swing -= translation;
            swingVelocity += (-swing * 4 - swingVelocity * .65f) * dt;
            if(Shift.Phase==ShiftPhase.Active && Shift.Stage==2) swingVelocity += new Vector3(Mathf.Sin(now*1.4f),0,Mathf.Cos(now*.8f))*(dt*.7f);
            swing += swingVelocity * dt;
            swing = Vector3.ClampMagnitude(swing, 4.5f);
            float ropeLength = Mathf.Max(2, ClawLayout.Ceiling - Anchor.y);
            var candidate = Anchor + swing + Vector3.up * Mathf.Min(.8f, swing.sqrMagnitude / (2 * ropeLength));
            candidate.x = Mathf.Clamp(candidate.x, -ClawLayout.HalfWidth + 1.4f, ClawLayout.HalfWidth - 1.4f);
            candidate.z = Mathf.Clamp(candidate.z, -ClawLayout.HalfDepth + 1.4f, ClawLayout.HalfDepth - 1.4f);
            candidate.y=Mathf.Min(candidate.y,ClawLayout.HookMax);
            var hookExtent=HasLoad?Vector3.Max(Vector3.one*.45f,HeldExtents):Vector3.one*.45f;
            var offset=Vector3.zero;
            if(HasLoad){
                float bottom=-HoldOffset-HeldExtents.y,top=Mathf.Max(.45f,HeldExtents.y-HoldOffset);
                offset.y=(bottom+top)*.5f;hookExtent.y=(top-bottom)*.5f;
                candidate.y=Mathf.Max(candidate.y,HeldExtents.y+HoldOffset);
            }
            if(MagneticToys.Count>0){
                var group=new Bounds(offset,hookExtent*2);
                for(int i=0;i<MagneticToys.Count;i++){var toy=Map.Toys[MagneticToys[i]];group.Encapsulate(MagneticOffsets[i]+toy.Bounds.extents);group.Encapsulate(MagneticOffsets[i]-toy.Bounds.extents);}
                offset=group.center;hookExtent=group.extents;
            }
            if(Tool==ClawTool.Scoop){
                var r=Quaternion.FromToRotation(Vector3.up,new Vector3(Anchor.x,ClawLayout.Ceiling,Anchor.z)-candidate);var box=new Bounds(new Vector3(0,-.495f,-.375f),new Vector3(3.4f,2.29f,3.45f));offset=r*box.center;var x=r*Vector3.right*box.extents.x;var y=r*Vector3.up*box.extents.y;var z=r*Vector3.forward*box.extents.z;hookExtent=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));candidate.y=Mathf.Max(candidate.y,hookExtent.y-offset.y+.002f);
            }
            // Sweep hook and held cargo together; preserve gantry movement so reversing can release contact.
            foreach(var id in MagneticToys)Map.Toys[id].Attached=true;
            var resolved=Map.Slide(priorClaw+offset,candidate+offset,hookExtent,Tool==ClawTool.Scoop?-2:HeldToy)-offset;

            if(!Map.Overlaps(resolved,Vector3.one*.45f,HeldToy))Claw=resolved;
            if(Vector3.Distance(Claw,candidate)>.02f){
                swing=Claw-Anchor;swing.y=0;swingVelocity*=.5f;
                Notice="Contact: reverse or slide along the frame. R: recover crane and return load.";
            }
            var newVelocity = (Claw - priorClaw) / dt;
            if (HasLoad)
            {
                if (GripQuality < 0.85f) GripQuality -= (dt * (0.035f + newVelocity.magnitude * 0.08f) + (newVelocity - clawVelocity).magnitude * 0.014f)*Shift.GripWear;
                if(Gripped)Prize = Claw - Vector3.up * MissionOffset;
                else if(HeldToy>=0)Map.Toys[HeldToy].SetCenter(Claw-Vector3.up*HoldOffset);
                if (GripQuality <= 0) { SlipEvents++; Release("Loose hook slipped! Watch below."); }
            }
            for(int i=0;i<MagneticToys.Count;i++)Map.Toys[MagneticToys[i]].SetCenter(Claw+MagneticOffsets[i]);
            clawVelocity = newVelocity;
            StepToys(dt);
            if (!Gripped && ResetTimer <= 0)
            {
                var prior = Prize;
                if(PhysicalCargo){
                    if(threadPhysics==null)threadPhysics=new ClawThreadPhysics(Map,CurrentExtents(0),MissionOffset,Tool==ClawTool.Scoop);
                    threadPhysics.Step(priorClaw,Claw,ref Prize,ref PrizeVelocity,ref PrizeRotation,dt,Tool==ClawTool.Scoop?RigRotation:Quaternion.identity);
                    if(PrizeVelocity.y < -3.5f)DetectImpact(prior,Prize,CurrentExtents(PrizeYaw),PrizeVelocity);
                } else {
                PrizeVelocity.y -= 12 * dt;
                var next = Prize + PrizeVelocity * dt;
                var ext = CurrentExtents(PrizeYaw);
                next.x = Mathf.Clamp(next.x, -ClawLayout.HalfWidth + ext.x, ClawLayout.HalfWidth - ext.x);
                next.z = Mathf.Clamp(next.z, -ClawLayout.HalfDepth + ext.z, ClawLayout.HalfDepth - ext.z);
                if (Map.Overlaps(new Vector3(next.x, prior.y, prior.z), ext)) { next.x = prior.x; PrizeVelocity.x = 0; }
                if (Map.Overlaps(new Vector3(next.x, prior.y, next.z), ext)) { next.z = prior.z; PrizeVelocity.z = 0; }
                float landing = Map.LandingHeight(prior, next, ext);
                if (PrizeVelocity.y < -3.5f) DetectImpact(prior, next, ext, PrizeVelocity);
                if (next.y <= landing) { next.y = landing; PrizeVelocity = Vector3.zero; }
                Prize = next;
                }
                var deliveryExt=CurrentExtents(PrizeYaw);
                if (Map.InsideHole(Prize, deliveryExt) && Prize.y < -0.5f)
                {
                    Score++; Shift.Delivery(); ResetTimer = 2.5f; Notice = "DELIVERED! Next figure in the load bay.";
                    foreach (var p in Players.Values) if (p.Attachment == 1) p.Attachment = 0;
                }
            }
            if (ResetTimer <= 0 && Map.LaserHit(priorPrize, Prize, CurrentExtents(PrizeYaw)))
            {
                Release("LASER! Figure reset. Read the beam height before crossing.");
                LaserResets++; Shift.LaserPenalty(); ResetTimer = 2.5f;
            }
            if (ResetTimer > 0){DisposePhysics();PrizeRotation=Quaternion.identity;
                ResetTimer -= dt;
                if (ResetTimer <= 0) { Prize = Map.PrizeSpawn(Score); Prize.y=CurrentExtents(0).y; PrizeVelocity = Vector3.zero; PrizeYaw = 0; }
            }
            foreach (var p in Players.Values)
            {
                var beforeAttachment=p.Position;
                if (p.Attachment == 1) p.Position = Prize + Quaternion.Euler(0, PrizeYaw, 0) * new Vector3((p.Slot % 2 == 0 ? -1 : 1)*(CurrentExtents(0).x+.2f), 0.3f, p.Slot < 2 ? -0.35f : 0.35f);
                if (p.Attachment == 2) p.Position = Claw + new Vector3((p.Slot % 2 == 0 ? -1 : 1) * 0.65f, -0.7f, 0);
                if(p.Attachment!=0 && (Map.Overlaps(p.Position,new Vector3(.3f,.64f,.3f)) || Vector3.Distance(Map.Slide(beforeAttachment,p.Position,new Vector3(.3f,.64f,.3f)),p.Position)>.05f)) {p.Attachment=0;p.Position=Map.FreePlayerPosition(beforeAttachment);p.VerticalSpeed=0;Notice="Grip released to keep crew clear of the frame.";}
            }
        }
        void StepToys(float dt)
        {
            foreach(var toy in Map.Toys){
                if(toy.PhysicsManaged)continue;
                if(toy.Id==HeldToy||MagneticToys.Contains(toy.Id)){
                    if(Map.LaserHit(toy.Bounds.center,toy.Bounds.center,toy.Bounds.extents)){
                        Release("Laser recycled the wrong plush.");toy.Reset();
                    }
                    continue;
                }
                if(!toy.Moving)continue;
                var previous=toy.Bounds.center;toy.Velocity.y-=12*dt;
                var target=previous+toy.Velocity*dt;
                target.x=Mathf.Clamp(target.x,-ClawLayout.HalfWidth+toy.Bounds.extents.x,ClawLayout.HalfWidth-toy.Bounds.extents.x);
                target.z=Mathf.Clamp(target.z,-ClawLayout.HalfDepth+toy.Bounds.extents.z,ClawLayout.HalfDepth-toy.Bounds.extents.z);
                var next=Map.Slide(previous,target,toy.Bounds.extents,toy.Id);
                float floor=Map.LandingHeight(previous,next,toy.Bounds.extents,toy.Id);
                if(next.y<=floor){next.y=floor;toy.Velocity.y=0;var planar=Vector3.MoveTowards(new Vector3(toy.Velocity.x,0,toy.Velocity.z),Vector3.zero,5f*dt);toy.Velocity=planar;toy.Moving=planar.sqrMagnitude>.0025f;}
                if(Mathf.Abs(next.x-target.x)>.001f)toy.Velocity.x=0;
                if(Mathf.Abs(next.z-target.z)>.001f)toy.Velocity.z=0;
                if(Mathf.Abs(next.y-target.y)>.001f&&toy.Moving){toy.Velocity.y=0;}
                if(toy.Velocity.y < -3.5f)DetectImpact(previous,next,toy.Bounds.extents,toy.Velocity);
                toy.SetCenter(next);
                if(Map.LaserHit(previous,next,toy.Bounds.extents)||(Map.InsideHole(next,toy.Bounds.extents)&&next.y<-.5f)){
                    toy.Reset();Notice="Wrong plush recycled. Find the marked order.";
                }
            }
        }
        void RecoverPlayer(Player p)
        {
            if(p.RecoveryCooldown>0)return;
            p.RecoveryCooldown=5;
            if(Operator==p.Id){
                if(HeldToy>=0)Map.Toys[HeldToy].Reset();
                DisposePhysics();PrizeRotation=Quaternion.identity;Release("Crane recovered. Load returned to its bay.");
                foreach(var other in Players.Values)if(other.Attachment!=0){other.Attachment=0;other.Position=Map.FreePlayerPosition(other.Position);}
                Claw=Anchor=Map.RigSpawn;swing=swingVelocity=clawVelocity=Vector3.zero;
                if(ResetTimer<=0){Prize=Map.PrizeSpawn(Score);Prize.y=CurrentExtents(0).y;PrizeVelocity=Vector3.zero;PrizeYaw=0;}
            }else{
                p.Attachment=0;p.Position=Map.CrewSpawn+Vector3.right*(.5f+p.Slot*1.25f);
                p.VerticalSpeed=0;p.KnockVelocity=Vector3.zero;p.KnockTimer=0;p.Invulnerable=1;
                Notice="Crew recovered to the console.";
            }
            if(Shift.Phase==ShiftPhase.Active)Shift.Seconds=Mathf.Max(0,Shift.Seconds-10);
        }
        void Interact(Player p)
        {
            if (Operator == p.Id)
            {
                if(Tool==ClawTool.Scoop){Notice="Slide the open scoop under the load. Brake gently; E does not attach.";return;}
                if(PhysicalHook){Notice="Thread the open steel tip through the ring. Lower and reverse to unthread; E does not attach.";return;}
                if (HasLoad) Release("Released! Stand clear below the load.");
                else if(Map.RoomCount>0&&Tool==ClawTool.Magnet){MagnetGrab();return;}
                else
                {
                    float offset = Vector3.Distance(Claw, Prize + Vector3.up * MissionOffset);
                    int nearest=-1;float best=ResetTimer<=0?offset:float.MaxValue;
                    foreach(var toy in Map.Toys){float distance=Vector3.Distance(Claw,toy.Target);if(distance<best){nearest=toy.Id;best=distance;}}
                    if(nearest>=0 && best<(Map.RoomCount>0&&Tool==ClawTool.Hook?.35f:1.65f)){
                        var toy=Map.Toys[nearest];
                        var target=Claw-Vector3.up*(toy.Bounds.extents.y+.15f);
                        if(!Map.Overlaps(target,toy.Bounds.extents,nearest)){
                            HeldToy=nearest;toy.Moving=false;toy.Velocity=Vector3.zero;GripQuality=Mathf.Clamp01(1-best/1.65f);
                            Notice="Wrong plush! E drops it. Only the marked order earns credit.";
                        } else Notice="This plush is wedged. Align above it before gripping.";
                        return;
                    }
                    if (offset < (Map.RoomCount>0?(Tool==ClawTool.Hook?.35f:1.4f):1.65f) && ResetTimer <= 0)
                    {
                        Gripped = true; GripQuality = Map.RoomCount>0&&Tool==ClawTool.Scoop?1:Mathf.Clamp01(1 - offset / 1.65f);
                        Notice = GripQuality >= 0.85f ? "Hook secure." : "LOOSE HOOK! Helper: hold the figure and hold F to secure.";
                    }
                    else Notice = "Center the hook over the figure's ring, then press E.";
                }
            }
            else if(Tool==ClawTool.Magnet){ChangePole(p);return;}
            else if (p.Attachment != 0) p.Attachment = 0;
            else if (Map.RoomCount==0 && Vector3.Distance(p.Position, ConsolePosition) < 1.8f)
            {
                if (Operator == Nobody) { Operator = p.Id; Notice = "DEAF PILOT: read crew chat. You cannot hear incoming voice. Q leaves controls."; }
                else Notice = "The controls are in use.";
            }
            else if (Vector3.Distance(p.Position, Prize) < 2.2f && ResetTimer <= 0) p.Attachment = 1;
            else if (Vector3.Distance(p.Position, Claw) < 1.7f) p.Attachment = 2;
        }
        void Release(string notice)
        {
            foreach(var id in MagneticToys){Map.Toys[id].Moving=true;Map.Toys[id].Attached=false;Map.Toys[id].Velocity=clawVelocity*.55f;}MagneticToys.Clear();MagneticOffsets.Clear();
            if(HeldToy>=0){var toy=Map.Toys[HeldToy];toy.Moving=true;toy.Velocity=clawVelocity*.55f;HeldToy=-1;GripQuality=0;Notice=notice;return;}
            Gripped = false; GripQuality = 0; PrizeVelocity = clawVelocity * 0.55f; Notice = notice;
            foreach (var p in Players.Values) if (p.Attachment == 1) { p.Attachment = 0; p.VerticalSpeed = PrizeVelocity.y; }
        }
        void PushToys(Player player,Vector2 movement,float dt)
        {
            if(movement.sqrMagnitude<.01f||player.KnockTimer>0)return;
            var delta=new Vector3(movement.x,0,movement.y)*dt;
            var body=new Bounds(player.Position+delta,new Vector3(.64f,1.28f,.64f));
            if(!Gripped&&ResetTimer<=0&&body.Intersects(new Bounds(Prize,CurrentExtents(PrizeYaw)*2))){
                var moved=Map.Slide(Prize,Prize+delta*1.1f,CurrentExtents(PrizeYaw));PrizeVelocity=(moved-Prize)/dt*.4f;Prize=moved;
            }
            foreach(var toy in Map.Toys){
                if(toy.Id==HeldToy||MagneticToys.Contains(toy.Id)||!body.Intersects(toy.Bounds)||player.Position.y-Floor>=toy.Bounds.max.y-.04f)continue;
                var from=toy.Bounds.center;var target=from+delta*1.15f;
                target.x=Mathf.Clamp(target.x,-ClawLayout.HalfWidth+toy.Bounds.extents.x,ClawLayout.HalfWidth-toy.Bounds.extents.x);
                target.z=Mathf.Clamp(target.z,-ClawLayout.HalfDepth+toy.Bounds.extents.z,ClawLayout.HalfDepth-toy.Bounds.extents.z);
                var next=Map.Slide(from,target,toy.Bounds.extents,toy.Id);
                bool blocked=false;
                foreach(var other in Players.Values)if(other.Id!=player.Id&&new Bounds(next,toy.Bounds.size).Intersects(new Bounds(other.Position,new Vector3(.6f,1.28f,.6f)))){blocked=true;break;}
                if(blocked||(next-from).sqrMagnitude<.000001f)continue;
                toy.SetCenter(next);toy.Velocity=Vector3.ClampMagnitude((next-from)/dt*.65f,2.2f);toy.Moving=true;
            }
        }
        public void AssignRandomPilot()
        {
            if(Players.Count==0)return;
            var crew=new List<Player>(Players.Values);var chosen=crew[new System.Random().Next(crew.Count)];
            Operator=chosen.Id;chosen.Position=ClawLayout.PilotSeat;chosen.Attachment=0;chosen.VerticalSpeed=0;
            Notice="Random pilot: P"+(chosen.Slot+1)+". CCTV only. Crew: guide and prepare the load.";
        }
        // -3: none, -2: crane magnet, -1: mission toy, nonnegative: scenery toy.
        public int PoleTarget(Player p)
        {
            if(p.Id==Operator||p.KnockTimer>0||Tool!=ClawTool.Magnet)return -3;
            var hand=p.Position+Vector3.up*.5f;float best=1.8f;int selected=-3;
            void Consider(Bounds bounds,int id){var point=bounds.ClosestPoint(hand);var delta=point-hand;float d=delta.magnitude;if(d>=best)return;
                if(d>.001f){var ray=new Ray(hand,delta/d);foreach(var wall in Map.Obstacles)if(wall.IntersectRay(ray,out float hit)&&hit<d)return;}
                best=d;selected=id;}
            Consider(new Bounds(Claw+Vector3.down*.5f,new Vector3(1.3f,.5f,1.3f)),-2);
            if(ResetTimer<=0)Consider(new Bounds(Prize,CurrentExtents(PrizeYaw)*2),-1);
            foreach(var toy in Map.Toys)Consider(toy.Bounds,toy.Id);
            return selected;
        }
        void ChangePole(Player p)
        {
            int target=PoleTarget(p);if(target==-3)return;
            if(target==-2)MagnetPole*=-1;
            else if(target==-1)PrizePole*=-1;
            else {Map.Toys[target].Pole*=-1;Map.Toys[target].Changed=true;}
            Notice=(target==-2?"Magnet":target==-1?"Mission toy":"Plush "+(target+1))+" polarity switched by P"+(p.Slot+1);
            if(Gripped&&PrizePole==MagnetPole){Gripped=false;PrizeVelocity=clawVelocity;}
            for(int i=MagneticToys.Count-1;i>=0;i--){var toy=Map.Toys[MagneticToys[i]];if(toy.Pole!=MagnetPole)continue;toy.Attached=false;toy.Moving=true;toy.Velocity=clawVelocity;MagneticToys.RemoveAt(i);MagneticOffsets.RemoveAt(i);}
        }
        void MagnetGrab()
        {
            if(ResetTimer<=0&&PrizePole!=MagnetPole&&Vector3.Distance(Claw,Prize+Vector3.up*MissionOffset)<2.2f){Gripped=true;}
            foreach(var toy in Map.Toys){
                if(toy.Pole==MagnetPole||Vector3.Distance(Claw,toy.Target)>2.2f||MagneticToys.Count>=6)continue;
                MagneticToys.Add(toy.Id);MagneticOffsets.Add(toy.Bounds.center-Claw);toy.Moving=false;toy.Attached=true;
            }
            GripQuality=1;Notice=HasLoad?"Magnet engaged: "+(MagneticToys.Count+(Gripped?1:0))+" items. E releases.":"No opposite pole in range. Crew: G changes polarity.";
        }
        void MovePlayer(Player p, Vector2 velocity, float dt)
        {
            var old = p.Position; var next = old;
            var ext = new Vector3(0.3f, Floor - 0.01f, 0.3f);
            PushToys(p,velocity,dt);
            next.x = Mathf.Clamp(old.x + velocity.x * dt, -ClawLayout.HalfWidth + .5f, ClawLayout.HalfWidth - .5f);
            if (Map.Overlaps(next, ext)) next.x = old.x;
            next.z = Mathf.Clamp(old.z + velocity.y * dt, -ClawLayout.HalfDepth + .5f, ClawLayout.HalfDepth - .5f);
            if (Map.Overlaps(next, ext)) next.z = old.z;
            float gravity=p.VerticalSpeed>0?12f:16f;
            var verticalTarget=next+Vector3.up*(p.VerticalSpeed*dt-.5f*gravity*dt*dt);p.VerticalSpeed-=gravity*dt;
            next=Map.Slide(next,verticalTarget,ext);
            if(Mathf.Abs(next.y-verticalTarget.y)>.001f)p.VerticalSpeed=0;
            float landing = Map.LandingHeight(old, next, new Vector3(0.28f, Floor, 0.28f));
            if (next.y <= landing) { next.y = landing; p.VerticalSpeed = 0; }
            if (next.y < -1) { next = ConsolePosition + Vector3.right; p.VerticalSpeed = 0; p.Attachment = 0; }
            p.Position = next;
        }
        void DetectImpact(Vector3 previous, Vector3 next, Vector3 ext, Vector3 velocity)
        {
            var swept = new Bounds((previous + next) / 2, ext * 2 + new Vector3(Mathf.Abs(next.x - previous.x), Mathf.Abs(next.y - previous.y), Mathf.Abs(next.z - previous.z)));
            foreach (var p in Players.Values)
                if (p.KnockTimer <= 0 && p.Invulnerable <= 0 && swept.Intersects(new Bounds(p.Position, new Vector3(0.6f, 1.4f, 0.6f))))
                {
                    p.KnockTimer = KnockDuration; p.Attachment = 0;
                    var away = p.Position - next; away.y = 0;
                    if (away.sqrMagnitude < 0.01f) away = Vector3.back;
                    p.KnockVelocity = away.normalized * 4 + new Vector3(velocity.x, 0, velocity.z) * 0.4f;
                    p.VerticalSpeed = 2.8f;
                    if (Operator == p.Id) Operator = Nobody;
                    KnockEvents++; Notice = "P" + (p.Slot + 1) + " got flattened! Getting up in a moment...";
                }
        }
        public static Vector3 PrizeExtents(float yaw)
        {
            float a = yaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Abs(Mathf.Cos(a)) * 1.3f + Mathf.Abs(Mathf.Sin(a)) * 0.65f, 0.85f, Mathf.Abs(Mathf.Sin(a)) * 1.3f + Mathf.Abs(Mathf.Cos(a)) * 0.65f);
        }
        public Vector3 CurrentExtents(float yaw)
        {
            if(Shift.Stage==0||Shift.Stage==3)return PrizeExtents(yaw);
            float a=yaw*Mathf.Deg2Rad;float x=Shift.Stage==1?.75f:.7f,z=.65f,y=Shift.Stage==1?1.05f:1.2f;
            return new Vector3(Mathf.Abs(Mathf.Cos(a))*x+Mathf.Abs(Mathf.Sin(a))*z,y,Mathf.Abs(Mathf.Sin(a))*x+Mathf.Abs(Mathf.Cos(a))*z);
        }
        public bool CanPrizeOccupy(Vector3 p, float yaw) => !Map.Overlaps(p, CurrentExtents(yaw));
    }
}

















