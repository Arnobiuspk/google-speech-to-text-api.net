using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Cluster.SpeechToText
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            SpeechCapture speechCapture = new SpeechCapture();
            speechCapture.DetectVolume = 30000;
            speechCapture.MaxResults = 10;
            speechCapture.Language = "en-EN";
            speechCapture.OnSpeechRecognized += speechCapture_OnSpeechRecognized;
            speechCapture.OnSpeechRecognizeFailed += speechCapture_OnSpeechRecognizeFailed;
            speechCapture.OnPeakMeter += speechCapture_OnPeakMeter;
            speechCapture.StartCapture();
            Console.WriteLine("Speak in microphone");
            
            Console.ReadLine();
            speechCapture.StopCapture();
        }

        static void speechCapture_OnPeakMeter(int peakVolume)
        {
            Console.Title = "Peak volume: "+peakVolume;
        }

        static void speechCapture_OnSpeechRecognized(string[] result)
        {
            Console.WriteLine("Google recognized:");
            foreach (var text in result)
                Console.WriteLine(text);
        }

        static void speechCapture_OnSpeechRecognizeFailed()
        {
            Console.WriteLine("Can't understand");
        }        
    }
}
