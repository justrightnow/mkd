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
using System.Web;
using System.Net;
using Microsoft.Translator.Samples;

using Google.Cloud.Translation.V2;
using System.Text.RegularExpressions;


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

        public void Start(string recResult,string name)
        {
            string text = recResult;
            string recognition = "";
            string translation = "";

            try
            {
                if (name == "中英翻译")
                {
                    Debug.WriteLine("HOLA!");
                    recognition = polish(text);

                    //Start Google
                    List<string> reco = new List<string>();
                    reco.Add(recognition);
                    int b = 0;

                    while (reco[b].Length > 74 || reco[b].Contains("？"))
                    {
                        string buffer = reco[b];

                        if (reco[b].Contains("？") && reco[b].IndexOf("？") < 74)
                        {
                            if (reco[b].IndexOf("？") + 1 == reco[b].Length) break;
                            else
                            {
                                reco.Add(reco[b].Substring(buffer.IndexOf("？") + 1));
                                reco[b] = reco[b].Remove(buffer.IndexOf("？") + 1);
                            }
                        }
                        else
                        {
                            while (buffer.LastIndexOf("，") > 74) buffer = buffer.Remove(buffer.LastIndexOf("，"));

                            reco.Add(reco[b].Substring(buffer.LastIndexOf("，") + 1));
                            reco[b] = reco[b].Remove(buffer.LastIndexOf("，") + 1);
                        }

                        b++;
                    }

                    foreach (string value in reco) translation += GoogleTranslate(value, "zh-CN", "en") + " ";
                    //End Google
                    Debug.WriteLine("ADIOS!");
                }
                else if (name == "英中翻译")
                {
                    RootObject jsonObject = JsonConvert.DeserializeObject<RootObject>(text);
                    recognition = jsonObject.trans_result.src.ToString();
                    translation = jsonObject.trans_result.dst.ToString();
                }
                else if (name == "语音识别")
                {
                    recognition = polish(text);
                    translation = recognition;
                }
            }
            catch (Exception e)
            {
                text = null;
            }

            TranscriptUtterance utterance = new TranscriptUtterance();
            utterance.Recognition = recognition;
            utterance.Translation = translation;
            utterance.Timespan = stopwatch.Elapsed;
            Transcript.Add(utterance);

            Thread pipeThread = new Thread(new ParameterizedThreadStart(SendData));
            pipeThread.IsBackground = true;
            pipeThread.Start(translation);
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

        public string polish(string a)
        {
            string textpolished = a;

            if (textpolished.Contains("啊")) textpolished = textpolished.Replace("啊", "");
            if (textpolished.Contains("饿")) textpolished = textpolished.Replace("饿", "");
            if (textpolished.Contains("嗯")) textpolished = textpolished.Replace("嗯", "");

            return textpolished;
        }

        public string GoogleTranslate(string text, string fromLanguage, string toLanguage)
        {
            CookieContainer cc = new CookieContainer();

            string GoogleTransBaseUrl = "https://translate.google.cn/";

            var BaseResultHtml = GetResultHtml(GoogleTransBaseUrl, cc, "");

            Regex re = new Regex(@"(?<=TKK=)(.*?)(?=\);)");

            var TKKStr = re.Match(BaseResultHtml).ToString() + ")";//在返回的HTML中正则匹配TKK的JS代码  

            var TKK = ExecuteScript(TKKStr, TKKStr);//执行TKK代码，得到TKK值  

            var GetTkkJS = File.ReadAllText(@"gettk.js");

            var tk = ExecuteScript("tk(\"" + text + "\",\"" + TKK + "\")", GetTkkJS);

            string googleTransUrl = "https://translate.google.cn/translate_a/single?client=t&sl=" + fromLanguage + "&tl=" + toLanguage + "&hl=en&dt=at&dt=bd&dt=ex&dt=ld&dt=md&dt=qca&dt=rw&dt=rm&dt=ss&dt=t&ie=UTF-8&oe=UTF-8&otf=1&ssel=0&tsel=0&kc=1&tk=" + tk + "&q=" + HttpUtility.UrlEncode(text);

            var ResultHtml = GetResultHtml(googleTransUrl, cc, "");

            dynamic TempResult = Newtonsoft.Json.JsonConvert.DeserializeObject(ResultHtml);

            string ResultText = Convert.ToString(TempResult[0][0][0]);

            return ResultText;
        }

        public string GetResultHtml(string url, CookieContainer cookie, string referer)
        {
            var html = "";

            var webRequest = WebRequest.Create(url) as HttpWebRequest;

            webRequest.Method = "GET";

            webRequest.CookieContainer = cookie;

            webRequest.Referer = referer;

            webRequest.Timeout = 20000;

            webRequest.Headers.Add("X-Requested-With:XMLHttpRequest");

            webRequest.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";

            webRequest.UserAgent = url;//useragent;  

            using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                using (var reader = new StreamReader(webResponse.GetResponseStream(), Encoding.UTF8))
                {

                    html = reader.ReadToEnd();
                    reader.Close();
                    webResponse.Close();
                }
            }
            return html;
        }

        private string ExecuteScript(string sExpression, string sCode)
        {
            MSScriptControl.ScriptControl scriptControl = new MSScriptControl.ScriptControl();
            scriptControl.UseSafeSubset = true;
            scriptControl.Language = "JScript";
            scriptControl.AddCode(sCode);
            try
            {
                string str = scriptControl.Eval(sExpression).ToString();
                return str;
            }
            catch (Exception ex)
            {
                string str = ex.Message;
            }
            return null;
        }
    }   
}
