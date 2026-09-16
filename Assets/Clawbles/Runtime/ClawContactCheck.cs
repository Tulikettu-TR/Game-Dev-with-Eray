using System;
using System.IO;
using UnityEngine;
namespace Clawbles
{
    // Opt-in regression: create the world before hosting with the same seed,
    // then verify real walking input moves both the simulation and rendered figure.
    public static class ClawContactCheck
    {
        static bool initialized,prepared,done;
        static string path;
        static float began;
        public static void BeforeStep(ClawSession s)
        {
            if(!initialized){initialized=true;var a=Environment.GetCommandLineArgs();for(int i=0;i<a.Length-1;i++)if(a[i]=="-clawContactReport")path=a[i+1];}
            if(path==null||!s.Hosting||s.State.Players.Count<2)return;
            if(!prepared){
                prepared=true;began=Time.unscaledTime;
                var toy=s.State.Map.Toys[0];toy.SetCenter(new Vector3(0,toy.Bounds.extents.y+.002f,0));toy.Velocity=Vector3.zero;
                s.State.Players[s.LocalId].Position=new Vector3(-toy.Bounds.extents.x-.34f,ClawSimulation.Floor,0);
            }
            s.State.Input(s.LocalId,Time.unscaledTime-began<.8f?Vector2.right:Vector2.zero,0,0,0,Time.unscaledTime);
            if(done||Time.unscaledTime-began<2)return;
            var field=s.GetComponentInChildren<ClawToyField>();
            var figure=s.State.Map.Toys[0];
            bool ok=field!=null&&ReferenceEquals(field.BoundLayout,s.State.Map)&&figure.Bounds.center.x>.5f&&Vector3.Distance(field.PresentedCenter(0),figure.Bounds.center)<.02f;
            File.WriteAllText(path,"{\"ok\":"+ok.ToString().ToLowerInvariant()+",\"pushDistance\":"+figure.Bounds.center.x.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
            Debug.Log("CLAWBLES_CONTACT_RENDER_CHECK "+ok);done=true;
        }
    }
}
