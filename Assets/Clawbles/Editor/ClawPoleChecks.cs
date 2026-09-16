using System;
using UnityEngine;
namespace Clawbles.Editor {
public static class ClawPoleChecks {
static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
public static void Run(){
var s=new ClawSimulation(7241,16);s.Shift.Stage=1;s.Map.Obstacles.Clear();s.Map.Lasers.Clear();var helper=s.Add(2);var pilot=s.Add(1);s.Operator=1;
s.Prize=new Vector3(15,1,15);s.Claw=s.Anchor=new Vector3(0,6,0);foreach(var toy in s.Map.Toys)toy.SetCenter(new Vector3(20,1,20));var first=s.Map.Toys[0];var second=s.Map.Toys[1];first.SetCenter(new Vector3(0,first.Bounds.extents.y,0));helper.Position=new Vector3(-1,.65f,0);
int pole=first.Pole,other=second.Pole;s.Input(2,Vector2.zero,0,1,2,0);s.Step(.02f,0);Check(first.Pole==-pole&&second.Pole==other,"E changes only nearby plush polarity");
helper.Position=new Vector3(0,.65f,0);first.SetCenter(new Vector3(20,1,20));Check(s.PoleTarget(helper)==-3,"Raised magnet cannot be reached");
s.Claw=s.Anchor=new Vector3(0,1.8f,0);Check(s.PoleTarget(helper)==-2,"Lowered magnet can be reached");int magnet=s.MagnetPole;s.Input(2,Vector2.zero,0,2,2,.02f);s.Step(.02f,.02f);Check(s.MagnetPole==-magnet,"Helper switches magnet by interaction");pilot.Position=helper.Position;Check(s.PoleTarget(pilot)==-3,"Pilot cannot change poles");
s.Map.Obstacles.Add(new Bounds(new Vector3(0,1,0),new Vector3(.1f,3,4)));helper.Position=new Vector3(-1,.65f,0);s.Claw=new Vector3(1,1.8f,0);Check(s.PoleTarget(helper)==-3,"Cannot interact through walls");
Debug.Log("CLAWBLES_DIRECT_POLE_CHECKS_OK");
}}
}
