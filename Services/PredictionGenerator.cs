namespace KiLupeDemo.Services;

public static class PredictionGenerator
{
    public static Task<string> GenerateAsync(string before, string after, PredictionSettings settings,
        Func<string, string, bool, CancellationToken, Task<string>>? predict, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (settings.CompleteCalculations && ArithmeticPrediction.TryComplete(before, after, out var result))
            return Task.FromResult(result ?? "");
        if (settings.CompleteCalculations && NumberSequencePrediction.TryComplete(before, after, out var nextNumber))
            return Task.FromResult(nextNumber ?? "");
        if (settings.OnlyCurrentSentence && PredictionPrompt.EndsSentence(before)) return Task.FromResult("");
        return predict is null ? Task.FromResult("") : predict(before, after, settings.OnlyCurrentSentence, cancellationToken);
    }
}
