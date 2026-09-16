using System;
using System.IO;
using UnityEngine;
namespace Clawbles
{
    // Explicit opt-in integration scenario: finishes real delivery logic, never captures a microphone.
    public static class ClawCampaignCheck
    {
        static bool initialized,started,done;
        static string path;
        static float nextAction;
        static int phases,stages;
        public static void Tick(ClawSession s)
        {
            if(!initialized){initialized=true;var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]=="-clawCampaignReport")path=args[i+1];}
            if(path==null||done||!s.Connected)return;
            var shift=s.State.Shift;
            phases|=1<<(int)shift.Phase;stages|=1<<shift.Stage;
            if(shift.Phase==ShiftPhase.Won){
                if(s.Hosting){foreach(var toy in s.State.Map.Toys)toy.SetCenter(toy.OriginalCenter+Vector3.up*.05f);s.State.HeldToy=0;foreach(var crew in s.State.Players.Values){crew.Signal=5;crew.SignalTimer=2.5f;}}
                int moved=0;foreach(var toy in s.State.Map.Toys)if(Vector3.Distance(toy.Bounds.center,toy.OriginalCenter+Vector3.up*.05f)<.001f)moved++;
                if(moved!=s.State.Map.Toys.Count||s.State.HeldToy!=0)return;
                foreach(var crew in s.State.Players.Values)if(crew.Signal!=5)return;
                bool ok=(phases&30)==30&&stages==15&&shift.Stars==12&&s.State.Score==10&&shift.GripLevel==1&&shift.TeamLevel==1;
                File.WriteAllText(path,"{\"ok\":"+ok.ToString().ToLowerInvariant()+",\"phaseMask\":"+phases+",\"stageMask\":"+stages+",\"stars\":"+shift.Stars+",\"deliveries\":"+s.State.Score+",\"gripLevel\":"+shift.GripLevel+",\"teamLevel\":"+shift.TeamLevel+"}");
                Debug.Log("CLAWBLES_TOY_NETWORK_CHECK count="+moved+" held="+s.State.HeldToy);Debug.Log("CLAWBLES_CAMPAIGN_NETWORK_CHECK "+ok);done=true;return;
            }
            if(!s.Hosting||s.State.Players.Count<2||Time.unscaledTime<nextAction)return;
            nextAction=Time.unscaledTime+.7f;
            if(!started){started=true;shift.NewRun(s.State,7241);return;}
            if(shift.Phase==ShiftPhase.Briefing)shift.Start();
            else if(shift.Phase==ShiftPhase.Upgrade)shift.Next(s.State,shift.Stage);
            else if(shift.Phase==ShiftPhase.Active){
                s.State.Prize=ClawLayout.Delivery+Vector3.down*.6f;s.State.PrizeVelocity=Vector3.zero;s.State.Gripped=false;s.State.ResetTimer=0;
            }
        }
    }
}


