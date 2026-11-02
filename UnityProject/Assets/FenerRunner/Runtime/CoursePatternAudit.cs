using System;
using System.Collections.Generic;
using System.Text;

namespace FenerRunner
{
    [Serializable]
    public sealed class CoursePatternAuditResult
    {
        public bool passed;
        public int seeds,rowsPerSeed,checkedRows,fullWidthRows,recoveryRows,distinctMotifs;
        public int distinctFamilies,distinctRowLayouts,distinctPhraseLayouts,familiesWithUniqueLayouts;
        public int seedsRequiringJumpAndSlideBefore200,laneOnlyFailures,jumpOnlyFailures,slideOnlyFailures;
        public float minimumClearGap=float.MaxValue,minimumActionGap=float.MaxValue;
        public float maximumAssumedSpeed=CoursePatternGenerator.MaximumSpeed;
        public float minimumActionRecoverySeconds=float.MaxValue,minimumLaneClearanceMetres=float.MaxValue;
        public float minimumJumpClearance=float.MaxValue,minimumSlideClearance=float.MaxValue;
        public double worstLaneOnlySurvivalDistance,worstJumpOnlySurvivalDistance,worstSlideOnlySurvivalDistance;
        public double maximumCompulsoryJumpGap,maximumCompulsorySlideGap,maximumGapBetweenAnyCompulsoryRows;
        public float[] averageGapByTier,averageSecondsByTier,averageHazardsByTier,laneChangesPerRowByTier;
        public float minimumClearSeconds=float.MaxValue,minimumActionSeconds=float.MaxValue;
        public int[] rowsByTier;
        public string[] errors;
        public string scope="Actual C# seeded generator and shared RunTempo curve. Reachable-lane propagation, final spatial phrase signatures, distance-tier temporal density and constructive timing/geometry witnesses from12.24 to42m/s where reachable at that distance. Every row includes the maximum8m/s bonus. Constants model2.65m/.72s asymmetric jumps, .85s slides, .14s one-lane moves, worst15Hz input sampling, .38m controller radius,1.9215m jump blockers and1.66m gate openings. Native motor/collider playback is verified separately in unity_tempo_course_v12. Finite seeded coverage, not every possible player input or physical-device validation.";
    }

    public static class CoursePatternAudit
    {
        const float JumpHeight=2.65f,JumpRise=.72f*.56f,JumpFall=.72f*.44f,SlideSeconds=.85f,CommandDelay=1f/15f;
        static readonly float[] witnessSpeeds={12.24f,18f,24f,30f,34f,38f,42f};
        static bool Uniform(PatternRow row,CourseHazard hazard){return row.Left==hazard&&row.Centre==hazard&&row.Right==hazard;}
        static string Layout(PatternRow row,int previousLane)
        {return ((int)row.Left).ToString()+((int)row.Centre)+((int)row.Right)+"/"+(row.Lane-previousLane)+"/"+(int)row.Action+(row.Recovery?"r":"");}
        static void FinishPhrase(Dictionary<string,int> layouts,StringBuilder phrase,int family,int count)
        {
            if(family<0||count<3)return;
            string signature=phrase.ToString();
            int old;layouts.TryGetValue(signature,out old);layouts[signature]=old|(1<<family);
        }
        public static CoursePatternAuditResult Run(int seeds=2048,int rowsPerSeed=256)
        {
            var report=new CoursePatternAuditResult{seeds=seeds,rowsPerSeed=rowsPerSeed,rowsByTier=new int[4],
                averageGapByTier=new float[4],averageSecondsByTier=new float[4],averageHazardsByTier=new float[4],laneChangesPerRowByTier=new float[4]};
            var failures=new List<string>();var motifs=new HashSet<string>();var rowLayouts=new HashSet<string>();
            var phraseLayouts=new Dictionary<string,int>();int families=0;
            var gapTotals=new double[4];var secondsTotals=new double[4];var hazardTotals=new double[4];var moveTotals=new double[4];
            for(int seedIndex=0;seedIndex<seeds;seedIndex++)
            {
                int seed=unchecked(seedIndex*104729+1907);
                var generator=new CoursePatternGenerator(seed);var replay=new CoursePatternGenerator(seed);
                PatternRow previous=default;
                int reachable=2,laneOnly=2,jumpOnly=2,slideOnly=2;
                double firstJump=-1,firstSlide=-1,previousJump=-1,previousSlide=-1,previousCompulsory=-1;
                bool laneFailed=false,jumpFailed=false,slideFailed=false;
                var signature=new StringBuilder();int phraseId=-1,phraseFamily=-1,phraseRows=0;
                for(int index=0;index<rowsPerSeed;index++)
                {
                    PatternRow row=generator.Next(),duplicate=replay.Next();
                    report.checkedRows++;motifs.Add(row.Pattern);
                    if(row.HazardCount==3)report.fullWidthRows++;if(row.Recovery)report.recoveryRows++;
                    string spatial=Layout(row,previous.Lane);rowLayouts.Add(spatial);
                    if(row.Family>=0)
                    {
                        families|=1<<row.Family;
                        if(row.Phrase!=phraseId)
                        {FinishPhrase(phraseLayouts,signature,phraseFamily,phraseRows);signature.Length=0;phraseRows=0;phraseId=row.Phrase;phraseFamily=row.Family;}
                        signature.Append(spatial).Append(';');phraseRows++;
                    }
                    if(row.Distance!=duplicate.Distance||row.Left!=duplicate.Left||row.Centre!=duplicate.Centre||
                        row.Right!=duplicate.Right||row.Lane!=duplicate.Lane||row.Action!=duplicate.Action||row.Family!=duplicate.Family||
                        row.PhraseStep!=duplicate.PhraseStep||row.Recovery!=duplicate.Recovery)
                        Fail(failures,seed,index,"seed replay changed");
                    if(row.Sequence!=index||row.Distance<=previous.Distance||row.Lane<-1||row.Lane>1)
                        Fail(failures,seed,index,"invalid row identity or distance");
                    if(Math.Abs(row.Lane-previous.Lane)>1)Fail(failures,seed,index,"route crosses two lanes in one beat");
                    CourseHazard intended=row.Action==RouteAction.Jump?CourseHazard.Jump:row.Action==RouteAction.Slide?CourseHazard.Slide:CourseHazard.Clear;
                    if(row.At(row.Lane)!=intended)Fail(failures,seed,index,"route action does not match obstacle");
                    bool actionGap=row.Action!=RouteAction.None||previous.Action!=RouteAction.None;
                    if(index>1)
                    {
                        int tier=row.DifficultyTier;report.rowsByTier[tier]++;
                        gapTotals[tier]+=row.Gap;hazardTotals[tier]+=row.HazardCount;
                        float seconds=row.Gap/RunTempo.CeilingAtDistance(row.Distance);secondsTotals[tier]+=seconds;
                        if(row.Lane!=previous.Lane)moveTotals[tier]++;
                        if(actionGap){report.minimumActionGap=Math.Min(report.minimumActionGap,row.Gap);report.minimumActionSeconds=Math.Min(report.minimumActionSeconds,seconds);}
                        else{report.minimumClearGap=Math.Min(report.minimumClearGap,row.Gap);report.minimumClearSeconds=Math.Min(report.minimumClearSeconds,seconds);}
                        float required=CoursePatternGenerator.RequiredGap(actionGap,row.Distance);
                        if(row.Gap<required||row.Gap>CoursePatternGenerator.MaximumGeneratedGap)Fail(failures,seed,index,"spacing exceeds its declared envelope");
                        if(row.Recovery&&row.Gap<required+6)Fail(failures,seed,index,"recovery beat has no additional space");
                    }
                    reachable=Advance(reachable,row,previous.Action,true,true);
                    laneOnly=Advance(laneOnly,row,RouteAction.None,false,false);
                    jumpOnly=Advance(jumpOnly,row,RouteAction.None,true,false);
                    slideOnly=Advance(slideOnly,row,RouteAction.None,false,true);
                    if((reachable&(1<<(row.Lane+1)))==0)Fail(failures,seed,index,"advertised route is unreachable");
                    PhysicalWitness(row,previous,report,failures,seed,index);
                    if(!laneFailed&&laneOnly==0){laneFailed=true;report.laneOnlyFailures++;report.worstLaneOnlySurvivalDistance=Math.Max(report.worstLaneOnlySurvivalDistance,row.Distance);}
                    if(!jumpFailed&&jumpOnly==0){jumpFailed=true;report.jumpOnlyFailures++;report.worstJumpOnlySurvivalDistance=Math.Max(report.worstJumpOnlySurvivalDistance,row.Distance);}
                    if(!slideFailed&&slideOnly==0){slideFailed=true;report.slideOnlyFailures++;report.worstSlideOnlySurvivalDistance=Math.Max(report.worstSlideOnlySurvivalDistance,row.Distance);}
                    if(Uniform(row,CourseHazard.Jump))
                    {if(firstJump<0)firstJump=row.Distance;if(previousJump>=0)report.maximumCompulsoryJumpGap=Math.Max(report.maximumCompulsoryJumpGap,row.Distance-previousJump);previousJump=row.Distance;}
                    if(Uniform(row,CourseHazard.Slide))
                    {if(firstSlide<0)firstSlide=row.Distance;if(previousSlide>=0)report.maximumCompulsorySlideGap=Math.Max(report.maximumCompulsorySlideGap,row.Distance-previousSlide);previousSlide=row.Distance;}
                    if(row.Compulsory)
                    {if(previousCompulsory>=0)report.maximumGapBetweenAnyCompulsoryRows=Math.Max(report.maximumGapBetweenAnyCompulsoryRows,row.Distance-previousCompulsory);previousCompulsory=row.Distance;}
                    if(previousJump>=0&&row.Distance-previousJump>CoursePatternGenerator.MaximumCompulsoryActionGap)Fail(failures,seed,index,"compulsory jump absence exceeds bound");
                    if(previousSlide>=0&&row.Distance-previousSlide>CoursePatternGenerator.MaximumCompulsoryActionGap)Fail(failures,seed,index,"compulsory slide absence exceeds bound");
                    previous=row;
                }
                // The final partial phrase is deliberately excluded from diversity counts.
                if(firstJump>=0&&firstJump<=200&&firstSlide>=0&&firstSlide<=200)report.seedsRequiringJumpAndSlideBefore200++;
                else Fail(failures,seed,-1,"both gestures are not compulsory before200m");
                if(!laneFailed||!jumpFailed||!slideFailed)Fail(failures,seed,-1,"a restricted strategy survives the complete course");
            }
            for(int i=0;i<4;i++)if(report.rowsByTier[i]>0)
            {report.averageGapByTier[i]=(float)(gapTotals[i]/report.rowsByTier[i]);report.averageSecondsByTier[i]=(float)(secondsTotals[i]/report.rowsByTier[i]);report.averageHazardsByTier[i]=(float)(hazardTotals[i]/report.rowsByTier[i]);report.laneChangesPerRowByTier[i]=(float)(moveTotals[i]/report.rowsByTier[i]);}
            int uniqueFamilies=0;
            foreach(int mask in phraseLayouts.Values)if(mask!=0&&(mask&(mask-1))==0)uniqueFamilies|=mask;
            report.distinctFamilies=CountBits(families);report.familiesWithUniqueLayouts=CountBits(uniqueFamilies);
            report.distinctPhraseLayouts=phraseLayouts.Count;report.distinctRowLayouts=rowLayouts.Count;report.distinctMotifs=motifs.Count;
            if(report.distinctFamilies!=CoursePatternGenerator.FamilyCount||report.familiesWithUniqueLayouts!=CoursePatternGenerator.FamilyCount||
                report.distinctPhraseLayouts<40||report.distinctRowLayouts<20)
                failures.Add("Not all twelve families produce distinct final spatial phrases.");
            if(report.recoveryRows==0||report.fullWidthRows==0)failures.Add("No challenge/recovery variety.");
            if(report.rowsByTier[0]==0||report.rowsByTier[3]==0||report.averageSecondsByTier[3]>=report.averageSecondsByTier[0]*.94f||
                report.averageHazardsByTier[3]<report.averageHazardsByTier[0])
                failures.Add("Measured late-game temporal density does not exceed opening density.");
            if(report.worstLaneOnlySurvivalDistance>200||report.worstJumpOnlySurvivalDistance>200||report.worstSlideOnlySurvivalDistance>200)
                failures.Add("A restricted strategy survives beyond200m.");
            if(report.maximumCompulsoryJumpGap>CoursePatternGenerator.MaximumCompulsoryActionGap||report.maximumCompulsorySlideGap>CoursePatternGenerator.MaximumCompulsoryActionGap)
                failures.Add("Compulsory recurrence exceeds256m.");
            report.errors=failures.ToArray();report.passed=failures.Count==0;return report;
        }
        static int CountBits(int bits){int count=0;while(bits!=0){count+=bits&1;bits>>=1;}return count;}
        static int Advance(int source,PatternRow row,RouteAction previousAction,bool jump,bool slide)
        {
            int destination=0;
            for(int target=-1;target<=1;target++)for(int origin=-1;origin<=1;origin++)
            {
                if((source&(1<<(origin+1)))==0)continue;
                var cell=row.At(target);if((cell==CourseHazard.Jump&&!jump)||(cell==CourseHazard.Slide&&!slide))continue;
                float available=row.Gap/RunTempo.CeilingAtDistance(row.Distance);
                float laneBudget=.55f+Math.Abs(target-origin)*.14f+.12f+CommandDelay;
                float actionBudget=cell!=CourseHazard.Clear||previousAction!=RouteAction.None?CoursePatternGenerator.MinimumActionSeconds:0;
                if(available>=Math.Max(laneBudget,actionBudget))destination|=1<<(target+1);
            }
            return destination;
        }
        static float JumpFeet(float time)
        {
            if(time<0)return 0;
            if(time<=JumpRise){float t=time/JumpRise;return JumpHeight*(2*t-t*t);}
            float fall=(time-JumpRise)/JumpFall;return Math.Max(0,JumpHeight*(1-fall*fall));
        }
        static float SlideBody(float time)
        {
            if(time<0||time>=SlideSeconds)return 2.05f;
            float tuck=Math.Min(1,Math.Min(time/.17f,(SlideSeconds-time)/.17f));
            return .10f+2.05f-.55f*tuck;
        }
        static void PhysicalWitness(PatternRow row,PatternRow previous,CoursePatternAuditResult report,List<string> errors,int seed,int index)
        {
            // Lateral movement happens in the empty longitudinal region between rows.
            if(index>0&&row.Lane!=previous.Lane)
            {
                float afterPrevious=row.Gap-CoursePatternGenerator.LaneChangeLead;
                report.minimumLaneClearanceMetres=Math.Min(report.minimumLaneClearanceMetres,afterPrevious);
                if(afterPrevious<3.5f||CoursePatternGenerator.LaneChangeLead-CoursePatternGenerator.MaximumSpeed*(.14f+CommandDelay)<14f)
                    Fail(errors,seed,index,"lane transition overlaps a row cross-section");
            }
            for(int speedIndex=0;speedIndex<witnessSpeeds.Length;speedIndex++)
            {
                float speed=witnessSpeeds[speedIndex];
                if(speed>RunTempo.CeilingAtDistance(row.Distance)+.0001f)continue;
                float lead=row.Action==RouteAction.Jump?JumpRise:.42f;
                float release=previous.Action==RouteAction.Jump?JumpFall:previous.Action==RouteAction.Slide?SlideSeconds-.42f:0;
                if(index>0&&row.Action!=RouteAction.None)
                {
                    float recovery=row.Gap/speed-release-lead-CommandDelay;
                    report.minimumActionRecoverySeconds=Math.Min(report.minimumActionRecoverySeconds,recovery);
                    if(recovery<.25f)Fail(errors,seed,index,"the next gesture starts before recovery/read time");
                }
                if(row.Action==RouteAction.Jump)
                {
                    float halfCrossing=(.601f+.38f)/speed;
                    float clearance=Math.Min(JumpFeet(lead-CommandDelay-halfCrossing),JumpFeet(lead+halfCrossing))-1.9215f;
                    report.minimumJumpClearance=Math.Min(report.minimumJumpClearance,clearance);
                    if(clearance<.12f)Fail(errors,seed,index,"jump has insufficient body clearance");
                }
                else if(row.Action==RouteAction.Slide)
                {
                    float halfCrossing=(.645f+.38f)/speed;
                    float clearance=1.66f-Math.Max(SlideBody(lead-CommandDelay-halfCrossing),SlideBody(lead+halfCrossing));
                    report.minimumSlideClearance=Math.Min(report.minimumSlideClearance,clearance);
                    if(clearance<.04f)Fail(errors,seed,index,"slide shoulder intersects gate header");
                }
            }
        }
        static void Fail(List<string> failures,int seed,int row,string reason)
        {if(failures.Count<30)failures.Add("seed="+seed+" row="+row+": "+reason);}
    }
}

