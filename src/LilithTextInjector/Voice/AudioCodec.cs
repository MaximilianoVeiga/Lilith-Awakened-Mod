namespace LilithTextInjector;

// Stateless PCM/WAV encoding, normalisation and Unity clip conversion.
internal static class AudioCodec
{
    internal static byte[] EncodePcm16Wav(float[] samples, int channels, int sampleRate)
    {
        using var stream = new MemoryStream(44 + samples.Length * 2);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + samples.Length * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(samples.Length * 2);
        foreach (var sample in samples)
            writer.Write((short)Math.Round(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        writer.Flush();
        return stream.ToArray();
    }

    internal static (byte[] Wav, double Rms, float Peak, bool Measured) NormalizeVoiceWav(byte[] wav, int preferredSampleRate)
    {
        try
        {
            using var input = new MemoryStream(wav, false);
            using var reader = new WaveFileReader(input);
            var provider = reader.ToSampleProvider();
            var channels = Math.Max(1, provider.WaveFormat.Channels);
            var sourceRate = Math.Max(8000, provider.WaveFormat.SampleRate);
            var estimatedSamples = (int)Math.Min(int.MaxValue, Math.Max(4096L,
                reader.Length / Math.Max(1, reader.WaveFormat.BlockAlign) * channels));
            var samples = new float[estimatedSamples];
            var count = 0;
            while (count < samples.Length)
            {
                var read = provider.Read(samples, count, samples.Length - count);
                if (read <= 0)
                    break;
                count += read;
            }
            if (count < channels)
                return (wav, 0d, 0f, false);

            var frames = count / channels;
            var mono = new float[frames];
            double sumSquares = 0;
            float peak = 0;
            for (var frame = 0; frame < frames; frame++)
            {
                double sum = 0;
                for (var channel = 0; channel < channels; channel++)
                    sum += samples[frame * channels + channel];
                var value = (float)(sum / channels);
                mono[frame] = value;
                sumSquares += value * value;
                peak = Math.Max(peak, Math.Abs(value));
            }

            var rms = Math.Sqrt(sumSquares / Math.Max(1, frames));
            var gain = peak > 0.001f ? Math.Min(3f, 0.88f / peak) : 1f;
            if (gain > 1.05f)
                for (var i = 0; i < mono.Length; i++)
                    mono[i] = Math.Clamp(mono[i] * gain, -1f, 1f);

            var targetRate = Math.Min(Math.Clamp(preferredSampleRate, 8000, 48000), sourceRate);
            float[] output;
            if (targetRate == sourceRate)
            {
                output = mono;
            }
            else
            {
                var outputFrames = Math.Max(1, (int)Math.Round(frames * (double)targetRate / sourceRate));
                output = new float[outputFrames];
                var ratio = sourceRate / (double)targetRate;
                for (var i = 0; i < outputFrames; i++)
                {
                    var position = i * ratio;
                    var left = Math.Min(frames - 1, (int)position);
                    var right = Math.Min(frames - 1, left + 1);
                    var fraction = (float)(position - left);
                    output[i] = mono[left] + (mono[right] - mono[left]) * fraction;
                }
            }

            Plugin.PluginLog.LogInfo($"Voice audio prepared as mono PCM16 {targetRate}Hz (source {sourceRate}Hz/{channels}ch, RMS {rms:F4}, peak {peak:F4}, gain {gain:F2}x).");
            return (EncodePcm16Wav(output, 1, targetRate), rms, peak, true);
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Voice audio normalization failed; sending original recording: {exception.Message}");
            return (wav, 0d, 0f, false);
        }
    }

    internal static AudioClip CreateAudioClipFromWav(byte[] wav)
    {
        if (wav.Length < 44 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF" || Encoding.ASCII.GetString(wav, 8, 4) != "WAVE")
            throw new InvalidDataException("TTS response is not a WAV file.");

        var offset = 12;
        ushort format = 0;
        ushort channels = 0;
        var sampleRate = 0;
        ushort bits = 0;
        var dataOffset = -1;
        var dataLength = 0;
        while (offset + 8 <= wav.Length)
        {
            var chunk = Encoding.ASCII.GetString(wav, offset, 4);
            var length = BitConverter.ToInt32(wav, offset + 4);
            var body = offset + 8;
            if (length < 0 || body + length > wav.Length)
                throw new InvalidDataException("Invalid WAV chunk length.");
            if (chunk == "fmt " && length >= 16)
            {
                format = BitConverter.ToUInt16(wav, body);
                channels = BitConverter.ToUInt16(wav, body + 2);
                sampleRate = BitConverter.ToInt32(wav, body + 4);
                bits = BitConverter.ToUInt16(wav, body + 14);
            }
            else if (chunk == "data")
            {
                dataOffset = body;
                dataLength = length;
                break;
            }
            offset = body + length + (length & 1);
        }
        if (dataOffset < 0 || channels == 0 || sampleRate <= 0)
            throw new InvalidDataException("WAV format or data chunk is missing.");

        float[] samples;
        if (format == 1 && bits == 16)
        {
            samples = new float[dataLength / 2];
            for (var i = 0; i < samples.Length; i++)
                samples[i] = BitConverter.ToInt16(wav, dataOffset + i * 2) / 32768f;
        }
        else if (format == 3 && bits == 32)
        {
            samples = new float[dataLength / 4];
            Buffer.BlockCopy(wav, dataOffset, samples, 0, samples.Length * 4);
        }
        else
        {
            throw new InvalidDataException($"Unsupported WAV format={format}, bits={bits}.");
        }

        var frameCount = samples.Length / channels;
        var clip = AudioClip.Create("LilithAiVoice", frameCount, channels, sampleRate, false);
        if (!clip.SetData(samples, 0))
            throw new InvalidOperationException("Unity rejected generated audio samples.");
        return clip;
    }
}
