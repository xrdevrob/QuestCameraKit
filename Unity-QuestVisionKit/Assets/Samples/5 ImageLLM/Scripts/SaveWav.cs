using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace QuestCameraKit.OpenAI
{
    public static class SaveWav
    {
        // sampleFrames is per channel, matching AudioClip.samples and Microphone.GetPosition.
        public static byte[] Save(string filename, AudioClip clip, int sampleFrames = -1)
        {
            if (!clip) throw new ArgumentNullException(nameof(clip));
            if (sampleFrames < -1 || sampleFrames > clip.samples)
                throw new ArgumentOutOfRangeException(nameof(sampleFrames));
            if (sampleFrames == -1) sampleFrames = clip.samples;

            var samples = new float[sampleFrames * clip.channels];
            if (samples.Length > 0 && !clip.GetData(samples, 0))
                throw new InvalidOperationException("Audio clip data is not readable.");

            using var stream = new MemoryStream(44 + samples.Length * 2);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples.Length * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write((ushort)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((ushort)(clip.channels * 2));
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(samples.Length * 2);
            foreach (var sample in samples)
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            return stream.ToArray();
        }
    }
}
