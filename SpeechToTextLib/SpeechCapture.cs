using GoogleSpeech;
using Microsoft.DirectX.DirectSound;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Cluster.SpeechToText
{
    public class SpeechCapture
    {
        Capture capture;
        CaptureBuffer captureBuffer;
        int readPos = 0;
        public bool IsRecording { get; private set; }
        MemoryStream record = null;
        int recordTime = 0;
        int noRecordTime = 0;
        byte[] lastSample = null;
        int lastSize = 0;
        int sampleRate;
        Thread captureThread = null;

        public Int16 DetectVolume { get; set; } // Громкость, при которой начинаем запись
        public int PhraseLengthMillisecondsMinimum { get; set; } // Минимальная длина фразы
        public int PhraseLengthMillisecondsMaximum { get; set; } // Максимальная длина фразы
        public int SilenceLenghtMillisecondsMaximum { get; set; } // Максимальная длина паузы
        public string Language { get; set; } // Язык распознавания ("ru-RU", en-US", "fr-FR" и т.п.)
        public int MaxResults { get; set; } // Максимальное число результатов
        public delegate void OnSpeechRecognizedDelegate(string[] result);
        public delegate void OnSpeechRecognizeFailedDelegate();
        public delegate void OnCaptureErorDelegate(Exception exception);
        public delegate void OnGoogleErrorDelegate(Exception exception);
        public delegate void OnPeakMeterDelegate(int peakVolume);
        public event OnSpeechRecognizedDelegate OnSpeechRecognized; // Событие для успешного распознавания
        public event OnSpeechRecognizeFailedDelegate OnSpeechRecognizeFailed; // Событие, когда Гугл не может распознать
        public event OnGoogleErrorDelegate OnGoogleError; // Возникает, когда происходит ошибка при обращении к гуглу
        public event OnCaptureErorDelegate OnCaptureEror; // Возникает, когда происходит ошибка при чтении звука
        public event OnPeakMeterDelegate OnPeakMeter; // Возникает каждые 100 миллисекунд и сообщает пик громкости

        const int bufferSize = 10 * 44100 * 2;

        public SpeechCapture()
        {
            DetectVolume = 30000;
            PhraseLengthMillisecondsMinimum = 500;
            PhraseLengthMillisecondsMaximum = 10000;
            SilenceLenghtMillisecondsMaximum = 500;
            Language = "ru-RU";
            MaxResults = 10;
        }

        public void StartCapture(int sampleRate = 16000)
        {
            StartCapture(sampleRate, null);
        }
        public void StartCapture(int sampleRate, Capture captureDevice)
        {
            StopCapture();
            EmptyRequest();

            this.sampleRate = sampleRate;
            readPos = 0;
            IsRecording = false;
            record = null;
            recordTime = 0;
            noRecordTime = 0;
            lastSample = null;
            lastSize = 0;

            capture = (captureDevice == null) ? new Capture() : captureDevice;

            WaveFormat waveFormat = new WaveFormat();// Load the sound 
            waveFormat.BitsPerSample = 16;
            waveFormat.BlockAlign = 2;
            waveFormat.Channels = 1;
            waveFormat.AverageBytesPerSecond = sampleRate * 2;
            waveFormat.SamplesPerSecond = sampleRate;
            waveFormat.FormatTag = WaveFormatTag.Pcm;

            CaptureBufferDescription captureBuffDesc = new CaptureBufferDescription();
            captureBuffDesc.BufferBytes = bufferSize;
            captureBuffDesc.Format = waveFormat;

            captureBuffer = new CaptureBuffer(captureBuffDesc, capture);
            captureBuffer.Start(true);

            captureThread = new Thread(captureLoop);
            captureThread.Start();
            new Thread(EmptyRequest).Start();
        }

        public void StopCapture()
        {
            if (captureThread != null)
            {
                captureThread.Abort();
                captureThread = null;
            }
            if (captureBuffer != null)
            {
                //captureBuffer.Stop();
                captureBuffer.Dispose();
                captureBuffer = null;
            }
            if (capture != null)
            {
                capture.Dispose();
                capture = null;
            }
            IsRecording = false;
        }

        private void captureLoop()
        {
            try
            {
                while (true)
                {
                    int currentPos, pos2;
                    // Смотрим, в каком месте находится курсор записи
                    captureBuffer.GetCurrentPosition(out currentPos, out pos2);
                    // Вычисляем размер записи
                    int size = currentPos - readPos;
                    if (size < 0)
                        size += bufferSize;
                    if (size == 0) continue;

                    // Читаем данные в буфер
                    byte[] streamBuffer = new byte[size];
                    var stream = new MemoryStream(streamBuffer);
                    captureBuffer.Read(readPos, stream, size, LockFlag.None);

                    bool voiceDetected = false;
                    // Проверяем каждый семпл, смотрим громкость
                    int peakVolume = 0;
                    for (int i = 1; i < size; i += 2)
                    {
                        int sampleVolume = streamBuffer[i] * 0x100 + streamBuffer[i - 1];
                        if (sampleVolume > 32767) sampleVolume -= 65536; // Оно со знаком
                        if (sampleVolume > peakVolume) peakVolume = sampleVolume; // Вычисляем пик громкости
                        // Если она выше заданное громкости определения голоса...
                        if (Math.Abs(sampleVolume) > DetectVolume)
                        {
                            voiceDetected = true;
                            if (OnPeakMeter == null) break;
                            //break;
                        }
                    }
                    if (OnPeakMeter != null) OnPeakMeter(peakVolume);

                    // Обнаружен голос
                    if (voiceDetected)
                    {
                        // Если мы ещё не пишем...
                        if (!IsRecording)
                        {
                            if (record != null) record.Dispose();
                            record = new MemoryStream();
                            IsRecording = true;
                            recordTime = 0;
                            // Сохраняем кусочек звука, который был до этого
                            if (lastSample != null && lastSize > 0)
                                record.Write(lastSample, 0, lastSize);
                        }
                        noRecordTime = 0; // Обнуляем счётчик времени без голоса
                    }
                    else // Если в микрофоне тишина...
                    {
                        if (IsRecording) // Если при этом идёт запись
                        {
                            noRecordTime++; // Увеличиваем счётчик времени без голоса
                            // И если мы молчим уже достаточно долго
                            if (noRecordTime >= SilenceLenghtMillisecondsMaximum / 100)
                            {
                                IsRecording = false; // Прекращаем запись
                                // Проверяем на заданные условия
                                if ((recordTime >= PhraseLengthMillisecondsMinimum / 100) &&
                                    (recordTime <= PhraseLengthMillisecondsMaximum / 100))
                                {
                                    // Приостанавливаем запись
                                    captureBuffer.Stop();
                                    // Отправляем записанное на обработку
                                    processRecord(record);
                                    // Снова запускаем запись
                                    captureBuffer.Start(true);
                                }
                            }
                        }
                    }

                    // Если пишем, то добавляем данные в буфер
                    if (IsRecording)
                    {
                        record.Write(streamBuffer, 0, size);
                        recordTime++;
                    }
                    else
                    {
                        // Запомиаем семпл, пригодится
                        lastSample = streamBuffer;
                        lastSize = size;
                    }
                    // Запоминаем позицию
                    readPos = currentPos;

                    Thread.Sleep(100);
                }
            }
            catch (ThreadAbortException)
            {
            }
            catch (Exception ex)
            {
                captureThread = null;
                if (OnCaptureEror != null)
                    OnCaptureEror(ex);
            }
        }

        private void processRecord(MemoryStream record)
        {
            var wav = SoundTools.ConvertSamplesToWavFileFormat(record, sampleRate);
            var flac = new MemoryStream();
            SoundTools.Wav2Flac(wav, flac);
            try
            {
                var result = GoogleVoice.GoogleSpeechRequest(flac, sampleRate, Language, MaxResults);
                if (result.Length > 0)
                {
                    if (OnSpeechRecognized != null)
                        OnSpeechRecognized(result);
                }
                else if (OnSpeechRecognizeFailed != null) OnSpeechRecognizeFailed();
            }
            catch (Exception ex)
            {
                if (OnGoogleError != null)
                    OnGoogleError(ex);
            }
        }

        private void EmptyRequest()        
        {
            try
            {
                GoogleVoice.GoogleSpeechRequest(new MemoryStream(), sampleRate);
            }
            catch { }
        }
    }
}
