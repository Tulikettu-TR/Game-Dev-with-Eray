using System;
using UnityEngine;
namespace Clawbles.Editor {
 [UnityEditor.InitializeOnLoad] public static class ClawThreadChecks {
 static ClawThreadChecks(){UnityEditor.EditorApplication.playModeStateChanged+=state=>{
 if(state==UnityEditor.PlayModeStateChange.EnteredPlayMode&&UnityEditor.SessionState.GetBool("ThreadTest",false)){
 UnityEditor.SessionState.SetBool("ThreadTest",false);
 try{Run();ClawPoleChecks.Run();ClawScoopChecks.Run();ClawBuild.Validate();ClawBuild.ValidateToyCourse();ClawRoomChecks.Run();if(UnityEditor.SessionState.GetBool("ThreadBuild",false))UnityEditor.EditorApplication.ExitPlaymode();else UnityEditor.EditorApplication.Exit(0);}
 catch(Exception e){Debug.LogException(e);UnityEditor.SessionState.SetBool("ThreadBuild",false);UnityEditor.EditorApplication.Exit(1);}}
 if(state==UnityEditor.PlayModeStateChange.EnteredEditMode&&UnityEditor.SessionState.GetBool("ThreadBuild",false)){
 UnityEditor.SessionState.SetBool("ThreadBuild",false);try{ClawArt.Generate();ClawBuild.Build();UnityEditor.EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);UnityEditor.EditorApplication.Exit(1);}}
 };}
 public static void Play(){UnityEditor.SessionState.SetBool("ThreadTest",true);UnityEditor.EditorApplication.EnterPlaymode();}
 public static void Run(){
 var map=new ClawLayout(1,25);map.Obstacles.Clear();map.Toys.Clear();map.Floors.Clear();map.Floors.Add(new Bounds(new Vector3(0,-.15f,0),new Vector3(20,.3f,20)));
 var ext=new Vector3(.7f,1.2f,.65f);float height=1.75f;var hook=new Vector3(0,4,0);var p=hook+new Vector3(0,-.22f-height,0);var v=Vector3.zero;var q=Quaternion.identity;
 using(var physics=new ClawThreadPhysics(map,ext,height)){
 for(int i=0;i<150;i++)physics.Step(hook,hook,ref p,ref v,ref q,.02f);
 if(p.y<1.5f)throw new Exception("Threaded ring must carry weight without a joint: "+p);
 for(int i=0;i<100;i++){var next=hook+Vector3.up*.01f;physics.Step(hook,next,ref p,ref v,ref q,.02f);hook=next;}
 if(p.y<2.5f)throw new Exception("Contact must lift threaded ring: "+p);
 }
 p=new Vector3(0,1.2f,0);v=Vector3.zero;q=Quaternion.identity;float insertionY=p.y+height+.06f;hook=new Vector3(0,insertionY,-.8f);
 using(var physics=new ClawThreadPhysics(map,ext,height)){
 // The open tip starts outside and crosses the empty ring aperture.
 for(int i=0;i<80;i++){var next=new Vector3(0,insertionY,Mathf.Lerp(-.8f,0,(i+1)/80f));physics.Step(hook,next,ref p,ref v,ref q,.02f);hook=next;}
 for(int i=0;i<100;i++){var next=hook+Vector3.up*.01f;physics.Step(hook,next,ref p,ref v,ref q,.02f);hook=next;}
 Debug.Log("THREAD_INSERT_LIFT "+p);
 if(p.y<1.6f)throw new Exception("Entering through aperture must lift load: "+p);
 // Lower onto floor, then withdraw through the open tip.
 for(int i=0;i<100;i++){var next=hook-Vector3.up*.01f;physics.Step(hook,next,ref p,ref v,ref q,.02f);hook=next;}
 for(int i=0;i<80;i++){var next=hook-Vector3.forward*.01f;physics.Step(hook,next,ref p,ref v,ref q,.02f);hook=next;}
 for(int i=0;i<100;i++){var next=hook+Vector3.up*.01f;physics.Step(hook,next,ref p,ref v,ref q,.02f);hook=next;}
 Debug.Log("THREAD_WITHDRAW "+p);
 if(p.y>1.35f)throw new Exception("Withdrawn hook must no longer lift load: "+p);
 }
 Debug.Log("CLAWBLES_PHYSICAL_THREAD_OK");
 }
 }
}
