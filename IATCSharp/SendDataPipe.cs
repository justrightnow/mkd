using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Text;
using System.Globalization;

namespace WpfIATCSharp
{
    class SendDataPipe
    {
    /**/Stopwatch stopwatch = new Stopwatch();
        /// <summary>
        /// Holds one utterance for the transcript
        /// </summary>
    /**/private class TranscriptUtterance
        {
            public TimeSpan Timespan;
            public string Recognition;
            public string Translation;
        }
        /// <summary>
        /// Holds the set of utterances in this conversation;
        /// </summary>
    /**/private List<TranscriptUtterance> Transcript = new List<TranscriptUtterance>();

        private void SendData(object data)
        {
            try
            {
                NamedPipeClientStream _pipeClient = new NamedPipeClientStream(".", "closePipe", PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.Impersonation);
                _pipeClient.Connect();
                StreamWriter sw = new StreamWriter(_pipeClient);
                sw.WriteLine(data);
                sw.Flush();
                Thread.Sleep(1000);
                sw.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public void Start(string recResult,string service)
        {
            string text = recResult;
            string final_text = "";

            try
            {
                if ((service == "SessionBeginTranslateCntoEn") || (service == "SessionBeginTranslateEntoCn"))
                {
                    RootObject jsonObject = JsonConvert.DeserializeObject<RootObject>(text);
                    final_text = jsonObject.trans_result.dst.ToString();

                    TranscriptUtterance utterance = new TranscriptUtterance();
                    utterance.Recognition = jsonObject.trans_result.src.ToString();
                    utterance.Translation = jsonObject.trans_result.dst.ToString();
                    utterance.Timespan = stopwatch.Elapsed;
                    Transcript.Add(utterance);
                }
                else final_text = recResult;

            }
            catch (Exception e)
            {
                text = null;
            }

            Debug.WriteLine(final_text);

            Thread pipeThread = new Thread(new ParameterizedThreadStart(SendData));
            pipeThread.IsBackground = true;
            pipeThread.Start(final_text);
        }

        //Below 2 classes for Solution A:
        public class TransResult
        {
            public string src { get; set; }
            public string dst { get; set; }
        }

        public class RootObject
        {
            public string from { get; set; }
            public int ret { get; set; }
            public string sid { get; set; }
            public string to { get; set; }
            public TransResult trans_result { get; set; }
        }

        private string Now() { return DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.ff", DateTimeFormatInfo.InvariantInfo); }

        public void WriteDataOnFile()
        {
            SaveFileDialog savefiledialog = new SaveFileDialog();
            savefiledialog.RestoreDirectory = true;
            savefiledialog.FileName = "Transcript_" + DateTime.Now.ToString("yyMMdd_HHmm") + ".txt";
            savefiledialog.Filter = "Text Files|*.txt|All files|*.*";
            if (savefiledialog.ShowDialog() ?? false)
            {
                string transcriptfilename = Path.ChangeExtension(savefiledialog.FileName, "." + Path.GetExtension(savefiledialog.FileName));
                using (StreamWriter file = new StreamWriter(transcriptfilename, false, Encoding.UTF8))
                {
                    foreach (TranscriptUtterance utterance in Transcript)
                    {
                        file.WriteLine("{0} Recognition: {1}\n",Now(),utterance.Recognition);
                        file.WriteLine("{0} Translation: {1}\n",Now(),utterance.Translation);
                    }
                    file.Close();
                    using (Process p = new Process())
                    {
                        p.StartInfo.FileName = transcriptfilename;
                        p.Start();
                    }
                }
            }

        }
    }   
}
