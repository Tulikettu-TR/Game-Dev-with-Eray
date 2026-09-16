using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clawbles
{
    // 16 kHz mono mu-law, 20 ms packets: 16 KB/s per active talker before transport overhead.
    // Only the V key starts microphone capture. Automated tests feed synthetic samples instead.
    public sealed class ClawVoice : MonoBehaviour
    {
        public const int SampleRate = 16000, FrameSamples = 320;
        public const float Range = 30;
        public ClawSession Session;
        public Action<byte[], uint> SendFrame;
        public bool Muted;
        public float Volume = 0.8f;
        public bool Recording => microphone != null;
        public string Status { get; private set; } = "V: hold to talk";
        public int ReceivedFrames { get; private set; }
        AudioClip microphone;
        string device;
        int cursor, deviceIndex;
        uint sequence;
        float retryAt;
        float[] samples;
        readonly byte[] encoded = new byte[FrameSamples];
        readonly Dictionary<ulong, Speaker> speakers = new Dictionary<ulong, Speaker>();
        readonly List<ulong> removed = new List<ulong>();
        public string DeviceLabel { get { var devices = Microphone.devices; return devices.Length == 0 ? "No microphone" : devices[Mathf.Clamp(deviceIndex, 0, devices.Length - 1)]; } }
        public void NextDevice() { StopMic(); var list = Microphone.devices; if (list.Length > 0) deviceIndex = (deviceIndex + 1) % list.Length; }
        public static bool InRange(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= Range * Range;
        public void Capture(bool held)
        {
            if (!held || !Session.Connected) { StopMic(); return; }
            if (microphone == null && Time.unscaledTime >= retryAt)
            {
                retryAt = Time.unscaledTime + 3;
                var list = Microphone.devices;
                if (list.Length == 0) { Status = "No microphone detected"; return; }
                device = list[Mathf.Clamp(deviceIndex, 0, list.Length - 1)];
                try
                {
                    microphone = Microphone.Start(device, true, 1, SampleRate);
                    if (microphone == null) { Status = "Microphone unavailable - check Windows microphone access"; return; }
                    cursor = 0; samples = new float[FrameSamples * microphone.channels]; Status = "TALKING - nearby players only";
                }
                catch (Exception e) { Status = "Microphone unavailable: " + e.Message; StopMic(); }
            }
            if (microphone == null) return;
            int position = Microphone.GetPosition(device);
            if (position < 0) { Status = "Microphone disconnected"; StopMic(); return; }
            int available = (position - cursor + microphone.samples) % microphone.samples;
            if (available > SampleRate / 4) { cursor = (position - FrameSamples + microphone.samples) % microphone.samples; available = FrameSamples; }
            int frames = 0;
            while (available >= FrameSamples && frames++ < 5)
            {
                microphone.GetData(samples, cursor);
                for (int i = 0; i < FrameSamples; i++)
                {
                    float value = 0; for (int c = 0; c < microphone.channels; c++) value += samples[i * microphone.channels + c];
                    encoded[i] = Encode(value / microphone.channels);
                }
                SendFrame?.Invoke(encoded, ++sequence);
                cursor = (cursor + FrameSamples) % microphone.samples; available -= FrameSamples;
            }
        }
        void StopMic()
        {
            if (microphone == null) return;
            Microphone.End(device); Destroy(microphone); microphone = null; Status = "V: hold to talk";
        }
        public void Receive(ulong id, uint seq, byte[] bytes)
        {
            if (Session.State.Operator == Session.LocalId || id == Session.LocalId || bytes.Length != FrameSamples || !Session.State.Players.ContainsKey(id)) return;
            if (!speakers.TryGetValue(id, out var speaker)) { speaker = new Speaker(transform, id); speakers.Add(id, speaker); }
            if (speaker.HasSequence && seq <= speaker.Sequence) return;
            speaker.HasSequence = true; speaker.Sequence = seq; speaker.LastPacket = Time.unscaledTime;
            if (!Muted) speaker.Append(bytes);
            ReceivedFrames++;
        }
        public bool Speaking(ulong id) => speakers.TryGetValue(id, out var speaker) && Time.unscaledTime - speaker.LastPacket < 0.3f;
        void Update()
        {
            removed.Clear();
            foreach (var pair in speakers)
            {
                if (!Session.State.Players.TryGetValue(pair.Key, out var p)) { removed.Add(pair.Key); continue; }
                pair.Value.Source.transform.position = p.Position + Vector3.up * 0.7f;
                bool deaf = Session.State.Operator == Session.LocalId;
                pair.Value.Source.spatialBlend=pair.Key==Session.State.Operator?0:1;
                pair.Value.Source.volume = Muted || deaf ? 0 : Volume;
                if (deaf || Muted) pair.Value.Clear();
            }
            foreach (var id in removed) { speakers[id].Dispose(); speakers.Remove(id); }
        }
        public void ResetSession() { StopMic(); foreach (var s in speakers.Values) s.Dispose(); speakers.Clear(); ReceivedFrames = 0; sequence = 0; }
        void OnDestroy() { ResetSession(); }
        public void SyntheticFrame(float phase)
        {
            for (int i = 0; i < FrameSamples; i++) encoded[i] = Encode(Mathf.Sin((phase + i / (float)SampleRate) * 440 * Mathf.PI * 2) * 0.04f);
            SendFrame?.Invoke(encoded, ++sequence);
        }
        public static byte Encode(float sample)
        {
            int pcm = (int)(Mathf.Clamp(sample, -1, 1) * 32767); int sign = (pcm >> 8) & 0x80;
            if (sign != 0) pcm = -pcm;
            pcm = Math.Min(pcm, 32635) + 132;
            int exponent = 7; for (int mask = 16384; (pcm & mask) == 0 && exponent > 0; mask >>= 1) exponent--;
            int mantissa = (pcm >> (exponent + 3)) & 15;
            return (byte)~(sign | (exponent << 4) | mantissa);
        }
        public static float Decode(byte value)
        {
            int u = (~value) & 255; int sample = (((u & 15) << 3) + 132) << ((u >> 4) & 7);
            sample -= 132; return ((u & 128) != 0 ? -sample : sample) / 32768f;
        }
        sealed class Speaker
        {
            public readonly AudioSource Source;
            public uint Sequence; public bool HasSequence; public float LastPacket;
            readonly float[] ring = new float[SampleRate / 4];
            readonly object gate = new object();
            int read, write, count;
            readonly AudioClip clip;
            public Speaker(Transform parent, ulong id)
            {
                var go = new GameObject("Proximity voice " + id); go.transform.SetParent(parent, false);
                Source = go.AddComponent<AudioSource>(); Source.spatialBlend = 1; Source.dopplerLevel = 0;
                Source.rolloffMode = AudioRolloffMode.Linear; Source.minDistance = 1.8f; Source.maxDistance = Range;
                clip = AudioClip.Create("Incoming voice", SampleRate, 1, SampleRate, true, Read);
                Source.clip = clip; Source.loop = true; Source.Play();
            }
            public void Clear() { lock (gate) { read = write = count = 0; } }
            public void Append(byte[] data)
            {
                lock (gate)
                {
                    foreach (var value in data)
                    {
                        if (count == ring.Length) { read = (read + 1) % ring.Length; count--; }
                        ring[write] = Decode(value); write = (write + 1) % ring.Length; count++;
                    }
                }
            }
            void Read(float[] output)
            {
                lock (gate) for (int i = 0; i < output.Length; i++)
                    if (count > 0) { output[i] = ring[read]; read = (read + 1) % ring.Length; count--; } else output[i] = 0;
            }
            public void Dispose() { Source.Stop(); UnityEngine.Object.Destroy(Source.gameObject); UnityEngine.Object.Destroy(clip); }
        }
    }
}

