using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CUETools.Codecs;
using CUETools.Codecs.FLAKE;
using System.IO;

namespace GoogleSpeech
{
    public static class SoundTools
    {
        /// <summary> Конвертирование wav-файла во flac </summary>        
        /// <returns>Частота дискретизации</returns>
        public static int Wav2Flac(Stream wavStream, Stream flacStream)
        {
            int sampleRate = 0;

            IAudioSource audioSource = new WAVReader(null, wavStream);
            AudioBuffer buff = new AudioBuffer(audioSource, 0x10000);

            FlakeWriter flakewriter = new FlakeWriter(null, flacStream, audioSource.PCM);
            sampleRate = audioSource.PCM.SampleRate;

            FlakeWriter audioDest = flakewriter;
            while (audioSource.Read(buff, -1) != 0)
            {
                audioDest.Write(buff);
            }
            return sampleRate;
        }

        public static Stream ConvertSamplesToWavFileFormat(Stream samplesStream, int sampleRate)
        {
            const int channelCount = 1;
            int sampleSize = (int)samplesStream.Length;
            int totalSize = 12 + 24 + 8 + sampleSize;

            byte[] wav = new byte[totalSize];
            int b = 0;

            // RIFF header
            wav[b++] = (byte)'R';
            wav[b++] = (byte)'I';
            wav[b++] = (byte)'F';
            wav[b++] = (byte)'F';
            int chunkSize = totalSize - 8;
            wav[b++] = (byte)(chunkSize & 0xff);
            wav[b++] = (byte)((chunkSize >> 8) & 0xff);
            wav[b++] = (byte)((chunkSize >> 16) & 0xff);
            wav[b++] = (byte)((chunkSize >> 24) & 0xff);
            wav[b++] = (byte)'W';
            wav[b++] = (byte)'A';
            wav[b++] = (byte)'V';
            wav[b++] = (byte)'E';

            // Format header
            wav[b++] = (byte)'f';
            wav[b++] = (byte)'m';
            wav[b++] = (byte)'t';
            wav[b++] = (byte)' ';
            wav[b++] = 16;
            wav[b++] = 0;
            wav[b++] = 0;
            wav[b++] = 0; // Chunk size
            wav[b++] = 1;
            wav[b++] = 0; // Compression code
            wav[b++] = channelCount;
            wav[b++] = 0; // Number of channels
            wav[b++] = (byte)(sampleRate & 0xff);
            wav[b++] = (byte)((sampleRate >> 8) & 0xff);
            wav[b++] = (byte)((sampleRate >> 16) & 0xff);
            wav[b++] = (byte)((sampleRate >> 24) & 0xff);
            int byteRate = sampleRate * channelCount * sizeof(short); // byte rate for all channels
            wav[b++] = (byte)(byteRate & 0xff);
            wav[b++] = (byte)((byteRate >> 8) & 0xff);
            wav[b++] = (byte)((byteRate >> 16) & 0xff);
            wav[b++] = (byte)((byteRate >> 24) & 0xff);
            wav[b++] = channelCount * sizeof(short);
            wav[b++] = 0; // Block align (bytes per sample)
            wav[b++] = sizeof(short) * 8;
            wav[b++] = 0; // Bits per sample

            // Data chunk header
            wav[b++] = (byte)'d';
            wav[b++] = (byte)'a';
            wav[b++] = (byte)'t';
            wav[b++] = (byte)'a';
            wav[b++] = (byte)(sampleSize & 0xff);
            wav[b++] = (byte)((sampleSize >> 8) & 0xff);
            wav[b++] = (byte)((sampleSize >> 16) & 0xff);
            wav[b++] = (byte)((sampleSize >> 24) & 0xff);

            samplesStream.Seek(0, SeekOrigin.Begin);
            for (int s = 0; s < samplesStream.Length; s += 2)
            {
                byte b1 = (byte)samplesStream.ReadByte();
                byte b2 = (byte)samplesStream.ReadByte();
                wav[b++] = b1;
                wav[b++] = b2;
            }
            var result = new MemoryStream(wav);
            result.Seek(0, SeekOrigin.Begin);
            return result;
        }
    }
}
