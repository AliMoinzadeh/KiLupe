using System.IO;
using System.Linq;
using System.Threading;
using KiLupeDemo.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace KiLupeDemo.Services;

public sealed class OnnxTextCorrectionService : ITextCorrectionService
{
    private const int MaxInputTokens = 256;
    private const int MaxGeneratedTokens = 128;
    private const long PadTokenId = 0;
    private const long EndOfSentenceTokenId = 1;

    private readonly InferenceSession? modelSession;
    private readonly SentencePieceTokenizer? tokenizer;
    private readonly object inferenceLock = new();
    private readonly string modelDirectory;
    private readonly string effectiveProvider;

    public OnnxTextCorrectionService(
        string modelPath,
        string? tokenizerDirectory,
        InferenceProviderKind provider)
    {
        ModelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
        modelDirectory = Directory.Exists(modelPath)
            ? modelPath
            : Path.GetDirectoryName(modelPath) ?? string.Empty;
        TokenizerDirectory = tokenizerDirectory ?? modelDirectory;
        effectiveProvider = InferenceProviderResolver.GetRequestedName(provider);

        try
        {
            var modelFile = FindModelFile("model.onnx");
            if (modelFile is null)
            {
                StatusText = $"Korrekturmodell fehlt: {modelDirectory}";
                return;
            }

            var tokenizerJsonPath = Path.Combine(TokenizerDirectory, "tokenizer.json");
            if (!File.Exists(tokenizerJsonPath))
            {
                StatusText = $"Tokenizer fehlt: {tokenizerJsonPath}";
                return;
            }

            using (var tokenizerStream = File.OpenRead(tokenizerJsonPath))
            {
                tokenizer = SentencePieceTokenizer.CreateFromTokenizerJson(
                    tokenizerStream,
                    addBeginningOfSentence: false,
                    addEndOfSentence: false);
            }

            modelSession = InferenceProviderResolver.CreateSession(
                modelFile,
                provider,
                out effectiveProvider);
            ValidateModelInputs(modelSession);
            StatusText = $"Textkorrektur bereit ({effectiveProvider}).";
        }
        catch (Exception exception)
        {
            modelSession?.Dispose();
            tokenizer = null;
            StatusText = $"Korrekturmodell konnte nicht geladen werden: {exception.Message}";
        }
    }

    public string Name => "Deutsche Textkorrektur";

    public string ModelId => "german-spelling-correction-onnx";

    public string ModelPath { get; }

    public string TokenizerDirectory { get; }

    public bool IsAvailable => modelSession is not null
        && tokenizer is not null;

    public string StatusText { get; }

    public Task<CorrectionSuggestion?> CorrectAsync(
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!IsAvailable || string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult<CorrectionSuggestion?>(null);
        }

        return Task.Run(
            () => Correct(text, cancellationToken),
            cancellationToken);
    }

    public void Dispose()
    {
        modelSession?.Dispose();
    }

    private CorrectionSuggestion? Correct(
        string text,
        CancellationToken cancellationToken)
    {
        lock (inferenceLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prompt = $"correct: {text}";
            var inputIds = tokenizer!
                .EncodeToIds(prompt)
                .Take(MaxInputTokens)
                .Select(Convert.ToInt64)
                .ToArray();
            if (inputIds.Length == 0)
            {
                return null;
            }

            var inputTensor = new DenseTensor<long>(
                inputIds,
                new[] { 1, inputIds.Length });
            var attentionTensor = new DenseTensor<long>(
                Enumerable.Repeat(1L, inputIds.Length).ToArray(),
                new[] { 1, inputIds.Length });
            var generatedIds = Generate(
                inputTensor,
                attentionTensor,
                cancellationToken);
            var correctedText = tokenizer.Decode(generatedIds.Select(id => (int)id)).Trim();
            if (string.IsNullOrWhiteSpace(correctedText)
                || string.Equals(text, correctedText, StringComparison.Ordinal))
            {
                return null;
            }

            return new CorrectionSuggestion(
                text,
                correctedText,
                Name,
                effectiveProvider);
        }
    }

    private IReadOnlyList<long> Generate(
        DenseTensor<long> inputIds,
        DenseTensor<long> encoderAttention,
        CancellationToken cancellationToken)
    {
        var generated = new List<long> { PadTokenId };
        for (var step = 0; step < MaxGeneratedTokens; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decoderIds = new DenseTensor<long>(
                generated.ToArray(),
                new[] { 1, generated.Count });
            var decoderAttention = new DenseTensor<long>(
                Enumerable.Repeat(1L, generated.Count).ToArray(),
                new[] { 1, generated.Count });
            using var modelOutputs = modelSession!.Run(
                CreateModelInputs(
                    inputIds,
                    encoderAttention,
                    decoderIds,
                    decoderAttention));
            var logits = FindLogitsOutput(modelOutputs);
            var nextToken = FindLastToken(logits);
            if (nextToken == EndOfSentenceTokenId || nextToken == PadTokenId)
            {
                break;
            }

            generated.Add(nextToken);
        }

        return generated.Skip(1).ToArray();
    }

    private IReadOnlyList<NamedOnnxValue> CreateModelInputs(
        DenseTensor<long> inputIds,
        DenseTensor<long> attentionMask,
        DenseTensor<long> decoderIds,
        DenseTensor<long> decoderAttention)
    {
        return modelSession!.InputMetadata
            .Select(input =>
            {
                var name = input.Key;
                if (Contains(name, "decoder_input_ids"))
                {
                    return NamedOnnxValue.CreateFromTensor(name, decoderIds);
                }

                if (Contains(name, "decoder_attention_mask"))
                {
                    return NamedOnnxValue.CreateFromTensor(name, decoderAttention);
                }

                if (Contains(name, "input_ids"))
                {
                    return NamedOnnxValue.CreateFromTensor(name, inputIds);
                }

                if (Contains(name, "attention_mask"))
                {
                    return NamedOnnxValue.CreateFromTensor(name, attentionMask);
                }

                throw new NotSupportedException($"Nicht unterstützter Korrektur-Input: {name}");
            })
            .ToArray();
    }

    private static Tensor<float> FindLogitsOutput(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs)
    {
        foreach (var output in outputs)
        {
            if (output.Value is not Tensor<float> tensor)
            {
                continue;
            }

            var dimensions = tensor.Dimensions.ToArray();
            if (dimensions.Length == 3 && dimensions[^1] >= 10000)
            {
                return tensor;
            }
        }

        throw new InvalidOperationException(
            "ONNX-Korrekturmodell liefert keine Vokabular-Logits.");
    }

    private static long FindLastToken(Tensor<float> logits)
    {
        var dimensions = logits.Dimensions.ToArray();
        if (dimensions.Length == 2)
        {
            return FindBestToken(logits, 0, dimensions[1]);
        }

        if (dimensions.Length != 3 || dimensions[0] != 1)
        {
            throw new InvalidOperationException("Decoder-Logits haben eine unbekannte Form.");
        }

        return FindBestToken(logits, dimensions[1] - 1, dimensions[2]);
    }

    private static long FindBestToken(
        Tensor<float> logits,
        int sequenceIndex,
        int vocabularySize)
    {
        var dimensions = logits.Dimensions.ToArray();
        var values = logits.ToArray();
        var bestToken = 0;
        var bestScore = float.MinValue;
        for (var token = 0; token < vocabularySize; token++)
        {
            var index = dimensions.Length == 2
                ? token
                : sequenceIndex * vocabularySize + token;
            if (values[index] > bestScore)
            {
                bestScore = values[index];
                bestToken = token;
            }
        }

        return bestToken;
    }

    private static void ValidateModelInputs(InferenceSession session)
    {
        var requiredInputs = new[]
        {
            "input_ids",
            "attention_mask",
            "decoder_input_ids",
            "decoder_attention_mask"
        };
        if (requiredInputs.Any(required => !session.InputMetadata.Keys.Any(name =>
                string.Equals(name, required, StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException(
                "ONNX-Korrekturmodell benoetigt Encoder- und Decoder-Eingaben.");
        }

        if (!session.OutputMetadata.Keys.Any(name =>
                string.Equals(name, "logits", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "ONNX-Korrekturmodell liefert keinen Logit-Ausgang.");
        }
    }

    private string? FindModelFile(string fileName)
    {
        if (File.Exists(ModelPath)
            && string.Equals(Path.GetFileName(ModelPath), fileName, StringComparison.OrdinalIgnoreCase))
        {
            return ModelPath;
        }

        var path = Path.Combine(modelDirectory, fileName);
        return File.Exists(path) ? path : null;
    }

    private static bool Contains(string value, string part)
    {
        return value.Contains(part, StringComparison.OrdinalIgnoreCase);
    }
}