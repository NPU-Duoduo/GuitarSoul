using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Runtime.InteropServices;

namespace GuitarSoul.Audio.Windows;

public static class AudioDevices
{
    public static IReadOnlyList<AudioDeviceInfo> ListInputs() => ListSources(AudioSourceKind.GuitarInput);
    public static IReadOnlyList<AudioDeviceInfo> ListOutputs() => ListSources(AudioSourceKind.ComputerPlayback);
    public static IReadOnlyList<AudioDeviceInfo> ListSources(AudioSourceKind sourceKind) => List(GetSourceDataFlow(sourceKind));

    internal static DataFlow GetSourceDataFlow(AudioSourceKind sourceKind) => sourceKind switch
    {
        AudioSourceKind.GuitarInput => DataFlow.Capture,
        AudioSourceKind.ComputerPlayback => DataFlow.Render,
        _ => throw new ArgumentOutOfRangeException(nameof(sourceKind), "请选择有效的音频来源。")
    };

    public static string DefaultOutputId()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return device.ID;
    }

    private static IReadOnlyList<AudioDeviceInfo> List(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var result = new List<AudioDeviceInfo>();
        foreach (var endpoint in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (endpoint)
            {
                try
                {
                    using var client = endpoint.AudioClient;
                    var format = client.MixFormat;
                    result.Add(new(endpoint.ID, endpoint.FriendlyName, format.Channels, format.SampleRate, format.BitsPerSample));
                }
                catch (COMException) { /* The endpoint may disappear during enumeration. */ }
            }
        }
        return result.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal static MMDevice Open(string id, DataFlow expectedFlow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        using var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(id);
        if (device.DataFlow != expectedFlow || device.State != DeviceState.Active)
        {
            device.Dispose();
            throw new InvalidOperationException("所选音频设备已不可用，请重新选择。程序不会切换到默认设备。");
        }
        return device;
    }
}

/// <summary>Notifications never enumerate devices or call subscribers on a Windows audio callback.</summary>
public sealed class AudioDeviceWatcher : IMMNotificationClient, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private int _disposed;
    public event EventHandler<AudioDeviceChangedEventArgs>? DevicesChanged;

    public AudioDeviceWatcher() => _enumerator.RegisterEndpointNotificationCallback(this);

    private void Notify(string? id, string reason, bool unavailable = false)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            if (Volatile.Read(ref _disposed) == 0)
                DevicesChanged?.Invoke(this, new(id, reason, unavailable));
        });
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) =>
        Notify(deviceId, "state", newState != DeviceState.Active);
    public void OnDeviceAdded(string deviceId) => Notify(deviceId, "added");
    public void OnDeviceRemoved(string deviceId) => Notify(deviceId, "removed", true);
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => Notify(defaultDeviceId, "default");
    public void OnPropertyValueChanged(string deviceId, PropertyKey key) => Notify(deviceId, "property");

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _enumerator.UnregisterEndpointNotificationCallback(this); }
        finally { _enumerator.Dispose(); }
    }
}
