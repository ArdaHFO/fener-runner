using UnityEngine;

namespace FenerRunner
{
    // Shared immutable person sampling keeps baked supporter proportions and
    // runtime hand-release positions in agreement without per-person objects.
    public static class CrowdAnatomy
    {
        public static Vector4 Sample(int side,int row,int number)
        {
            uint key=Key(side,row,number);
            return new Vector4(.93f+Unit(key+11)*.14f,.83f+Unit(key+23)*.34f,.86f+Unit(key+37)*.27f,.91f+Unit(key+53)*.18f);
        }
        public static int Identity(int side,int row,int number)=>(int)(Mix(Key(side,row,number)+71)%120);
        public static float Phase(int side,int row,int number)=>Unit(Key(side,row,number)+97)*6.283185307f;
        static uint Key(int side,int row,int number)=>unchecked((uint)(number*373+row*1163+(side<0?1907:19070)));
        static uint Mix(uint value){value=value*747796405u+2891336453u;value=((value>>((int)(value>>28)+4))^value)*277803737u;return (value>>22)^value;}
        static float Unit(uint value)=>(Mix(value)&0x00ffffff)/16777215f;
    }
}
