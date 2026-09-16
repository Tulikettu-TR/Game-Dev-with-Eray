using UnityEngine;
namespace Clawbles
{
    public static class ClawGestures
    {
        public static int Parse(string text)
        {
            switch(text){
                case "[SIGNAL] WEST / -X":return 1;
                case "[SIGNAL] EAST / +X":return 2;
                case "[SIGNAL] NORTH / +Z":return 3;
                case "[SIGNAL] SOUTH / -Z":return 4;
                case "[SIGNAL] RAISE THE HOOK":return 5;
                case "[SIGNAL] LOWER THE HOOK":return 6;
                case "[SIGNAL] STOP AND LET IT SETTLE":return 7;
                case "[SIGNAL] RELEASE NOW":return 8;
                default:return 0;
            }
        }
        public static string Label(int signal)=>signal==1?"WEST":signal==2?"EAST":signal==3?"NORTH":signal==4?"SOUTH":signal==5?"UP":signal==6?"DOWN":signal==7?"STOP":signal==8?"DROP":"";
        public static void Arms(int signal,float yaw,float time,out Vector3 left,out Vector3 right)
        {
            left=right=Vector3.down;
            if(signal>=1&&signal<=4){
                var world=signal==1?Vector3.left:signal==2?Vector3.right:signal==3?Vector3.forward:Vector3.back;
                var local=Quaternion.Euler(0,-yaw,0)*world;
                if(local.x<0)left=local;else right=local;
            }else if(signal==5){left=new Vector3(-.35f,1,0).normalized;right=new Vector3(.35f,1,0).normalized;}
            else if(signal==6){left=new Vector3(-.35f,-1,.1f).normalized;right=new Vector3(.35f,-1,.1f).normalized;}
            else if(signal==7){left=new Vector3(.65f,.8f,.35f).normalized;right=new Vector3(-.65f,.8f,.35f).normalized;}
            else if(signal==8){float pulse=Mathf.Sin(time*7)*.25f;left=right=new Vector3(0,-.6f+pulse,1).normalized;}
        }
    }
}
