using System;

namespace FenerRunner
{
    public enum RouteAction { None, Jump, Slide }
    public enum CourseHazard { Clear, Jump, Slide }
    public struct PatternRow
    {
        public double Distance;
        public float Gap;
        public int Sequence,Lane;
        public CourseHazard Left,Centre,Right;
        public RouteAction Action;
        public string Pattern;
        public bool Recovery;
        public int Phrase,Family,PhraseStep,DifficultyTier;
        public bool Compulsory;
        public CourseHazard At(int lane){return lane<0?Left:lane>0?Right:Centre;}
        public void Set(int lane,CourseHazard value){if(lane<0)Left=value;else if(lane>0)Right=value;else Centre=value;}
        public int HazardCount=>(Left==CourseHazard.Clear?0:1)+(Centre==CourseHazard.Clear?0:1)+(Right==CourseHazard.Clear?0:1);
    }
    public struct CourseRoute
    {
        public float Z;
        public int Lane,Sequence;
        public RouteAction Action;
        public string Pattern;
        public bool Recovery;
    }

    // Authored spatial phrases are varied by a seeded generator. A separate action
    // deadline makes it impossible to replace all gestures with endless lane changes.
    public sealed class CoursePatternGenerator
    {
        public const float MaximumSpeed=RunTempo.HardCap;
        public const float MinimumClearGap=28.5f;
        public const float MinimumActionGap=35f;
        public const float MinimumClearSeconds=.90f,MinimumActionSeconds=1.17f;
        public const float MaximumGeneratedGap=62f;
        public const float MaximumCompulsoryActionGap=256f;
        public const int MaximumCompulsoryRowGap=4,FamilyCount=12,MaximumPhraseLength=6;
        // Retained for source compatibility. New phrases have individual lengths.
        public const int PatternLength=5;
        public const float LaneChangeLead=25f;
        public static float RequiredGap(bool action,double nextRowDistance)
        {return Math.Max(action?MinimumActionGap:MinimumClearGap,RunTempo.CeilingAtDistance(nextRowDistance)*(action?MinimumActionSeconds:MinimumClearSeconds));}
        static readonly string[] names={"SLALOM","İKİLİ SIÇRAYIŞ","ALÇAK TÜNELLER","DEĞİŞEN RİTİM",
            "ÇAPRAZ ATLAMA","ÇAPRAZ KAYMA","ÇİFT KAPI","MERDİVEN","YOL AYRIMI","KAPANAN ŞERİT","DALGA","SPRİNT"};
        static readonly int[] lengths={4,3,3,4,4,4,4,5,4,4,6,6};
        static readonly int[] earlyFamilies={0,1,2,6,8,9},middleFamilies={0,1,2,3,4,5,6,8,9};
        uint state;
        int sequence,lane,family=-1,previousFamily=-1,phrase=-1,step,phraseLength,direction=1;
        int lastJump=-1,lastSlide=-1,variant;
        double distance;
        RouteAction previousAction;
        public int Seed{get;private set;}
        public CoursePatternGenerator(int seed)
        {Seed=seed;state=unchecked((uint)seed);if(state==0)state=0x9E3779B9;}
        uint NextBits(){state^=state<<13;state^=state>>17;state^=state<<5;return state;}
        int Pick(int count){return (int)(NextBits()%(uint)count);}
        static float Clamp01(float value){return Math.Max(0,Math.Min(1,value));}
        static void Fill(ref PatternRow row,CourseHazard hazard){row.Left=row.Centre=row.Right=hazard;}
        static bool Uniform(PatternRow row,CourseHazard hazard){return row.Left==hazard&&row.Centre==hazard&&row.Right==hazard;}
        void MoveRoute()
        {
            if(lane+direction<-1||lane+direction>1)direction=-direction;
            lane+=direction;
        }
        void OpenLane(ref PatternRow row,bool narrow,CourseHazard hazard)
        {
            if(narrow){Fill(ref row,hazard);row.Set(lane,CourseHazard.Clear);}
            else row.Set(lane==0?(variant==0?-1:1):-lane,hazard);
        }
        void Mixed(ref PatternRow row,CourseHazard required)
        {
            Fill(ref row,required==CourseHazard.Jump?CourseHazard.Slide:CourseHazard.Jump);
            row.Set(lane,required);
        }
        void Rest(ref PatternRow row)
        {row.Recovery=true;row.Pattern="NEFES ARALIĞI";row.Set(lane==0?-direction:-lane,variant==0?CourseHazard.Jump:CourseHazard.Slide);}
        void BeginPhrase(int tier)
        {
            int[] selection=tier==0?earlyFamilies:tier==1?middleFamilies:null;
            do{family=selection==null?Pick(FamilyCount):selection[Pick(selection.Length)];}while(family==previousFamily);
            previousFamily=family;phrase++;step=0;phraseLength=lengths[family];variant=Pick(2);direction=Pick(2)==0?-1:1;
        }
        void BuildBeat(ref PatternRow row,float difficulty)
        {
            bool narrow=difficulty>.4f||variant==1;
            switch(family)
            {
                case 0:
                    MoveRoute();OpenLane(ref row,narrow,step%2==0?CourseHazard.Jump:CourseHazard.Slide);break;
                case 1:
                    if(step<2)Fill(ref row,CourseHazard.Jump);else Rest(ref row);break;
                case 2:
                    if(step<2)Fill(ref row,CourseHazard.Slide);else{MoveRoute();Rest(ref row);}break;
                case 3:
                    Fill(ref row,(step+variant)%2==0?CourseHazard.Jump:CourseHazard.Slide);break;
                case 4:
                    if(step==3)Rest(ref row);else{MoveRoute();Mixed(ref row,CourseHazard.Jump);}break;
                case 5:
                    if(step==3){MoveRoute();Rest(ref row);}else{MoveRoute();Mixed(ref row,CourseHazard.Slide);}break;
                case 6:
                    if(step<2){if(step==1)MoveRoute();OpenLane(ref row,true,step==0?CourseHazard.Slide:CourseHazard.Jump);}
                    else Fill(ref row,step==2?CourseHazard.Slide:CourseHazard.Jump);break;
                case 7:
                    MoveRoute();
                    if(step==4)OpenLane(ref row,true,CourseHazard.Slide);
                    else Mixed(ref row,step%2==0?CourseHazard.Jump:CourseHazard.Slide);break;
                case 8:
                    if(step==0)OpenLane(ref row,false,CourseHazard.Jump);
                    else if(step==1){MoveRoute();OpenLane(ref row,true,CourseHazard.Slide);}
                    else if(step==2)Mixed(ref row,variant==0?CourseHazard.Jump:CourseHazard.Slide);
                    else Rest(ref row);break;
                case 9:
                    if(step==0){int closed=lane;MoveRoute();row.Set(closed,CourseHazard.Slide);}
                    else if(step==1)OpenLane(ref row,true,CourseHazard.Jump);
                    else Fill(ref row,step==2?CourseHazard.Jump:CourseHazard.Slide);break;
                case 10:
                    if(step==0||step==3)Fill(ref row,CourseHazard.Jump);
                    else if(step==1||step==4){MoveRoute();Mixed(ref row,CourseHazard.Slide);}
                    else if(step==2)OpenLane(ref row,true,CourseHazard.Jump);
                    else Rest(ref row);break;
                default:
                    if(step==0||step==1||step==3){MoveRoute();OpenLane(ref row,true,step%2==0?CourseHazard.Jump:CourseHazard.Slide);}
                    else if(step==2)Mixed(ref row,CourseHazard.Jump);
                    else if(step==4)Fill(ref row,CourseHazard.Slide);
                    else Rest(ref row);break;
            }
        }
        public PatternRow Next()
        {
            float difficulty=Clamp01((float)(distance/1800.0));
            int tier=distance<350?0:distance<800?1:distance<1500?2:3;
            var row=new PatternRow{Sequence=sequence,Family=-1,Phrase=-1,DifficultyTier=tier};
            if(sequence<2)
            {
                lane=0;Fill(ref row,sequence==0?CourseHazard.Jump:CourseHazard.Slide);
                row.Pattern=sequence==0?"YUKARI KAYDIR":"AŞAĞI KAYDIR";
            }
            else
            {
                if(family<0||step>=phraseLength)BeginPhrase(tier);
                row.Family=family;row.Phrase=phrase;row.PhraseStep=step;row.Pattern=names[family];
                BuildBeat(ref row,difficulty);step++;
                // Replace an existing beat, never add a hidden extra obstacle.
                if(sequence-lastJump>=MaximumCompulsoryRowGap)
                {Fill(ref row,CourseHazard.Jump);row.Recovery=false;row.Pattern=names[family];}
                else if(sequence-lastSlide>=MaximumCompulsoryRowGap)
                {Fill(ref row,CourseHazard.Slide);row.Recovery=false;row.Pattern=names[family];}
            }
            row.Lane=lane;
            row.Action=row.At(lane)==CourseHazard.Jump?RouteAction.Jump:row.At(lane)==CourseHazard.Slide?RouteAction.Slide:RouteAction.None;
            bool jump=Uniform(row,CourseHazard.Jump),slide=Uniform(row,CourseHazard.Slide);
            row.Compulsory=jump||slide;if(jump)lastJump=sequence;if(slide)lastSlide=sequence;
            float gap;
            if(sequence==0)gap=70;
            else if(sequence==1)gap=45;
            else
            {
                bool action=row.Action!=RouteAction.None||previousAction!=RouteAction.None;
                gap=action?45f+(MinimumActionGap-45f)*difficulty:34f+(MinimumClearGap-34f)*difficulty;
                // Bound the speed at the next row, including every allowed boost.
                // The look-ahead uses the declared largest gap, so accelerating
                // within this beat cannot outrun its recovery/read-time budget.
                gap=Math.Max(gap,RequiredGap(action,distance+MaximumGeneratedGap))+Pick(4);
                if(row.Recovery)gap+=6f+2f*difficulty;
                gap=Math.Min(gap,MaximumGeneratedGap);
            }
            row.Gap=gap;distance+=gap;row.Distance=distance;previousAction=row.Action;sequence++;
            return row;
        }
    }
}

