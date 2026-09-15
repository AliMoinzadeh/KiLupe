using System.Text;
using System.IO;
using KiLupeDemo.Models;
using WeCantSpell.Hunspell;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLama.Sampling;

namespace KiLupeDemo.Services;

public sealed class LocalLlamaTextCorrectionService : ITextCorrectionService
{
    private const uint ContextSize = 1024;
    private const int MaxGeneratedTokens = 128;
    private const string ProviderName = "LLamaSharp CPU";
    private const string SystemPrompt =
        "Du bist ein sorgfaeltiger deutscher Korrektor. "
        + "Korrigiere nur Rechtschreibung, Grammatik, Gross-/Kleinschreibung und Zeichensetzung. "
        + "Behalte Inhalt, Namen und Zahlen. Gib nur den korrigierten deutschen Text aus, "
        + "ohne Erklaerung, Markdown oder Alternativen.";

    private readonly Lazy<Func<string, bool>> isMisspelled = new(LoadSpellingCheck);
    private readonly object stateLock = new();
    private readonly SemaphoreSlim inferenceGate = new(1, 1);
    private readonly ModelParams modelParameters;
    private LLamaWeights? weights;
    private bool loadFailed;
    private int disposed;
    private string statusText;

    public LocalLlamaTextCorrectionService(string modelPath, bool contextAwareCorrection = false)
    {
        ContextAwareCorrection = contextAwareCorrection;
        ModelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
        modelParameters = new ModelParams(ModelPath)
        {
            ContextSize = ContextSize,
            GpuLayerCount = 0
        };
        statusText = File.Exists(modelPath)
            ? "Textkorrekturmodell wird bei der ersten Anfrage geladen."
            : $"Korrekturmodell fehlt: {modelPath}";
    }

    public string Name => "Deutsches lokales LLM";

    public string ModelId => "qwen2.5-3b-instruct-gguf";

    public string ModelPath { get; }

    public bool ContextAwareCorrection { get; }

    public bool IsAvailable => Volatile.Read(ref disposed) == 0
        && !loadFailed
        && File.Exists(ModelPath);

    public string StatusText => statusText;

    public static string BuildCorrectionPrompt(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"{SystemPrompt}\n\nOCR-Zeile:\n{text}";
    }

    public Task<CorrectionSuggestion?> CorrectAsync(
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text) || !IsAvailable)
        {
            return Task.FromResult<CorrectionSuggestion?>(null);
        }

        return Task.Run(
            () => CorrectCoreAsync(text, cancellationToken),
            cancellationToken);
    }

    public Task<string> PredictAsync(string before, string after, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            await inferenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var loadedWeights = GetOrLoadWeights();
                if (loadedWeights is null) throw new InvalidOperationException(StatusText);
                var prompt = PredictionPrompt.Create(before, after,
                    text => loadedWeights.Tokenize(text, false, true, Encoding.UTF8).Length);
                if (prompt is null) return "";
                var executor = new StatelessExecutor(loadedWeights, modelParameters) { ApplyTemplate = false };
                using var sampling = new DefaultSamplingPipeline
                {
                    Temperature = 0f, TopP = 1f, TopK = 0, RepeatPenalty = 1f, Seed = 42
                };
                var parameters = new InferenceParams
                {
                    MaxTokens = 40, SamplingPipeline = sampling,
                    AntiPrompts = new[] { "<|im_end|>", "<|endoftext|>", "<|im_start|>", "\n" }
                };
                var response = new StringBuilder();
                await foreach (var chunk in executor.InferAsync(prompt.Text, parameters, cancellationToken))
                    response.Append(chunk);
                cancellationToken.ThrowIfCancellationRequested();
                return prompt.ReadCompletion(response.ToString());
            }
            finally { inferenceGate.Release(); }
        }, cancellationToken);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        inferenceGate.Wait();
        try
        {
            lock (stateLock)
            {
                weights?.Dispose();
                weights = null;
            }
        }
        finally
        {
            inferenceGate.Release();
        }

        GC.SuppressFinalize(this);
    }

    private async Task<CorrectionSuggestion?> CorrectCoreAsync(
        string text,
        CancellationToken cancellationToken)
    {
        await inferenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ContextAwareCorrection && !text.Trim().Any(char.IsWhiteSpace) && !isMisspelled.Value(text))
                return null;
            var loadedWeights = GetOrLoadWeights();
            if (loadedWeights is null)
            {
                return null;
            }

            using var context = loadedWeights.CreateContext(modelParameters);
            var executor = new InteractiveExecutor(context);
            var history = new ChatHistory();
            history.AddMessage(AuthorRole.System, ContextAwareCorrection ? ContextualCorrectionPolicy.SystemPrompt : SystemPrompt);
            var session = new ChatSession(executor, history)
            {
                HistoryTransform = new QwenChatHistoryTransform()
            };
            using var samplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.1f,
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.1f
            };
            var inferenceParameters = new InferenceParams
            {
                MaxTokens = MaxGeneratedTokens,
                AntiPrompts = new[]
                {
                    "<|im_end|>",
                    "<|endoftext|>",
                    "<|im_start|>user",
                    "<|im_start|>system"
                },
                SamplingPipeline = samplingPipeline
            };
            var response = new StringBuilder();
            var userMessage = new ChatHistory.Message(
                AuthorRole.User,
                text);

            await foreach (var chunk in session.ChatAsync(
                userMessage,
                inferenceParameters,
                cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                response.Append(chunk);
            }

            var correctedText = NormalizeResponse(response.ToString());
            if (ContextAwareCorrection)
                correctedText = ContextualCorrectionPolicy.ReadCorrection(text, correctedText, isMisspelled.Value);
            if (string.IsNullOrWhiteSpace(correctedText)
                || IsRoleMarkerOnly(correctedText)
                || string.Equals(text, correctedText, StringComparison.Ordinal))
            {
                return null;
            }

            SetStatus("Textkorrektur bereit (LLamaSharp CPU).");
            return new CorrectionSuggestion(
                text,
                correctedText,
                Name,
                ProviderName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SetStatus($"Lokales LLM konnte nicht ausgefuehrt werden: {exception.Message}");
            return null;
        }
        finally
        {
            inferenceGate.Release();
        }
    }

    private LLamaWeights? GetOrLoadWeights()
    {
        lock (stateLock)
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return null;
            }

            if (weights is not null)
            {
                return weights;
            }

            if (!File.Exists(ModelPath))
            {
                loadFailed = true;
                SetStatus($"Korrekturmodell fehlt: {ModelPath}");
                return null;
            }

            try
            {
                weights = LLamaWeights.LoadFromFile(modelParameters);
                loadFailed = false;
                SetStatus("Textkorrekturmodell geladen (LLamaSharp CPU).");
                return weights;
            }
            catch (Exception exception)
            {
                loadFailed = true;
                SetStatus($"Korrekturmodell konnte nicht geladen werden: {exception.Message}");
                return null;
            }
        }
    }

    private static string NormalizeResponse(string response)
    {
        var normalized = response.Trim();
        foreach (var marker in new[] { "<|im_end|>", "<|endoftext|>" })
        {
            var markerIndex = normalized.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                normalized = normalized[..markerIndex].Trim();
            }
        }

        foreach (var prefix in new[] { "Antwort:", "Korrigierter Text:" })
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..].Trim();
                break;
            }
        }

        const string assistantMarker = "<|im_start|>assistant";
        if (normalized.StartsWith(assistantMarker, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[assistantMarker.Length..].Trim();
        }

        if (normalized.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = normalized.IndexOf('\n');
            if (firstLineEnd >= 0)
            {
                normalized = normalized[(firstLineEnd + 1)..].Trim();
            }

            if (normalized.EndsWith("```", StringComparison.Ordinal))
            {
                normalized = normalized[..^3].Trim();
            }
        }

        return normalized;
    }

    private static bool IsRoleMarkerOnly(string text)
    {
        return text.Equals("User:", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Assistant:", StringComparison.OrdinalIgnoreCase)
            || text.Equals("user", StringComparison.OrdinalIgnoreCase)
            || text.Equals("assistant", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class QwenChatHistoryTransform : IHistoryTransform
    {
        public string HistoryToText(ChatHistory history)
        {
            var prompt = new StringBuilder();
            foreach (var message in history.Messages)
            {
                prompt.Append("<|im_start|>")
                    .Append(GetRoleName(message.AuthorRole))
                    .Append('\n')
                    .Append(message.Content)
                    .Append("<|im_end|>\n");
            }

            if (history.Messages.Count == 0
                || history.Messages[^1].AuthorRole != AuthorRole.Assistant)
            {
                prompt.Append("<|im_start|>assistant\n");
            }

            return prompt.ToString();
        }

        public ChatHistory TextToHistory(AuthorRole role, string text)
        {
            var history = new ChatHistory();
            history.AddMessage(role, text);
            return history;
        }

        public IHistoryTransform Clone()
        {
            return new QwenChatHistoryTransform();
        }

        private static string GetRoleName(AuthorRole role)
        {
            return role switch
            {
                AuthorRole.System => "system",
                AuthorRole.User => "user",
                AuthorRole.Assistant => "assistant",
                _ => "user"
            };
        }
    }

    private static Func<string, bool> LoadSpellingCheck()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var dictionaries = new List<WordList>();
        foreach (var name in new[] { "de_DE", "en_US" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "dictionaries", name + ".dic");
            if (!File.Exists(path) || !File.Exists(Path.ChangeExtension(path, ".aff")))
                continue;
            try { dictionaries.Add(WordList.CreateFromFiles(path)); }
            catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
            {
                // Missing/unreadable dictionaries must not permit grammar edits in fragments.
            }
        }
        var checker = new SpellingChecker(word => dictionaries.Any(dictionary => dictionary.Check(word)), null);
        return word => dictionaries.Count > 0 && checker.IsMisspelled(word);
    }
    private void SetStatus(string value)
    {
        lock (stateLock)
        {
            statusText = value;
        }
    }
}