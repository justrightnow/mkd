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

using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WpfIATCSharp
{
    class SendDataPipe
    {
        Stopwatch stopwatch = new Stopwatch();
        public string serviceFrom;
        public string serviceTo;
        public string g_languageFrom;
        public string g_languageTo;

        private class TranscriptUtterance
        {
            public TimeSpan Timespan;
            public string Recognition;
            public string Translation;
        }

        private List<TranscriptUtterance> Transcript = new List<TranscriptUtterance>();

        private OrderTaskScheduler _scheduler = new OrderTaskScheduler("aaa");

        public void SendData(object data)
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

        public void Start(string recResult)
        {
            string text = recResult;
            string recognition = "";
            string translation = "";

            try
            {
                if (serviceFrom == "中文" && serviceTo=="英文" || serviceFrom == "中文" && serviceTo == "西班牙语")
                {
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

                    foreach (string value in reco) translation += GoogleTranslate(value, "zh-CN", g_languageTo) + " ";
                    //End Google
                }
                else if (serviceFrom == "中文" && serviceTo=="中文")
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

            CallInOrderAsync(translation);
        }

        public Task CallInOrderAsync(string translation)
        {
            return Task.Factory.StartNew(() => SendData(translation), CancellationToken.None, TaskCreationOptions.None, this._scheduler); //Tasks don't close, after closing program still running on Task Manager! currently solved by using Environment.Exit(1) on MainWindow OnWindowClosing().. should be a better way (Cancellation Token?)
        }

        public class RootObject
        {
            public string RecognitionStatus { get; set; }
            public string DisplayText { get; set; }
            public string Offset { get; set; }
            public string Duration { get; set; }
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
