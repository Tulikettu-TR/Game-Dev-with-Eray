using UnityEngine;
using System;
using System.IO;
namespace Clawbles
{
    // Opt-in synthetic network check; never records a microphone.
    public static class ClawDeafCheck
    {
        static bool initialized, done;
        static string path;
        static int stage, before, muted, after;
        static float start;
        public static void Tick(ClawSession s)
        {
            if(!initialized) {initialized=true;var a=Environment.GetCommandLineArgs();for(int i=0;i<a.Length-1;i++) if(a[i]=="-clawDeafReport")path=a[i+1];}
            if(path==null||done||!s.Hosting||s.State.Players.Count<2)return;
            if(start==0)start=Time.unscaledTime;
            float age=Time.unscaledTime-start;
            if(stage==0&&age>3){before=s.Voice.ReceivedFrames;s.State.Operator=s.LocalId;stage=1;}
            if(stage==1&&age>4){muted=s.Voice.ReceivedFrames;stage=2;}
            if(stage==2&&age>6){
                after=s.Voice.ReceivedFrames;
                s.State.Operator=ClawSimulation.Nobody;stage=3;
            }
            if(stage==3&&age>8){
                bool ok=before>0&&muted==after&&s.Voice.ReceivedFrames>after;
                File.WriteAllText(path,"{\"ok\":"+ok.ToString().ToLowerInvariant()+",\"before\":"+before+",\"deafStart\":"+muted+",\"deafEnd\":"+after+",\"resumed\":"+s.Voice.ReceivedFrames+"}");
                Debug.Log("CLAWBLES_DEAF_CHECK "+ok);done=true;
            }
        }
    }
}
