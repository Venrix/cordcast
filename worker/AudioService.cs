using ManagedBass;

namespace CordCastWorker;

/// <summary>
/// Manages local audio device capture (mic → Discord) and playback (Discord → speakers).
/// </summary>
public class AudioService : IDisposable
{
    private int _recordHandle;
    private int _playbackStream; // push stream for Discord → speaker playback
    private bool _initialized;
    private bool _speakActive;
    private bool _listenActive;

    private string? _recordingDevice;
    private string? _playbackDevice;
    private bool _thresholdEnabled;
    private double _threshold;

    // Called by BotService to push 20ms PCM frames to Discord.
    public event EventHandler<byte[]>? AudioFrameReady;

    // Called by BotService with received PCM from Discord to play back locally.
    public void ReceiveDiscordAudio(byte[] pcm)
    {
        if (!_listenActive || _playbackStream == 0) return;
        Bass.StreamPutData(_playbackStream, pcm, pcm.Length);
    }

    public void Init()
    {
        if (_initialized) return;
        Bass.Init();
        _initialized = true;
    }

    public List<(string Id, string Name)> GetRecordingDevices()
    {
        var seen = new HashSet<string>();
        var list = new List<(string, string)>();
        var count = Bass.RecordingDeviceCount;
        for (int i = 0; i < count; i++)
        {
            var info = Bass.RecordGetDeviceInfo(i);
            if (info.IsEnabled && seen.Add(info.Name))
                list.Add((i.ToString(), info.Name));
        }
        return list;
    }

    public List<(string Id, string Name)> GetPlaybackDevices()
    {
        var seen = new HashSet<string>();
        var list = new List<(string, string)>();
        var count = Bass.DeviceCount;
        for (int i = 0; i < count; i++)
        {
            var info = Bass.GetDeviceInfo(i);
            if (info.IsEnabled && seen.Add(info.Name))
                list.Add((i.ToString(), info.Name));
        }
        return list;
    }

    public void SetSpeak(bool enabled, string? deviceName, bool thresholdEnabled, double threshold)
    {
        _thresholdEnabled = thresholdEnabled;
        _threshold = threshold;
        _recordingDevice = deviceName;

        if (enabled && !_speakActive)
            StartRecording();
        else if (!enabled && _speakActive)
            StopRecording();
    }

    public void SetListen(bool enabled, string? deviceName)
    {
        _playbackDevice = deviceName;

        if (enabled && !_listenActive)
            StartPlayback();
        else if (!enabled && _listenActive)
            StopPlayback();
    }

    private void StartRecording()
    {
        var deviceIndex = FindRecordingDevice(_recordingDevice);
        Bass.RecordInit(deviceIndex);

        // 48000 Hz stereo 16-bit, 20ms callback for Discord frame alignment
        _recordHandle = Bass.RecordStart(48000, 2, BassFlags.Default, 20, RecordCallback, 0);
        _speakActive = _recordHandle != 0;
    }

    private bool RecordCallback(int handle, IntPtr buffer, int length, IntPtr user)
    {
        if (!_speakActive) return false;

        var pcm = new byte[length];
        System.Runtime.InteropServices.Marshal.Copy(buffer, pcm, 0, length);

        if (_thresholdEnabled && !PassesThreshold(pcm, _threshold))
            return true; // gate — send silence implicitly (Discord handles comfort noise)

        AudioFrameReady?.Invoke(this, pcm);
        return true;
    }

    private static bool PassesThreshold(byte[] pcm, double threshold)
    {
        double sumSq = 0;
        for (int i = 0; i < pcm.Length - 1; i += 2)
        {
            short sample = (short)(pcm[i] | (pcm[i + 1] << 8));
            double normalized = sample / 32768.0;
            sumSq += normalized * normalized;
        }
        double rms = Math.Sqrt(sumSq / (pcm.Length / 2));
        return rms >= threshold;
    }

    private void StopRecording()
    {
        if (_recordHandle != 0)
        {
            Bass.ChannelStop(_recordHandle);
            _recordHandle = 0;
        }
        _speakActive = false;
    }

    private void StartPlayback()
    {
        var deviceIndex = FindPlaybackDevice(_playbackDevice);
        Bass.Init(deviceIndex, 48000);

        // Create a push stream: Discord PCM → local speakers
        _playbackStream = Bass.CreateStream(48000, 2, BassFlags.Default, StreamProcedureType.Push);
        if (_playbackStream != 0)
        {
            Bass.ChannelPlay(_playbackStream);
            _listenActive = true;
        }
    }

    private void StopPlayback()
    {
        if (_playbackStream != 0)
        {
            Bass.StreamFree(_playbackStream);
            _playbackStream = 0;
        }
        _listenActive = false;
    }

    private static int FindRecordingDevice(string? name)
    {
        if (name is null) return -1;
        var count = Bass.RecordingDeviceCount;
        for (int i = 0; i < count; i++)
        {
            var info = Bass.RecordGetDeviceInfo(i);
            if (info.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1; // default device
    }

    private static int FindPlaybackDevice(string? name)
    {
        if (name is null) return -1;
        var count = Bass.DeviceCount;
        for (int i = 0; i < count; i++)
        {
            var info = Bass.GetDeviceInfo(i);
            if (info.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public void Dispose()
    {
        StopRecording();
        StopPlayback();
        if (_initialized)
        {
            Bass.RecordFree();
            Bass.Free();
        }
    }
}
