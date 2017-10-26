using Microsoft.CognitiveServices.SpeechRecognition;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VoiceRecorder.Audio;
using System.Security.Cryptography;
using Newtonsoft.Json;
using RestSharp;
using System.Threading;

namespace WpfIATCSharp
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        string session_begin_params;
        private WaveIn waveIn;
        private AudioRecorder recorder;
        private float lastPeak;
        float secondsRecorded;
        float totalBufferLength;

        private string name;
        private string nameTo;
        Feedback feedback = new Feedback();
        SendDataPipe sd = new SendDataPipe();
        string logAudioFileName = null;
        private WaveFileWriter audioSent; //WaveFileWriter is a class                                 
        private BlockingCollection<QueueItem> outgoingMessageQueue = new BlockingCollection<QueueItem>();// Queue of messages waiting to be sent.
        public event EventHandler<Exception> Failed;
        private bool click = false;
        List<VoiceData> VoiceReady = new List<VoiceData>();
        List<VoiceData> VoiceBuffer = new List<VoiceData>();

        /**** Bing Microphone Client  ****/
        private string m_language;
        private string subscriptionKey; //No Custom Speech: "5e6*" , Custom Speech: "900*"
        private string endpointURL;// = "https://3268991a44f74ebcbe1e0e2b89cc59cd.api.cris.ai/ws/cris/speech/recognize/continuous"; // For Custom Speech purposes
        private MicrophoneRecognitionClient micClient;
        /// <summary>
        /// Gets the Cognitive Service Authentication Uri.
        /// </summary>
        /// <value>
        /// The Cognitive Service Authentication Uri.  Empty if the global default is to be used.
        /// </value>
        private string AuthenticationUri
        {
            get { return ConfigurationManager.AppSettings["AuthenticationUri"]; }
        }
        SpeechRecognitionMode Mode = SpeechRecognitionMode.LongDictation;
        Stopwatch stopwatch = new Stopwatch();
        private class TranscriptUtterance
        {
            public TimeSpan Timespan;
            public string Recognition;
            public string Translation;
        }
        private List<TranscriptUtterance> Transcript = new List<TranscriptUtterance>();
        public string buffer = "";
        public int flag = 1;
        public bool stopbutton = false;
        private string library_link;
        private bool lib = false;

        /**** Bing Data Client  ****/
        private DataRecognitionClient dataClient;
        private string LongWaveFile;
        private bool file = false;

        /**** Baidu translate ****/
        string from; // English: "en" Spanish: "spa"
        string to; // Chinese: "zh" Korean: "kor"
        string appid = "2017*"; //write own appid
        string salt = "143*"; //write own salt
        string key = "_i5*"; //write own key

        int Ends = 5;

        public MainWindow()
        {
            InitializeComponent();
            this.Topmost = true;

            Left = System.Windows.SystemParameters.PrimaryScreenWidth - 230 - 2;
            Top = System.Windows.SystemParameters.PrimaryScreenHeight - 180 - 40 - 2;

            string promptValue = Prompt.ShowDialog("Introduce your password:", "Welcome!");
            if (promptValue != "") { Environment.Exit(1); }

            FormLoad();
            SpeechRecognition();

            feedback.Show();
            
            this.Closing += new CancelEventHandler(OnWindowClosing);
        }

        void OnWindowClosing(object sender, CancelEventArgs e)
        {
            feedback.Close();
            Environment.Exit(1);//Close everything but I should properly close the Tasks on SendDataPipe CallInOrderAsync function. 
        }
        
        public void CreateMemoryFile()
        {
            long capacity = 1 << 10 << 10;
            var ss = MemoryMappedFile.CreateOrOpen("testMmf", capacity, MemoryMappedFileAccess.ReadWrite);
        }

        public void FormLoad()
        {
            var deviceEnum = new MMDeviceEnumerator();
            var devices = deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
            combDevice.ItemsSource = devices;
            if (devices != null)
            {
                combDevice.SelectedIndex = 0;
            }

            combFile.Items.Add("Choose File..");
            combFile.Items.Add("Cancel");

            combFrom.Items.Add("中文");
            combFrom.Items.Add("英文");
            combFrom.Items.Add("西班牙语");
            combTo.Items.Add("中文");
            combTo.Items.Add("英文");
            combTo.Items.Add("西班牙语");

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = false;
            CheckBox_Transcript.IsEnabled = false;
        }

        public void SpeechRecognition()
        {
            //初始化语音识别
            int ret = (int)ErrorCode.MSP_SUCCESS;
            string login_params = string.Format("appid = {0}, work_dir = {1}", ConfigurationManager.AppSettings["AppID"].ToString(), ConfigurationManager.AppSettings["WorkDir"].ToString());
            session_begin_params = ConfigurationManager.AppSettings["CntoCn"].ToString();

            string Username = ConfigurationManager.AppSettings["Username"].ToString();
            string Password = ConfigurationManager.AppSettings["Password"].ToString();
            ret = MSCDLL.MSPLogin(Username, Password, login_params);
            
            if ((int)ErrorCode.MSP_SUCCESS != ret)
            {
                MessageBox.Show("MSPLogin failed,error code:{0}", ret.ToString());
                MSCDLL.MSPLogout();
            }

            TTS welcome = new TTS();
            welcome.CreateWAV("欢迎使用恩懂");
        }
        
        private WaveIn CreateWaveInDevice(string micName)
        {
            WaveIn newWaveIn = new WaveIn();

            if (micName.StartsWith("Microphone")) newWaveIn.WaveFormat = new WaveFormat(16000, 1); //Laptop mic works great. External mics don't work. 
            else newWaveIn.WaveFormat = new WaveFormat(8000, 2); //External mics work fine. Laptop mic works soso.

            newWaveIn.DataAvailable += OnDataAvailable;
            newWaveIn.RecordingStopped += OnRecordingStopped;
            return newWaveIn;
        }

        void OnDataAvailable(object sender, WaveInEventArgs e)
        {            
            if (audioSent != null)
            {
                audioSent.Write(e.Buffer, 0, e.BytesRecorded);
            }

            if (name == "中文")
            {
                totalBufferLength += e.Buffer.Length;
                secondsRecorded = (float)(totalBufferLength / 32000);

                VoiceData data = new VoiceData();
                for (int i = 0; i < 3200; i++)
                {
                    data.data[i] = e.Buffer[i];
                }
                VoiceBuffer.Add(data);

                if (lastPeak < 20)
                    Ends = Ends - 1;
                else
                    Ends = 5;//梦龙：originally 5

                if (Ends == 0)
                {
                    if (VoiceBuffer.Count() > 5)
                    {
                        VoiceReady.AddRange(VoiceBuffer);

                        var msg = new QueueItem(VoiceReady, session_begin_params, ref sd);
                        this.outgoingMessageQueue.Add(msg);
                    }

                    VoiceBuffer.Clear();
                    Ends = 5;//梦龙：originally 5
                }
            }

            prgVolume.Value = lastPeak;
        }

        void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                MessageBox.Show(String.Format("A problem was encountered during recording {0}",
                                              e.Exception.Message));
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (name != "英文" && CheckBox_Library.IsChecked == true)
            {
                MessageBox.Show("The selected language cannot use the library service.\nPlease unselect the Lib box.","Library error");
                return;
            }

            InitializeComponent();
            this.Topmost = true;

            click = true;
            this.WindowState = WindowState.Minimized;

            if (name == "英文" && nameTo == "英文" || name == "西班牙语" && nameTo == "西班牙语")
            {
                feedback.txtBlock.TextAlignment = TextAlignment.Left;
                feedback.txtContent.TextAlignment = TextAlignment.Left;
            }
            else
            {
                feedback.txtBlock.TextAlignment = TextAlignment.Center;
                feedback.txtContent.TextAlignment = TextAlignment.Center;
            }

            if (!file)
            {
                string micName = WaveIn.GetCapabilities(combDevice.SelectedIndex).ProductName;

                totalBufferLength = 0;
                recorder = new AudioRecorder();
                if (!micName.StartsWith("Microphone")) recorder.RecordingFormat = new WaveFormat(8000, 2); //External mics work fine. Laptop mic works soso.
                recorder.MicrophoneLevel = 100;
                recorder.BeginMonitoring(combDevice.SelectedIndex);
                recorder.SampleAggregator.MaximumCalculated += OnRecorderMaximumCalculated;

                if (waveIn == null)
                {
                    waveIn = CreateWaveInDevice(micName);
                }
                var device = (MMDevice)combDevice.SelectedItem;
                device.AudioEndpointVolume.Mute = false;

                if (micName.StartsWith("Microphone")) waveIn.WaveFormat = new WaveFormat(16000, 1); //Laptop mic works great. External mics don't work. 
                else waveIn.WaveFormat = new WaveFormat(8000, 2); //External mics work fine. Laptop mic works soso.

                if (name == "中文")
                {
                    // Start receive and send loops
                    var sendAudioRecorded = Task.Run(() => this.StartSending())
                        .ContinueWith((t) => ReportError(t))
                        .ConfigureAwait(false);
                }
                else if (name == "英文" || name == "西班牙语")
                {
                    stopbutton = false;
                    if (this.micClient == null) this.CreateMicrophoneRecoClient();
                    this.micClient.StartMicAndRecognition();
                }

                if (CheckBox_RecordAudio.IsChecked == true && logAudioFileName != null)
                {
                    // Setup player and recorder but don't start them yet.
                    WaveFormat waveFormat;
                    if (micName.StartsWith("Microphone")) waveFormat = new WaveFormat(16000, 1); //Laptop mic works great. External mics don't work. 
                    else waveFormat = new WaveFormat(8000, 2); //External mics work fine. Laptop mic works soso.

                    audioSent = new WaveFileWriter(logAudioFileName, waveFormat);
                    Debug.WriteLine("I: Recording outgoing audio in " + logAudioFileName);
                }
                else CheckBox_RecordAudio.IsEnabled = false;

                waveIn.StartRecording();
            }            
            else if (file)
            {
                if (name == "英文" || name == "西班牙语")
                {
                    if (null == this.dataClient) this.CreateDataRecoClient();
                    this.SendAudioHelper(this.LongWaveFile);
                }
            }
            
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;

            if (CheckBox_RecordAudio.IsChecked == false) CheckBox_RecordAudio.IsEnabled = false;

            CheckBox_Transcript.IsEnabled = false;
            CheckBox_Transcript.IsChecked = false;

            CheckBox_Library.IsEnabled = false;
        }

        private async Task StartSending()
        {
            while (click)
            {
                QueueItem item = null;
                if (this.outgoingMessageQueue.TryTake(out item, 100))
                {
                    try
                    {
                        await IAT.RunIAT(VoiceReady, session_begin_params, ref sd);
                        item.CompletionSource.TrySetResult(true);
                        VoiceReady.Clear();
                    }
                    catch (OperationCanceledException)
                    {
                        item.CompletionSource.TrySetCanceled();
                    }
                    catch (ObjectDisposedException)
                    {
                        item.CompletionSource.TrySetCanceled();
                    }
                    catch (Exception ex)
                    {
                        item.CompletionSource.TrySetException(ex);
                        throw;
                    }
                }
            }
        }

        private void ReportError(Task task)
        {
            if (task.IsFaulted)
            {
                if (this.Failed != null) Failed(this, task.Exception);
            }
        }

        void OnRecorderMaximumCalculated(object sender, MaxSampleEventArgs e)
        {
            lastPeak = Math.Max(e.MaxSample, Math.Abs(e.MinSample)) * 100;
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;

            if (!file)
            {
                waveIn.StopRecording();
                waveIn.Dispose();
                waveIn = null;

                recorder.Stop();

                // Close the audio file if logging
                if (audioSent != null)
                {
                    audioSent.Flush();
                    audioSent.Dispose();
                    audioSent = null;
                }

                logAudioFileName = null;

                if (name == "英文" || name == "西班牙语")
                {
                    this.micClient.EndMicAndRecognition();
                    this.micClient.Dispose();
                    this.micClient = null;
                    stopbutton = true;
                }
            }
            else if (file)
            {
                if (name == "英文" || name == "西班牙语")
                {                   
                    this.dataClient.Dispose();
                    this.dataClient = null;
                }
            }           

            CheckBox_RecordAudio.IsEnabled = true;
            CheckBox_RecordAudio.IsChecked = false;

            CheckBox_Transcript.IsEnabled = true;
            CheckBox_Transcript.IsChecked = false;

            CheckBox_Library.IsEnabled = true;
        }

        private void combFile_selectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (combFile.SelectedIndex == 0)
            {
                OpenFileDialog open = new OpenFileDialog();
                open.ShowDialog();

                combFile.Items.Add(open.FileName);
                combFile.SelectedIndex = combFile.Items.IndexOf(open.FileName);

                file = true;
            }
            else if (combFile.SelectedIndex == 1)
            {
                file = false;
            }

            LongWaveFile = combFile.SelectedValue.ToString();
            Debug.WriteLine(LongWaveFile);
        }

        private void CheckBox_Transcript_Checked(object sender, RoutedEventArgs e)
        {
            if (name == "中文") { sd.WriteDataOnFile(); }
            else if (name == "英文" || name == "西班牙语") { WriteDataOnFile(); }
        }

        private void combFrom_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            name = combFrom.SelectedValue.ToString();

            feedback.serviceFrom = name;
            sd.serviceFrom = name;

            if (name == "英文") { m_language = "en-US"; from = "en"; }
            else if (name == "西班牙语") { m_language = "es-ES"; from = "spa"; }

            if(combTo.SelectedValue!=null) btnStart.IsEnabled = true;
        }

        private void combTo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            nameTo = combTo.SelectedValue.ToString();

            feedback.serviceTo = nameTo;
            sd.serviceTo = nameTo;

            if (nameTo == "中文") { to = "zh"; }
            else if (nameTo == "英文") { to = "en"; sd.g_languageTo = "en"; }
            else if (nameTo == "西班牙语") { to = "spa"; sd.g_languageTo = "es"; }

            if (combFrom.SelectedValue != null) btnStart.IsEnabled = true;
        }

        private string Now() { return DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.ff", DateTimeFormatInfo.InvariantInfo); }

        private void CheckBox_RecordAudio_Checked(object sender, RoutedEventArgs e)
        {
                string logAudioPath = AppDomain.CurrentDomain.BaseDirectory;
                try
                {
                    Directory.CreateDirectory(logAudioPath);
                }
                catch
                {
                    Debug.WriteLine("Could not create folder {0}", logAudioPath);
                }

                logAudioFileName = Path.Combine(logAudioPath, string.Format("audiosent_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".wav"));
        }

        private void CheckBox_Library_Checked(object sender, RoutedEventArgs e)
        {
            library_link = Prompt.Library("Introduce the library link:", "Library");
            if (library_link == "") { CheckBox_Library.IsChecked = false; lib = false; }
            else { lib = true; }
        }

        private void CheckBox_Library_UnChecked(object sender, RoutedEventArgs e)
        {
            lib = false;
        }

        /**************************************** Bing Microphone Client  ****************************************/

        private void CreateMicrophoneRecoClient()
        {
            if (!lib)
            {
                this.subscriptionKey = "5e6*";//Write your own subsciption key for standard service

                this.micClient = SpeechRecognitionServiceFactory.CreateMicrophoneClient(
                this.Mode,
                this.m_language, //English: "en-US", Chinese: "zh-CN", Spanish: "es-ES"
                this.subscriptionKey); //No Custom Speech: ...CreateMicrophoneClient(this.Mode,"en-US",this.subscriptionKey); Custom Speech: ...CreateMicrophoneClient(this.Mode,"en-US",this.subscriptionKey,this.subscriptionKey,this.endpointURL);
                this.micClient.AuthenticationUri = this.AuthenticationUri; //No Custom Speech: = this.AuthenticationUri; Custom Speech: = "https://westus.api.cognitive.microsoft.com/sts/v1.0/issueToken";
            }
            else if (lib)
            {
                this.subscriptionKey = "900*";//Write your own subsciption key for library service
                this.endpointURL = library_link;

                this.micClient = SpeechRecognitionServiceFactory.CreateMicrophoneClient(
                this.Mode,
                this.m_language, //English: "en-US", Chinese: "zh-CN", Spanish: "es-ES"
                this.subscriptionKey,
                this.subscriptionKey,
                this.endpointURL); //No Custom Speech: ...CreateMicrophoneClient(this.Mode,"en-US",this.subscriptionKey); Custom Speech: ...CreateMicrophoneClient(this.Mode,"en-US",this.subscriptionKey,this.subscriptionKey,this.endpointURL);
                this.micClient.AuthenticationUri = "https://westus.api.cognitive.microsoft.com/sts/v1.0/issueToken"; //No Custom Speech: = this.AuthenticationUri; Custom Speech: = "https://westus.api.cognitive.microsoft.com/sts/v1.0/issueToken";
            }

            // Event handlers for speech recognition results
            this.micClient.OnMicrophoneStatus += this.OnMicrophoneStatus;
            this.micClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;
            if (this.Mode == SpeechRecognitionMode.ShortPhrase)
            {
                this.micClient.OnResponseReceived += this.OnMicShortPhraseResponseReceivedHandler;
            }
            else if (this.Mode == SpeechRecognitionMode.LongDictation)
            {
                this.micClient.OnResponseReceived += this.OnMicDictationResponseReceivedHandler;
            }

            this.micClient.OnConversationError += this.OnConversationErrorHandler;
        }

        private void OnMicrophoneStatus(object sender, MicrophoneEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.Recording)
                {
                    Debug.WriteLine("Please start speaking.");
                }
                else if (!e.Recording)
                {
                    //e.Recording = FALSE when: this.micClient.EndMicAndRecognition();
                }
            });
        }

        private void OnPartialResponseReceivedHandler(object sender, PartialSpeechResponseEventArgs e)
        {
            this.WriteLine("{0}", e.PartialResult);
        }

        private void OnMicShortPhraseResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                //this.WriteLine("--- OnMicShortPhraseResponseReceivedHandler ---");

                // we got the final result, so it we can end the mic reco.  No need to do this
                // for dataReco, since we already called endAudio() on it as soon as we were done
                // sending all the data.
                //this.micClient.EndMicAndRecognition();
                Debug.WriteLine("Careful: {0}", e.PhraseResponse.RecognitionStatus);
                Debug.WriteLine("Before StartMic: "+Now());
                this.micClient.StartMicAndRecognition();
                Debug.WriteLine("After StartMic: " + Now());
                Debug.WriteLine("HOLA");
                //this.WriteResponseResult(e);

                //_startButton.IsEnabled = true;
                //_radioGroup.IsEnabled = true;
            }));
        }

        private void OnMicDictationResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            if ((e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout) && !stopbutton)
            {
                Dispatcher.Invoke(
                    (Action)(() =>
                    {
                        Debug.WriteLine("Careful: {0}", e.PhraseResponse.RecognitionStatus);

                        //if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout)
                        //{                            
                            Debug.WriteLine("Before CreateMic: " + Now());
                            if (this.micClient == null) Debug.WriteLine("micClient == NULL!!!!");
                            this.CreateMicrophoneRecoClient();
                            Debug.WriteLine("Before StartMic: " + Now());
                            this.micClient.StartMicAndRecognition();
                            Debug.WriteLine("After StartMic: " + Now());
                        //}
                    }));
            }
            Debug.WriteLine("Recognition Status: {0}", e.PhraseResponse.RecognitionStatus);
            if (e.PhraseResponse.RecognitionStatus.ToString() == "611")
            {
                Debug.WriteLine("THERE YOU GO!");
                this.micClient.EndMicAndRecognition();
                this.CreateMicrophoneRecoClient();
                Debug.WriteLine("Before StartMic: " + Now());
                this.micClient.StartMicAndRecognition();
                Debug.WriteLine("After StartMic: " + Now());
            }
            this.WriteResponseResult(e);
        }

        private void WriteResponseResult(SpeechResponseEventArgs e)
        {
            if (e.PhraseResponse.Results.Length == 0)
            {
                //this.WriteLine("No phrase response is available.");
            }
            else
            {
                //this.WriteLine("********* Final n-BEST Results *********");
                for (int i = 0; i < e.PhraseResponse.Results.Length; i++)
                {
                    this.WriteLine("{0}", e.PhraseResponse.Results[i].DisplayText,i);
                    //this.WriteLine("[{0}] Confidence={1}, Text=\"{2}\"", i, e.PhraseResponse.Results[i].Confidence, e.PhraseResponse.Results[i].DisplayText);
                }
                //this.WriteLine();
            }
        }

        private void WriteLine()
        {
            this.WriteLine(string.Empty);
        }

        private void WriteLine(string format, params object[] args)
        {
            var formattedStr = string.Format(format, args);
            Trace.WriteLine(formattedStr);
            Dispatcher.Invoke(() =>
            {
                string recognition = formattedStr;
                string translation = "";

                if (name != nameTo && (nameTo == "中文" || nameTo == "英文" || nameTo == "西班牙语"))
                {
                    if(flag %15 == 0 || args.Length > 1)
                    {
                        //Start Baidu translate API
                        translation = GetResult(recognition);
                        //End Baidu translate API

                        //feedback.txtContent.Text = translation;
                        feedback.txtContent.Text = buffer + translation + " ";
                        if (args.Length > 1) buffer += translation + "\n";

                        Debug.WriteLine("*");
                        flag = 0;
                    }
                    
                    flag++;
                }
                else
                {
                    if (flag % 2 == 0 || args.Length > 1)
                    { 
                        //feedback.txtContent.Text = recognition;
                        feedback.txtContent.Text = buffer + recognition + " ";
                        if (args.Length > 1) buffer += recognition + /*"\n"*/ " ";

                        Debug.WriteLine("*");

                        flag = 0;
                    }

                    flag++;
                }

                if (args.Length > 1)
                {
                    TranscriptUtterance utterance = new TranscriptUtterance();
                    utterance.Recognition = recognition;
                    utterance.Translation = translation;
                    utterance.Timespan = stopwatch.Elapsed;
                    Transcript.Add(utterance);
                }

                feedback.txtContent.LineDown();
            });

            if (args.Length > 1) { Thread.Sleep(1000); Debug.WriteLine("ID2: {0}", Thread.CurrentThread.ManagedThreadId); }
        }

        private void OnConversationErrorHandler(object sender, SpeechErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                //_startButton.IsEnabled = true;
                //_radioGroup.IsEnabled = true;
            });

            this.WriteLine("--- Error received by OnConversationErrorHandler() ---");
            this.WriteLine("Error code: {0}", e.SpeechErrorCode.ToString());
            this.WriteLine("Error text: {0}", e.SpeechErrorText);
            this.WriteLine();
        }

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
                        file.WriteLine("{0} Recognition: {1}\n", Now(), utterance.Recognition);
                        file.WriteLine("{0} Translation: {1}\n", Now(), utterance.Translation);
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

        /***************************************** Bing Data Client  *****************************************/

        private void CreateDataRecoClient()
        {
            this.subscriptionKey = "5e6*"; //no lib //Write your own subscription key

            this.dataClient = SpeechRecognitionServiceFactory.CreateDataClient(
                this.Mode,
                this.m_language,
                this.subscriptionKey);
            this.dataClient.AuthenticationUri = this.AuthenticationUri;

            // Event handlers for speech recognition results
            if (this.Mode == SpeechRecognitionMode.ShortPhrase)
            {
                this.dataClient.OnResponseReceived += this.OnDataShortPhraseResponseReceivedHandler;
            }
            else
            {
                this.dataClient.OnResponseReceived += this.OnDataDictationResponseReceivedHandler;
            }

            this.dataClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;
            this.dataClient.OnConversationError += this.OnConversationErrorHandler;
        }

        private void OnDataShortPhraseResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                this.WriteLine("--- OnDataShortPhraseResponseReceivedHandler ---");

                // we got the final result, so it we can end the mic reco.  No need to do this
                // for dataReco, since we already called endAudio() on it as soon as we were done
                // sending all the data.
                this.WriteResponseResult(e);

                //_startButton.IsEnabled = true;
                //_radioGroup.IsEnabled = true;
            }));
        }

        private void OnDataDictationResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout)
            {
                Dispatcher.Invoke(
                    (Action)(() =>
                    {
                        //_startButton.IsEnabled = true;
                        //_radioGroup.IsEnabled = true;

                        // we got the final result, so it we can end the mic reco.  No need to do this
                        // for dataReco, since we already called endAudio() on it as soon as we were done
                        // sending all the data.

                        Debug.WriteLine("Careful: {0}", e.PhraseResponse.RecognitionStatus);
                    }));
            }

            this.WriteResponseResult(e);
        }

        private void SendAudioHelper(string wavFileName)
        {
            using (FileStream fileStream = new FileStream(wavFileName, FileMode.Open, FileAccess.Read))
            {
                // Note for wave files, we can just send data from the file right to the server.
                // In the case you are not an audio file in wave format, and instead you have just
                // raw data (for example audio coming over bluetooth), then before sending up any 
                // audio data, you must first send up an SpeechAudioFormat descriptor to describe 
                // the layout and format of your raw audio data via DataRecognitionClient's sendAudioFormat() method.
                int bytesRead = 0;
                byte[] buffer = new byte[1024];

                try
                {
                    do
                    {
                        // Get more Audio data to send into byte buffer.
                        bytesRead = fileStream.Read(buffer, 0, buffer.Length);

                        // Send of audio data to service. 
                        this.dataClient.SendAudio(buffer, bytesRead);
                    }
                    while (bytesRead > 0);
                }
                finally
                {
                    // We are done sending audio.  Final recognition results will arrive in OnResponseReceived event call.
                    this.dataClient.EndAudio();
                }
            }
        }

        /***************************** Baidu translate *****************************/

        public string sign(string q)
        {
             /*get {*/ return string.Format("{0}{1}{2}{3}", appid, q, salt, key); /*}*/
        }

        string getMd5(string b)
        {
             var md5 = new MD5CryptoServiceProvider();
             var result = Encoding.UTF8.GetBytes(sign(b));
             var output = md5.ComputeHash(result);
             return BitConverter.ToString(output).Replace("-", "").ToLower();
        }

        public string GetJson(string q)
        {
            var client = new RestClient("http://api.fanyi.baidu.com");
            var request = new RestRequest("/api/trans/vip/translate", Method.GET);
            request.AddParameter("q", q);
            request.AddParameter("from", from);
            request.AddParameter("to", to);
            request.AddParameter("appid", appid);
            request.AddParameter("salt", salt);
            request.AddParameter("sign", getMd5(q));
            IRestResponse response = client.Execute(request);
            return response.Content;
        }

        public string GetResult(string a)
        {
            var lst = new List<string>();
            var content = GetJson(a);
            dynamic json = JsonConvert.DeserializeObject(content);

            try
            {
                foreach (var item in json.trans_result)
                {
                    lst.Add(item.dst.ToString());
                }
            }
            catch(NullReferenceException e) { Debug.WriteLine("Null Reference Exception detected"); }

            return string.Join(";", lst);
        }

    }
}