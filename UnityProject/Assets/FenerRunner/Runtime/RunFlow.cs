using System;

namespace FenerRunner
{
    // One run's skill rewards. Currency rewards never feed the pickup counter.
    public sealed class RunFlow
    {
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public int CompletedChallenges { get; private set; }
        public int BonusCoins { get; private set; }
        public float BurstRemaining { get; private set; }
        public float Multiplier => 1f + Math.Min(10, Math.Max(0, Streak - 3)) * .1f;
        public float SpeedBonus => BurstRemaining > 0f ? 1f : 0f;
        public int ChallengeKind => CompletedChallenges % 3;
        public int ChallengeTarget => ChallengeKind == 0 ? 4 + Math.Min(4, CompletedChallenges / 3) : ChallengeKind == 1 ? 20 + Math.Min(20, CompletedChallenges / 3 * 5) : 350 + Math.Min(350, CompletedChallenges / 3 * 50);
        public int ChallengeReward => 15 + Math.Min(25, CompletedChallenges * 5);
        public string ChallengeLabel => ChallengeKind == 0 ? "TEMİZ GEÇİŞ" : ChallengeKind == 1 ? "JETON AVI" : "MESAFE USTASI";
        public int ChallengeProgress => Math.Min(ChallengeTarget, ChallengeKind == 0 ? cleanProgress : ChallengeKind == 1 ? pickupProgress : (int)distanceProgress);
        int cleanProgress, pickupProgress;
        double distanceProgress;

        public void Reset()
        {
            Streak = BestStreak = CompletedChallenges = BonusCoins = 0;
            BurstRemaining = 0; ResetChallenge();
        }
        public void Tick(float seconds) { BurstRemaining = Math.Max(0f, BurstRemaining - Math.Max(0f, seconds)); }
        public void BreakStreak() { Streak = 0; BurstRemaining = 0; }
        public int CleanPass()
        {
            Streak++; BestStreak = Math.Max(BestStreak, Streak);
            if (Streak % 8 == 0) BurstRemaining = 6f;
            if (ChallengeKind == 0) { cleanProgress++; CompleteIfReady(); }
            return (int)Math.Round((Multiplier - 1f) * 100f);
        }
        public void Collect(int actualPickups)
        {
            if (ChallengeKind != 1 || actualPickups <= 0) return;
            pickupProgress += actualPickups; CompleteIfReady();
        }
        public void Travel(double metres)
        {
            if (ChallengeKind != 2 || metres <= 0 || double.IsNaN(metres) || double.IsInfinity(metres)) return;
            distanceProgress += metres; CompleteIfReady();
        }
        void CompleteIfReady()
        {
            if (ChallengeProgress < ChallengeTarget) return;
            BonusCoins += ChallengeReward; CompletedChallenges++; ResetChallenge();
        }
        void ResetChallenge() { cleanProgress = pickupProgress = 0; distanceProgress = 0; }
    }
}
