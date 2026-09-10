using KiLupeDemo.Models;
using Microsoft.ML.OnnxRuntime;

namespace KiLupeDemo.Services;

public static class InferenceProviderResolver
{
    public static string GetRequestedName(InferenceProviderKind provider)
    {
        return provider switch
        {
            InferenceProviderKind.DirectMl => "DirectML",
            InferenceProviderKind.Cpu => "CPU",
            _ => "Auto"
        };
    }

    public static InferenceSession CreateSession(
        string modelPath,
        InferenceProviderKind provider,
        out string effectiveProvider)
    {
        if (provider == InferenceProviderKind.Cpu)
        {
            effectiveProvider = "CPU";
            return new InferenceSession(modelPath);
        }

        try
        {
            using var options = new SessionOptions();
            options.AppendExecutionProvider_DML(0);
            effectiveProvider = "DirectML";
            return new InferenceSession(modelPath, options);
        }
        catch when (provider == InferenceProviderKind.Auto)
        {
            effectiveProvider = "CPU-Fallback";
            return new InferenceSession(modelPath);
        }
    }
}