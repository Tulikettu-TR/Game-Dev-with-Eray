using System;
using UnityEngine;
namespace Clawbles.Editor {
 public static class ClawScoopChecks {
 static void Check(bool ok,string why){if(!ok)throw new Exception("Scoop check: "+why);}
 public static void Run(){
 foreach(int rooms in new[]{9,16,25})foreach(int seed in new[]{7241,9918,15234}){var s=new ClawSimulation(seed,rooms);s.ResetRig();var c=s.Map.Rooms[0].center;Check(s.Claw.x==c.x&&s.Claw.z==c.z,"rig starts at room center");Check(!s.Map.OverlapsStructure(s.Claw,new Vector3(1.8f,2,2.2f)),"full tool clears spawn walls");}
 var first=new ClawSimulation(7241,9);Check(first.Tool==ClawTool.Claw,"first shift is claw");
 var s4=new ClawSimulation(7241,25);s4.Shift.Stage=3;s4.Add(1);s4.Operator=1;s4.Input(1,Vector2.zero,0,1,2,0);s4.Step(.02f,0);Check(!s4.HasLoad,"E cannot latch scoop cargo");s4.DisposePhysics();
 var map=new ClawLayout(1,25);map.Obstacles.Clear();map.Toys.Clear();map.Floors.Clear();map.Floors.Add(new Bounds(new Vector3(0,-.15f,0),new Vector3(30,.3f,30)));
 var p=new Vector3(0,.85f,-2.9f);var v=Vector3.zero;var q=Quaternion.identity;var scoop=new Vector3(0,1.642f,0);
 using(var physics=new ClawThreadPhysics(map,new Vector3(1.3f,.85f,.65f),1.4f,true)){
 for(int i=0;i<150;i++){var next=scoop-Vector3.forward*.02f;physics.Step(scoop,next,ref p,ref v,ref q,.02f);scoop=next;}
 for(int i=0;i<100;i++){var next=scoop-Vector3.forward*(.02f*(1-(i+1)/100f));physics.Step(scoop,next,ref p,ref v,ref q,.02f);scoop=next;}
 Debug.Log("SCOOP_LOAD "+p+" scoop="+scoop);
 for(int i=0;i<200;i++){var next=scoop+Vector3.up*.01f;physics.Step(scoop,next,ref p,ref v,ref q,.02f);scoop=next;}
 Debug.Log("SCOOP_LIFT "+p);Check(p.y>1.6f,"ramp loads and lifts without E");
 for(int i=0;i<180;i++)physics.Step(scoop,scoop,ref p,ref v,ref q,.02f,Quaternion.Euler(Mathf.Lerp(0,-65,Mathf.Min(1,(i+1)/60f)),0,0));
 Debug.Log("SCOOP_SPILL "+p);Check(p.y<1.4f,"tilt spills unattached load");
 }
 Debug.Log("CLAWBLES_SCOOP_CHECKS_OK");
 }
 }
}
