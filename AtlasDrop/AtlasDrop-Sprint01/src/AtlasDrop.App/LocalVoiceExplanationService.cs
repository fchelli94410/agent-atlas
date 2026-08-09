using System.Text;
using NAudio.Wave;
using Whisper.net;
using Whisper.net.Ggml;

namespace AtlasDrop.App;

internal sealed class LocalVoiceExplanationService : IDisposable
{
    private readonly string _modelDirectory;
    private WaveInEvent? _recorder;
    private WaveFileWriter? _writer;
    private string? _recordingPath;

    public LocalVoiceExplanationService(string stateDirectory)
    {
        _modelDirectory = Path.Combine(stateDirectory, "voice-models");
    }

    public bool IsRecording => _recorder is not null;

    public void StartRecording()
    {
        if (IsRecording) return;

        Directory.CreateDirectory(_modelDirectory);
        _recordingPath = Path.Combine(Path.GetTempPath(), $"atlasdrop-voice-{Guid.NewGuid():N}.wav");
        _recorder = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 100
        };
        _writer = new WaveFileWriter(_recordingPath, _recorder.WaveFormat);
        _recorder.DataAvailable += (_, args) => _writer?.Write(args.Buffer, 0, args.BytesRecorded);
        _recorder.StartRecording();
    }

    public async Task<string> StopAndTranscribeAsync(IProgress<string>? progress = null)
    {
        if (_recorder is null || string.IsNullOrWhiteSpace(_recordingPath))
            return string.Empty;

        var recorder = _recorder;
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        recorder.RecordingStopped += (_, _) => stopped.TrySetResult();
        recorder.StopRecording();
        await stopped.Task;

        recorder.Dispose();
        _recorder = null;
        _writer?.Dispose();
        _writer = null;

        var modelPath = Path.Combine(_modelDirectory, "ggml-small.bin");
        if (!File.Exists(modelPath))
        {
            progress?.Report("Premier usage : téléchargement unique du modèle vocal français…");
            var temporaryModel = modelPath + ".download";
            try
            {
                using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Small);
                await using var fileWriter = File.Create(temporaryModel);
                await modelStream.CopyToAsync(fileWriter);
                fileWriter.Close();
                File.Move(temporaryModel, modelPath, true);
            }
            finally
            {
                if (File.Exists(temporaryModel)) File.Delete(temporaryModel);
            }
        }

        progress?.Report("Analyse locale de ton explication…");
        using var factory = WhisperFactory.FromPath(modelPath);
        using var processor = factory.CreateBuilder()
            .WithLanguage("fr")
            .Build();
        await using var audio = File.OpenRead(_recordingPath);
        var transcription = new StringBuilder();
        await foreach (var segment in processor.ProcessAsync(audio))
            transcription.Append(' ').Append(segment.Text.Trim());

        try { File.Delete(_recordingPath); } catch { }
        _recordingPath = null;
        return transcription.ToString().Trim();
    }

    public void Dispose()
    {
        try { _recorder?.StopRecording(); } catch { }
        _recorder?.Dispose();
        _writer?.Dispose();
        if (!string.IsNullOrWhiteSpace(_recordingPath))
        {
            try { File.Delete(_recordingPath); } catch { }
        }
    }
}
