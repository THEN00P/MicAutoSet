using System;

namespace DontTouchMyMic.Models
{
    internal sealed class CachedMicrophoneEntry
    {
        public Guid DeviceId { get; set; }
        public Guid? EndpointGuid { get; set; }
        public string RealId { get; set; } = string.Empty;
        public string InterfaceName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Volume { get; set; }
    }

    internal sealed class CachedMicrophoneSnapshot
    {
        public Guid DeviceId { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsConnected { get; init; }
        public bool IsDefault { get; init; }
    }
}
