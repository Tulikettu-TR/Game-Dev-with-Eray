using UnityEngine;
namespace Clawbles
{
    public static class ClawFigureStyle
    {
        public static string Name(int kind)=>kind==0?"Sewn Teddy":kind==1?"Floppy Bunny":"Honey Duck";
        public static Color ColorFor(int kind,int part)
        {
            if(part==1)return kind==2?new Color(.98f,.43f,.19f):new Color(.98f,.9f,.75f);
            if(part==2)return new Color(.075f,.105f,.16f);
            return kind==0?new Color(.89f,.47f,.59f):kind==1?new Color(.38f,.73f,.67f):new Color(.98f,.75f,.25f);
        }
    }
}
