namespace BoneVisQA.Services.Utilities;

public static class SM2Algorithm
{
    public static class Defaults
    {
        public const decimal InitialEaseFactor = 2.5m;
        public const int InitialInterval = 1;
        public const int SecondInterval = 6;
        public const int MaxInterval = 365;
        public const decimal MinEaseFactor = 1.3m;
        public const int PassQualityThreshold = 3;
    }

    public record SM2Result(
        decimal EaseFactor,
        int IntervalDays,
        int RepetitionCount,
        DateOnly NextReviewDate
    );

    public static SM2Result Calculate(
        decimal currentEaseFactor,
        int currentInterval,
        int currentRepetitions,
        int quality)
    {
        quality = Math.Clamp(quality, 0, 5);

        int newInterval;
        int newRepetitions;

        if (quality >= Defaults.PassQualityThreshold)
        {
            if (currentRepetitions == 0)
                newInterval = Defaults.InitialInterval;
            else if (currentRepetitions == 1)
                newInterval = Defaults.SecondInterval;
            else
                newInterval = (int)Math.Round(currentInterval * currentEaseFactor);

            newRepetitions = currentRepetitions + 1;
        }
        else
        {
            newRepetitions = 0;
            newInterval = Defaults.InitialInterval;
        }

        var newEaseFactor = Math.Max(
            Defaults.MinEaseFactor,
            currentEaseFactor + (0.1m - (5 - quality) * (0.08m + (5 - quality) * 0.02m)));

        newInterval = Math.Min(Defaults.MaxInterval, Math.Max(1, newInterval));
        var nextReviewDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(newInterval));

        return new SM2Result(
            EaseFactor: newEaseFactor,
            IntervalDays: newInterval,
            RepetitionCount: newRepetitions,
            NextReviewDate: nextReviewDate
        );
    }

    public static SM2Result GetInitialValues()
    {
        var nextReviewDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(Defaults.InitialInterval));
        return new SM2Result(
            EaseFactor: Defaults.InitialEaseFactor,
            IntervalDays: Defaults.InitialInterval,
            RepetitionCount: 0,
            NextReviewDate: nextReviewDate
        );
    }
}
