using System;
using UnityEngine;
namespace Clawbles.Editor {
 public static class ClawRoomChecks {
 static void Check(bool ok,string label){if(!ok)throw new Exception("Room check: "+label);}
 public static void Run(){
 foreach(int count in new[]{9,16,25}){var map=new ClawLayout(7241,count);Check(map.Rooms.Count==count,"room count");Check(map.Lasers.Count>0,"window lasers");Check(!map.OverlapsStructure(map.CrewSpawn,new Vector3(.3f,.64f,.3f)),"crew clearance");}
 var s=new ClawSimulation(7241,9);s.Add(1);s.Add(2);s.AssignRandomPilot();var pilot=s.Operator;Check(s.Players.ContainsKey(pilot),"random member");s.Input(pilot,Vector2.one,1,1,5,0);s.Step(.02f,0);Check(s.Operator==pilot&&s.Players[pilot].Position==ClawLayout.PilotSeat,"pilot remains isolated");s.Remove(pilot);Check(s.Operator!=pilot&&s.Players.ContainsKey(s.Operator),"pilot replacement");
 var m=new ClawSimulation(7241,16);m.Shift.Stage=1;m.Add(1);m.Operator=1;m.Map.Obstacles.Clear();m.Map.Lasers.Clear();m.Prize=new Vector3(0,1,0);m.Claw=m.Anchor=m.Prize+Vector3.up*m.MissionOffset;
 foreach(var toy in m.Map.Toys)toy.SetCenter(new Vector3(20,1,20));var a=m.Map.Toys[0];var b=m.Map.Toys[1];a.Pole=1;b.Pole=-1;a.SetCenter(m.Claw+Vector3.right*.8f);b.SetCenter(m.Claw+Vector3.left*.8f);m.Input(1,Vector2.zero,0,1,2,0);m.Step(.02f,0);Check(m.Gripped&&m.MagneticToys.Contains(a.Id)&&!m.MagneticToys.Contains(b.Id),"opposite poles collect extra toys");m.Input(1,Vector2.zero,0,2,2,.02f);m.Step(.02f,.02f);Check(m.MagneticToys.Count==0&&!a.Attached,"magnet releases");
var push=new ClawSimulation(7241,9);push.Map.Obstacles.Clear();push.Map.Lasers.Clear();push.Map.Toys.Clear();push.Prize=new Vector3(0,ClawSimulation.PrizeExtents(0).y,0);var walker=push.Add(1);walker.Position=new Vector3(-ClawSimulation.PrizeExtents(0).x-.25f,.65f,0);float before=push.Prize.x;for(int i=0;i<25;i++){push.Input(1,Vector2.right,0,0,0,i*.02f);push.Step(.02f,i*.02f);}Check(push.Prize.x>before+.1f,"helper pushes mission toy");

 Debug.Log("CLAWBLES_ROOM_CHECKS_OK");
 }
 }
}
