using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using Newtonsoft.Json;

namespace WpfIATCSharp
{
    class SendDataPipe
    {
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

        public void Start(string recResult)
        {
            string text = recResult;
            string final_text = "";

            try
            {
                //Solution A:
                RootObject jsonObject = JsonConvert.DeserializeObject<RootObject>(text);
                final_text = jsonObject.trans_result.dst.ToString();

                //Solution B:
                //Dictionary<string, object>
                //JsonObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
                //Dictionary<string, object>
                //trans_result = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonObject["trans_result"].ToString());
                //final_text = trans_result["dst"].ToString();
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
    }   
}
