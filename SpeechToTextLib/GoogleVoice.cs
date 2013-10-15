using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace GoogleSpeech
{
    public class GoogleVoice
    {      
      public static String[] GoogleSpeechRequest(Stream flac, int sampleRate, string language = "ru-RU", int maxResults = 10)
      {
          WebRequest request = WebRequest.Create(string.Format("https://www.google.com/speech-api/v1/recognize?xjerr=1&client=chromium&lang={0}&maxresults={1}", language, maxResults));
          request.Method = "POST";

          byte[] byteArray = new byte[flac.Length];
          flac.Seek(0, SeekOrigin.Begin);
          flac.Read(byteArray, 0, byteArray.Length);
          //var fileName = @"D:\temp\" + DateTime.Now.ToFileTimeUtc().ToString()+".flac";
          //File.WriteAllBytes(fileName, byteArray);

          // Set the ContentType property of the WebRequest.
          request.ContentType = "audio/x-flac; rate=" + sampleRate; //"16000";        
          request.ContentLength = byteArray.Length;

          // Get the request stream.
          Stream dataStream = request.GetRequestStream();
          // Write the data to the request stream.
          dataStream.Write(byteArray, 0, byteArray.Length);

          dataStream.Close();

          // Get the response.
          WebResponse response = request.GetResponse();

          dataStream = response.GetResponseStream();
          // Open the stream using a StreamReader for easy access.
          StreamReader reader = new StreamReader(dataStream);
          // Read the content.
          string responseFromServer = reader.ReadToEnd();

          // Clean up the streams.
          reader.Close();
          dataStream.Close();
          response.Close();
          var result = new List<string>();
          Regex r = new Regex("(\\\"utterance\\\":\\\")(?<utterance>.+?)\\\"");
          var mc = r.Matches(responseFromServer);
          for (int i = 0; i < mc.Count; i++)
          {
              var gr = mc[i].Groups;
              var utterance = gr["utterance"].ToString();
              result.Add(utterance);
          }
          return result.ToArray();
      }
    }
}
