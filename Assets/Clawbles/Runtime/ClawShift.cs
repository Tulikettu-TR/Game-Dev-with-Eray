using UnityEngine;
namespace Clawbles
{
    public enum ShiftPhase { Practice, Briefing, Active, Upgrade, Won, Lost }
    public sealed class ClawShift
    {
        public ShiftPhase Phase;
        public int Stage, Credits, Stars, StageStartScore, RunId, GripLevel, TeamLevel, ExtraTime, InitialSeed;
        public float Seconds;
        int startLasers,startKnocks;ClawSimulation active;
        public bool Blocking => Phase!=ShiftPhase.Practice && Phase!=ShiftPhase.Active;
        public string LoadName => (Stage==0||Stage==3)?"HONEY DUCK":Stage==1?"BUTTON BEAR":"LONG-EAR BUNNY";
        public int Quota => Stage+1;
        public const int StageCount=4;
        public string Title => Stage==3?"SCOOP SHIFT":Stage==0?"FIRST SHIFT":Stage==1?"GLASS PARADE":"OVERTIME";
        public string Rule => Stage==0?"One careful delivery. Learn to trust your crew.":Stage==1?"Moving window lasers. Wait for a gap; brake the faster crane early.":"Faster, wider laser sweeps plus ventilation drift. Time your crossing.";
        public float CraneSpeed => (Stage==1 && Phase==ShiftPhase.Active)?6.2f:5;
        public float GripWear => 1f/(1+GripLevel*.45f);
        public float SecureSpeed => .65f+TeamLevel*.35f;
        public void NewRun(ClawSimulation s,int seed)
        {
            RunId++;InitialSeed=seed;Stage=Credits=Stars=GripLevel=TeamLevel=ExtraTime=0;
            s.Score=s.LaserResets=s.KnockEvents=s.SlipEvents=0;
            Prepare(s);Phase=ShiftPhase.Briefing;
        }
        void Prepare(ClawSimulation s)
        {
            active=s;s.SetSeed(unchecked(InitialSeed+Stage*7919),Stage==0?9:Stage==1?16:25);
            s.ResetRig();StageStartScore=s.Score;Seconds=600+Stage*180+ExtraTime;
            startLasers=s.LaserResets;startKnocks=s.KnockEvents;
        }
        public bool Start()
        {if(Phase!=ShiftPhase.Briefing)return false;Phase=ShiftPhase.Active;if(active!=null&&active.Map.RoomCount>0)active.AssignRandomPilot();return true;}
        public void Tick(ClawSimulation s,float dt)
        {
            if(Phase!=ShiftPhase.Active)return;
            // A completed delivery wins a deadline tie; the host is the only authority.
            if(s.Score-StageStartScore>=Quota){
                int errors=s.LaserResets-startLasers+s.KnockEvents-startKnocks;
                Stars+=errors==0?3:errors<=2?2:1;Credits+=100+(int)(Seconds*.25f);
                Phase=Stage==StageCount-1?ShiftPhase.Won:ShiftPhase.Upgrade;
                s.ReleaseForShift();return;
            }
            Seconds=Mathf.Max(0,Seconds-dt);
            if(Seconds<=0){Phase=ShiftPhase.Lost;s.ReleaseForShift();}
        }
        public void Delivery(){if(Phase==ShiftPhase.Active)Credits+=100;}
        public void LaserPenalty(){if(Phase==ShiftPhase.Active)Seconds=Mathf.Max(0,Seconds-15);}
        public bool Next(ClawSimulation s,int choice)
        {
            if(Phase!=ShiftPhase.Upgrade||choice<0||choice>3)return false;
            int cost=choice==3?0:150;if(Credits<cost)return false;
            Credits-=cost;if(choice==0)GripLevel++;if(choice==1)TeamLevel++;if(choice==2)ExtraTime+=60;
            Stage++;Prepare(s);Phase=ShiftPhase.Briefing;return true;
        }
    }
}






