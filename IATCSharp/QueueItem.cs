using System.Collections.Generic;
using System.Threading.Tasks;

namespace WpfIATCSharp
{
    internal class QueueItem
    {
        public QueueItem(List<VoiceData> voiceBuffer, string session_begin_params, ref SendDataPipe sendDataPipe)
        {
            this.VoiceBuffer = voiceBuffer;
            this.Session_begin_params = session_begin_params;
            this.SendDataPipe = sendDataPipe;
            this.CompletionSource = new TaskCompletionSource<bool>();
        }

        public List<VoiceData> VoiceBuffer { get; private set; }
        public string Session_begin_params { get; private set; }
        public SendDataPipe SendDataPipe { get; private set; }
        /// Completion source to signal to sender when message has been sent.
        public TaskCompletionSource<bool> CompletionSource { get; private set; }
    }
}