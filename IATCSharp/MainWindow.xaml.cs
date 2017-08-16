using Microsoft.Translator.Samples;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VoiceRecorder.Audio;

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

    /**/public string selected_service;
    /**/Feedback feedback = new Feedback();
    /**/SendDataPipe sd = new SendDataPipe();
    /**/string logAudioFileName = null;
    /**/private WaveFileWriter audioSent; //WaveFileWriter is a class                                 
    /**/private BlockingCollection<QueueItem> outgoingMessageQueue = new BlockingCollection<QueueItem>();// Queue of messages waiting to be sent.
    /**/public event EventHandler<Exception> Failed;
    /**/private bool click = false;
    /**/List<VoiceData> VoiceReady = new List<VoiceData>();

        private string name;

        List<VoiceData> VoiceBuffer = new List<VoiceData>();

        int Ends = 5;//梦龙：originally 5

        public MainWindow()
        {
            InitializeComponent();
            this.Topmost = true;

            Left = System.Windows.SystemParameters.PrimaryScreenWidth - 217 - 2;
            Top = System.Windows.SystemParameters.PrimaryScreenHeight - 150 - 40 - 2;

            FormLoad();
            SpeechRecognition();

            feedback.Show();
            
            this.Closing += new CancelEventHandler(OnWindowClosing);
        }

        void OnWindowClosing(object sender, CancelEventArgs e)
        {
            feedback.Close();
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

            combService.Items.Add("语音识别"); //CntoCn
            combService.Items.Add("中英翻译"); //CntoEn
            combService.Items.Add("英中翻译"); //EntoCn

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = false;
            CheckBox_Transcript.IsEnabled = false;
        }

        public void SpeechRecognition()
        {
            //初始化语音识别
            int ret = (int)ErrorCode.MSP_SUCCESS;
            string login_params = string.Format("appid = {0}, work_dir = {1}", ConfigurationManager.AppSettings["AppID"].ToString(), ConfigurationManager.AppSettings["WorkDir"].ToString());
            //session_begin_params = ConfigurationManager.AppSettings["CntoCn"].ToString();

            string Username = ConfigurationManager.AppSettings["Username"].ToString();
            string Password = ConfigurationManager.AppSettings["Password"].ToString();
            ret = MSCDLL.MSPLogin(Username, Password, login_params);
            
            if ((int)ErrorCode.MSP_SUCCESS != ret)
            {
                MessageBox.Show("MSPLogin failed,error code:{0}", ret.ToString());
                MSCDLL.MSPLogout();
            }

            TTS welcome = new TTS();
            welcome.CreateWAV("欢迎使用阿科督");
        }


        private WaveIn CreateWaveInDevice()
        {
            WaveIn newWaveIn = new WaveIn();
            newWaveIn.WaveFormat = new WaveFormat(16000, 1);
            newWaveIn.DataAvailable += OnDataAvailable;
            newWaveIn.RecordingStopped += OnRecordingStopped;
            return newWaveIn;
        }

        void OnDataAvailable(object sender, WaveInEventArgs e)
        {            
        /**/if (audioSent != null)
            {
                audioSent.Write(e.Buffer, 0, e.BytesRecorded);
            }

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
                    //IAT.RunIAT(VoiceBuffer, session_begin_params, ref sd,selected_service);
                    VoiceReady.AddRange(VoiceBuffer);
                    
                    var msg = new QueueItem(VoiceReady, session_begin_params, ref sd, selected_service);
                    this.outgoingMessageQueue.Add(msg);
                }

                VoiceBuffer.Clear();
                Ends = 5;//梦龙：originally 5
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
            click = true;
            this.WindowState = WindowState.Minimized;

            totalBufferLength = 0;
            recorder = new AudioRecorder();
            recorder.BeginMonitoring(combDevice.SelectedIndex);
            recorder.SampleAggregator.MaximumCalculated += OnRecorderMaximumCalculated;

            if (waveIn == null)
            {
                waveIn = CreateWaveInDevice();
            }
            var device = (MMDevice)combDevice.SelectedItem;
            device.AudioEndpointVolume.Mute = false;
            waveIn.WaveFormat = new WaveFormat(16000, 1);


            // Start receive and send loops
            var sendAudioRecorded = Task.Run(() => this.StartSending())
                .ContinueWith((t) => ReportError(t))
                .ConfigureAwait(false);

            // Setup player and recorder but don't start them yet.
        /**/WaveFormat waveFormat = new WaveFormat(16000, 1);

        /**/if (logAudioFileName != null)
            {
                audioSent = new WaveFileWriter(logAudioFileName, waveFormat);
                Debug.WriteLine("I: Recording outgoing audio in " + logAudioFileName);
            }

            waveIn.StartRecording();

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;

            CheckBox_Transcript.IsEnabled = true;
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
                        await IAT.RunIAT(VoiceReady, session_begin_params, ref sd, selected_service,name);
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
            waveIn.StopRecording();

            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;

            // Close the audio file if logging
        /**/if (audioSent != null)
            {
                audioSent.Flush();
                audioSent.Dispose();
                audioSent = null;
            }

            CheckBox_RecordAudio.IsChecked = false;

        }

        //private void btnSpread_Click(object sender, RoutedEventArgs e)
        //{
        //    POINT p = new POINT();
        //    //Point pp = Mouse.GetPosition(e.Source as FrameworkElement);//WPF方法
        //    //Point ppp = (e.Source as FrameworkElement).PointToScreen(pp);//WPF方法
        //    int x = 0, y = 0;
        //    if (GetCursorPos(out p))//API方法
        //    {
        //        x = p.X;
        //    }
        //    y = (int)this.Top;

        //    ShowRecogResult showRecogResult = new ShowRecogResult();
        //    showRecogResult.WindowStartupLocation = WindowStartupLocation.Manual;
        //    showRecogResult.Left = x + 25;
        //    showRecogResult.Top = y;
        //    showRecogResult.Show();
        //}
        
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            sd.WriteDataOnFile();
        }

        private void combService_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            name = combService.SelectedValue.ToString();

            if (name == "语音识别") selected_service = "CntoCn";
            else if (name == "中英翻译") selected_service = "CntoCn"; //Google will do translation
            else if (name == "英中翻译") selected_service = "EntoCn";

            btnStart.IsEnabled = true;
            session_begin_params = ConfigurationManager.AppSettings[selected_service].ToString();
            feedback.service = selected_service;
        }

        private string Now() { return DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.ff", DateTimeFormatInfo.InvariantInfo); }

        private void CheckBox_RecordAudio_Checked(object sender, RoutedEventArgs e)
        {
                string logAudioPath = System.AppDomain.CurrentDomain.BaseDirectory;
                try
                {
                    Directory.CreateDirectory(logAudioPath);
                }
                catch
                {
                    Debug.WriteLine("Could not create folder {0}", logAudioPath);
                }

                logAudioFileName = System.IO.Path.Combine(logAudioPath, string.Format("audiosent_" + DateTime.Now.ToString("yyMMdd_HHmm") + ".wav"));
        }
    }
}
