using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Drawing;
using DSLab;
using System.Windows.Threading;
using System.ComponentModel;
using System.Windows.Forms;
using System.Windows.Interop;
using System.IO;

namespace DocumentCameraTool
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : System.Windows.Forms.IWin32Window
    {

        public MainWindow()
        {
            InitializeComponent();
        }

        //DirectShow用のオブジェクトだよーー
        private IGraphBuilder Graph = null;
        private ICaptureGraphBuilder2 Builder = null;
        private IBaseFilter VideoSource = null;
        private IBaseFilter VideoGrabber = null;
        private IBaseFilter VideoRenderer = null;
        private IAMCameraControl CameraControl = null;
        private CxSampleGrabberCB VideoGrabberCB = new CxSampleGrabberCB();
        private VIDEOINFOHEADER VideoInfoHeader = new VIDEOINFOHEADER();
        private Bitmap[] Buffer = new Bitmap[5];
        private int BufferIndex = 0;

        //タッチの状態を記憶する文字列
        int DownCount = 0;


        //傾き補正に使う変数
        double Angle = 0;
        double DSita = 0;
        double ESita = 0;
        int CountSpin = 0;
        int[] SpinX = new int[3];
        int[] SpinY = new int[3];

        //タイマーのクラスの宣言
        private DispatcherTimer CameraTimer;
        private DispatcherTimer ZoomTimer;
        private DispatcherTimer SoftZoomTimer;

        int ZoomStartX = 0;
        int ZoomStartY = 0;
        int ZoomEndX = 0;
        int ZoomEndY = 0;

        //ディスプレイの高さと幅
        int h = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
        int width = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;

        int TouchID1 = 0;
        int TouchID2 = 0;

        double Point1X = 0;
        double Point1Y = 0;
        double Point2X = 0;
        double Point2Y = 0;
        double P1X = 0;
        double P1Y = 0;
        double P2X = 0;
        double P2Y = 0;

        double PreDistance;

        int MoveCount = 0;

        int First = 0;
        int MoveFlag = 0;
        int Value = 0;
        int FirstCounter = 0;

        double StartMovePointX = 0;
        double StartMovePointY = 0;

        bool SoftFlag = false;
        bool ZoomFlag = false;

        double w = 0;
        double x = 0;
        double y = 0;
        double z = 0;

        int aaaa = 0;

        //windowが表示した時の処理
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            cameraMove.IsEnabled = false;

            //カメラデバイスの選択画面
            var dlg = new CxDeviceSelectionForm(new Guid(GUID.CLSID_VideoInputDeviceCategory));
            dlg.TopMost = true;
            dlg.StartPosition = FormStartPosition.CenterParent;
            if (dlg.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                var filterInfo = dlg.FilterInfos[dlg.FilterIndex];
                var pinno = dlg.PinIndex;
                var frameSize = dlg.FormatInfos[dlg.FormatIndex].VideoSize;

                var win = new SubFunctionWindow();
                win.Owner = this;
                win.Show();

                Camera_Connect(filterInfo, pinno, frameSize);
            }

            //タイマーのスタートぉぉぉぉぉ
            CameraTimer = new DispatcherTimer();
            CameraTimer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            CameraTimer.Tick += new EventHandler(cameraTimer_Tick);
            CameraTimer.Start();


            int min = 0;
            int max = 0;
            int step = 0;
            int def = 0;
            int flag = 0;
            int value = 0;

            #region Zoom
            try
            {
                HRESULT hr;
                var prop = CameraControlProperty.Zoom;
                hr = (HRESULT)CameraControl.GetRange(prop, ref min, ref max, ref step, ref def, ref flag);

                if (hr < HRESULT.S_OK)
                    throw new CxDSException(hr);
                hr = (HRESULT)CameraControl.Get(prop, ref value, ref flag);
                if (hr < HRESULT.S_OK)
                    throw new CxDSException(hr);

                zoomSlider.IsEnabled = true;
                zoomSlider.Minimum = min;
                zoomSlider.Maximum = max;
                zoomSlider.Value = value;

                //startの時は必ずズームを0にするよ――
                try
                {
                    value = 0;
                    hr = (HRESULT)CameraControl.Set(prop, value, flag);
                    if (hr < HRESULT.S_OK)
                        throw new CxDSException(hr);
                }
                catch (System.Exception)
                {
                    zoomSlider.IsEnabled = false;
                }

            }
            catch (System.Exception)
            {
                zoomSlider.IsEnabled = false;
            }
            #endregion

            #region focus
            try
            {
                HRESULT hr;
                var prop = CameraControlProperty.Focus;
                hr = (HRESULT)CameraControl.GetRange(prop, ref min, ref max, ref step, ref def, ref flag);

                if (hr < HRESULT.S_OK)
                    throw new CxDSException(hr);
                hr = (HRESULT)CameraControl.Get(prop, ref value, ref flag);
                if (hr < HRESULT.S_OK)
                    throw new CxDSException(hr);

                focusSlider.IsEnabled = true;
                focusSlider.Minimum = min;
                focusSlider.Maximum = max;
                focusSlider.Value = value;
            }
            catch (System.Exception)
            {
                focusSlider.IsEnabled = false;
            }
            #endregion

            Camera_Start();
        }

        //timerで画像の更新
        private void cameraTimer_Tick(object sender, EventArgs e)
        {
            //コントロールの表示更新:
            bool enable = Camera_Connected;
            bool running = Camera_IsRunning;

            //画像の表示更新:
            var index = this.BufferIndex;
            var image = this.Buffer[index];

            if (image != null)
            {
                this.Buffer[index] = null;
                IntPtr hbitmap = image.GetHbitmap();
                pictureView.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(hbitmap);
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        //windowが閉じたとき
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            //window閉じるときにタイマーのストップ
            CameraTimer.Stop();

            //カメラの接続解除だよ！！( *´艸｀)
            Camera_Disconnect();
        }

        //カメラの接続！！！
        private void Camera_Connect(CxFilterInfo filterInfo, int pinno, System.Drawing.Size frameSize)
        {
            //グラフビルダーの生成:
            {
                Graph = (IGraphBuilder)Axi.CoCreateInstance(GUID.CLSID_FilterGraph);
                if (Graph == null)
                    throw new System.IO.IOException("Failed to create a GraphBuilder.");

                Builder = (ICaptureGraphBuilder2)Axi.CoCreateInstance(GUID.CLSID_CaptureGraphBuilder2);
                if (Builder == null)
                    throw new System.IO.IOException("Failed to create a GraphBuilder.");
                Builder.SetFiltergraph(Graph);
            }

            //映像入力用: ソースフィルタを生成します.
            {
                VideoSource = Axi.CreateFilter(GUID.CLSID_VideoInputDeviceCategory, filterInfo.CLSID, filterInfo.Index);
                if (VideoSource == null)
                    throw new System.IO.IOException("Failed to create a VideoSource.");
                Graph.AddFilter(VideoSource, "VideoSource");

                // フレームサイズを設定します.
                // ※注) この操作は、ピンを接続する前に行う必要があります.
                IPin pin = Axi.FindPin(VideoSource, pinno, PIN_DIRECTION.PINDIR_OUTPUT);
                Axi.SetFormatSize(pin, frameSize.Width, frameSize.Height);
            }

            //映像捕獲用: サンプルグラバーを生成します.
            {
                VideoGrabber = (IBaseFilter)Axi.CoCreateInstance(GUID.CLSID_SampleGrabber);
                if (VideoGrabber == null)
                    throw new System.IO.IOException("Failed to create a VideoGrabber.");
                Graph.AddFilter(VideoGrabber, "VideoGrabber");

                {
                    var grabber = (ISampleGrabber)VideoGrabber;

                    var mt = new AM_MEDIA_TYPE();
                    mt.majortype = new Guid(GUID.MEDIATYPE_Video);
                    mt.subtype = new Guid(GUID.MEDIASUBTYPE_RGB24);
                    mt.formattype = new Guid(GUID.FORMAT_VideoInfo);
                    grabber.SetMediaType(mt);
                    grabber.SetBufferSamples(false);            // サンプルコピー 無効.
                    grabber.SetOneShot(false);                  // One Shot 無効.
                                                                //grabber.SetCallback(VideoGrabberCB, 0);	// 0:SampleCB メソッドを呼び出すよう指示する.
                    grabber.SetCallback(VideoGrabberCB, 1);     // 1:BufferCB メソッドを呼び出すよう指示する.
                }
            }

            //映像出力用: レンダラーを生成します.
            {
                VideoRenderer = (IBaseFilter)Axi.CoCreateInstance(GUID.CLSID_NullRenderer);
                if (VideoRenderer == null)
                    throw new System.IO.IOException("Failed to create a VideoRenderer.");
                Graph.AddFilter(VideoRenderer, "VideoRenderer");
            }

            //フィルタの接続:
            unsafe
            {
                var mediatype = new Guid(GUID.MEDIATYPE_Video);
                var hr = (HRESULT)Builder.RenderStream(IntPtr.Zero, new IntPtr(&mediatype), VideoSource, VideoGrabber, VideoRenderer);
                if (hr < HRESULT.S_OK)
                    throw new CxDSException(hr);
            }

            // 同期用: サンプルグラバーのイベント登録:
            VideoGrabberCB.Enable = true;
            VideoGrabberCB.Notify += VideoGrabberCB_Notify;
            VideoInfoHeader = Axi.GetVideoInfo((ISampleGrabber)VideoGrabber);

            // カメラ制御インターフェースの抽出.
            CameraControl = Axi.GetInterface<IAMCameraControl>(this.Graph);
        }

        //バッファ
        private void VideoGrabberCB_Notify(object sender, CxSampleGrabberEventArgs e)
        {
            var index = (this.BufferIndex + 1) % this.Buffer.Length;
            var image = e.ToImage(VideoInfoHeader);
            this.Buffer[index] = image;
            this.BufferIndex = index;
        }

        //カメラの接続解除！！
        private void Camera_Disconnect()
        {
            if (Camera_IsRunning)
                Camera_Stop();

            // 同期用: サンプルグラバーのイベント登録解除:
            VideoGrabberCB.Enable = false;
            VideoGrabberCB.Notify -= VideoGrabberCB_Notify;

            #region 解放:
            if (CameraControl != null)
                Marshal.ReleaseComObject(CameraControl);
            CameraControl = null;

            if (VideoSource != null)
                Marshal.ReleaseComObject(VideoSource);
            VideoSource = null;

            if (VideoGrabber != null)
                Marshal.ReleaseComObject(VideoGrabber);
            VideoGrabber = null;

            if (VideoRenderer != null)
                Marshal.ReleaseComObject(VideoRenderer);
            VideoRenderer = null;

            if (Builder != null)
                Marshal.ReleaseComObject(Builder);
            Builder = null;

            if (Graph != null)
                Marshal.ReleaseComObject(Graph);
            Graph = null;
            #endregion
        }

        //カメラの接続状態は？？
        private bool Camera_Connected
        {
            get { return (Graph != null); }
        }

        //カメラの動作状態は？？
        private bool Camera_IsRunning
        {
            get
            {
                var mediaControl = (IMediaControl)Graph;
                if (mediaControl == null) return false;

                try
                {
                    int state = 0;
                    int hr = mediaControl.GetState(0, out state);
                    if (hr < 0)
                        return false;
                    return (
                        state == (int)FILTER_STATE.Running ||
                        state == (int)FILTER_STATE.Paused);
                }
                catch (System.Exception)
                {
                    return false;
                }
            }
        }

        //カメラを一時停止！！！
        private bool Camera_IsPaused
        {
            get
            {
                var mediaControl = (IMediaControl)Graph;
                if (mediaControl == null) return false;
                try
                {
                    int state = 0;
                    int hr = mediaControl.GetState(0, out state);
                    if (hr < 0)
                        return false;
                    return (state == (int)FILTER_STATE.Paused);
                }
                catch (System.Exception)
                {
                    return false;
                }
            }
        }

        //カメラスター――ート―――――
        private void Camera_Start()
        {
            var prop = CameraControlProperty.Focus;
            int flag = (int)CameraControlFlags.Manual;
            int value = 110;
            try
            {
                HRESULT hr;
                hr = (HRESULT)CameraControl.Set(prop, value, flag);
                if (hr < HRESULT.S_OK)
                    throw new CxDSException(hr);
            }
            catch (System.Exception)
            {
                focusSlider.IsEnabled = false;
            }

            var mediaControl = (IMediaControl)Graph;
            if (mediaControl == null) return;

            mediaControl.Run();
            int state = 0;
            mediaControl.GetState(3000, out state);
        }

        //カメラの露光停止
        private void Camera_Stop()
        {
            var mediaControl = (IMediaControl)Graph;
            if (mediaControl == null) return;

            mediaControl.Stop();
            int state = 0;
            mediaControl.GetState(3000, out state);
        }

        //カメラの制御できる情報の幅を確認！
        private bool Camera_IsSupported(CameraControlProperty prop)
        {
            if (CameraControl == null) return false;

            try
            {
                #region レンジの取得を試みる.
                int min = 0;
                int max = 0;
                int step = 0;
                int def = 0;
                int flag = 0;

                var hr = (HRESULT)CameraControl.GetRange(prop, ref min, ref max, ref step, ref def, ref flag);

                if (hr < HRESULT.S_OK)
                    return false;

                return true;
                #endregion
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public IntPtr Handle { get; private set; }

        public MainWindow(Window window)
        {
            this.Handle = new WindowInteropHelper(window).Handle;
        }

        //画面に指が触れたとき
        private void pictureView_TouchDown(object sender, TouchEventArgs e)
        {
            TouchPoint p = e.GetTouchPoint(this);

            if (DownCount == 0)
            {
                double tPointX = p.Position.X;
                double tPointY = p.Position.Y;

                TouchID1 = p.TouchDevice.Id;

                SpinX[CountSpin] = (int)tPointX;
                SpinY[CountSpin] = (int)tPointY;

                DownCount++;
                CountSpin++;
                MoveCount = 0;

                if (FirstCounter == 0)
                {
                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                    using (FileStream stream = new FileStream(System.IO.Path.Combine(dir, "test.bmp"), FileMode.Create))
                    {
                        ImageSource source = pictureView.Source;
                        BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create((BitmapSource)source));
                        encoder.Save(stream);
                    }
                    FirstCounter++;
                }
            }

            else if (DownCount == 1)
            {
                double tPointX = p.Position.X;
                double tPointY = p.Position.Y;

                SpinX[CountSpin] = (int)tPointX;
                SpinY[CountSpin] = (int)tPointY;

                TouchID2 = p.TouchDevice.Id;

                DownCount = 0;
                CountSpin = 0;
            }
        }

        //ドラッグ操作中
        private void pictureView_TouchMove(object sender, TouchEventArgs e)
        {
            MoveCount++;
            TouchPoint p = e.GetTouchPoint(this);
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            if (DownCount == 0)
            {
                if (MoveCount == 20)
                {
                    PreDistance = Math.Sqrt(Math.Pow(Point1X - Point2X, 2) + Math.Pow(Point1Y - Point2Y, 2));

                    ZoomTimer = new DispatcherTimer();
                    ZoomTimer.Interval = new TimeSpan(0, 0, 0, 0, 3);
                    ZoomTimer.Tick += new EventHandler(zoomTimer_Tick);
                    ZoomTimer.Start();
                }

                else if(MoveCount / 20 == 0)
                {
                    if (e.TouchDevice.Id == TouchID1)
                    {
                        double P1X = p.Position.X;
                        double P1Y = p.Position.Y;
                    }

                    else if (e.TouchDevice.Id == TouchID2)
                    {
                        double P2X = p.Position.X;
                        double P2Y = p.Position.Y;

                        double NowDistance = Math.Sqrt(Math.Pow(P1X - P2X, 2) + Math.Pow(P1Y - P2Y, 2));
                        if (PreDistance < NowDistance)
                        {
                            ZoomFlag = true;
                            PreDistance = NowDistance;
                        }
                        else if (PreDistance > NowDistance)
                        {
                            ZoomFlag = false;
                            PreDistance = NowDistance;
                        }
                    } 
                }
            }

            else if (DownCount == 1)
            {
                if(MoveCount == 20)
                {
                    StartMovePointX = p.Position.X;
                    StartMovePointY = p.Position.Y;

                    if (0 < Value && Value <= 10)
                    {
                        Console.WriteLine("1");
                        w = -10;
                        x = -10;
                        y = -10;
                        z = -10;
                        Thickness margin = new Thickness(w, x, y, z);
                        cameraMove.Margin = margin;
                    }
                    else if (10 < Value && Value <= 20)
                    {
                        Console.WriteLine("2");
                        w = -200;
                        x = -200;
                        y = -200;
                        z = -200;
                        Thickness margin = new Thickness(w, x, y, z);
                        cameraMove.Margin = margin;
                    }
                    else if (20 < Value && Value <= 40)
                    {
                        Console.WriteLine("3");
                        w = -400;
                        x = -400;
                        y = -400;
                        z = -400;
                        Thickness margin = new Thickness(w, x, y, z);
                        cameraMove.Margin = margin;
                    }
                    else if (40 < Value && Value <= 60)
                    {
                        Console.WriteLine("4");
                        w = -600;
                        x = -600;
                        y = -600;
                        z = -600;
                        Thickness margin = new Thickness(w, x, y, z);
                        cameraMove.Margin = margin;
                    }
                    else if (60 < Value && Value <= 80)
                    {
                        Console.WriteLine("5");
                        w = -750;
                        x = -750;
                        y = -750;
                        z = -750;
                        Thickness margin = new Thickness(w, x, y, z);
                        cameraMove.Margin = margin;
                    }
                    else if (80 < Value && Value <= 110)
                    {
                        Console.WriteLine("6");
                        w = -1000;
                        x = -1000;
                        y = -1000;
                        z = -1000;
                        Thickness margin = new Thickness(w, x, y, z);
                        cameraMove.Margin = margin;
                    }
                    else if (110 < Value && Value <= 130)
                    {
                        Console.WriteLine("7");
                        w = -1200;
                        x = -1200;
                        y = -1200;
                        z = -1200;
                        Thickness margin = new Thickness(w, x, y, z);
                        cameraMove.Margin = margin;
                    }

                    pictureView.Visibility = Visibility.Hidden;
                    cameraMove.Visibility = Visibility.Visible;
                    cameraMove.Source = new BitmapImage(new Uri(dir + "\\test.bmp"));
                    cameraMove.RenderTransform = new RotateTransform { Angle = Angle, CenterX = width / 2, CenterY = h / 2 };
                    SoftFlag = true;
                    MoveCount = 0;
                    SoftMove((int)StartMovePointX, (int)StartMovePointY);
                }
            }
        }

        private void zoomTimer_Tick(object sender, EventArgs e)
        {
            ZoomInFunction();
            if(ZoomFlag == true)
            {
                Value = Value + 2;
                Console.WriteLine(Value);
            }
            else if(ZoomFlag == false)
            {
                Value--;
                Console.WriteLine("bbbb");
            }
        }

        public void ZoomInFunction()
        {
            try
            {
                var prop = CameraControlProperty.Zoom;
                int flag = (int)CameraControlFlags.Manual;
                int value = Value;
                
                HRESULT hr;
                hr = (HRESULT)CameraControl.Set(prop, value, flag);
                if (hr < HRESULT.S_OK)
                    throw new CxDSException(hr);

            }
            catch (System.Exception)
            {
                zoomSlider.IsEnabled = false;
            }
        }

        //画面から指が離れたとき。ピンチならそのまま。傾き補正なら座標取るよ
        private void pictureView_TouchUp(object sender, TouchEventArgs e)
        {
            if (MoveCount < 20)
            {
                //Console.WriteLine("up");
                if (DownCount == 0)
                {
                    double Vector_A = Math.Sqrt((SpinX[0] - SpinX[1]) * (SpinX[0] - SpinX[1]) + (SpinY[0] - SpinY[1]) * (SpinY[0] - SpinY[1]));
                    double Vector_B = Math.Sqrt((SpinX[0] - SpinX[1]) * (SpinX[0] - SpinX[1]) + (SpinY[1] - SpinY[1]) * (SpinY[1] - SpinY[1]));
                    double InnerProduct = (SpinX[0] - SpinX[1]) * (SpinX[0] - SpinX[1]) + (SpinY[0] - SpinY[1]) * (SpinY[1] - SpinY[1]);

                    double CosSita = InnerProduct / (Vector_A * Vector_B);
                    double Sita = Math.Acos(CosSita);
                    if (SpinX[0] < SpinX[1] && SpinY[0] > SpinY[1])
                    {
                        DSita = Sita * 180 / Math.PI;
                    }
                    else if (SpinX[0] > SpinX[1] && SpinY[0] < SpinY[1])
                    {
                        DSita = Sita * 180 / Math.PI;
                    }
                    else if (SpinX[0] < SpinX[1] && SpinY[0] < SpinY[1])
                    {
                        ESita = -Sita * 180 / Math.PI;
                    }
                    else
                    {
                        ESita = -Sita * 180 / Math.PI;
                    }

                    if (SpinX[0] < SpinX[1] && SpinY[0] > SpinY[1])
                    {
                        Angle = Angle + DSita;
                    }
                    else if (SpinX[0] > SpinX[1] && SpinY[0] < SpinY[1])
                    {
                        Angle = Angle + DSita;
                    }
                    else if (SpinX[0] < SpinX[1] && SpinY[0] < SpinY[1])
                    {
                        Angle = Angle + ESita;
                    }
                    else
                    {
                        Angle = Angle + ESita;
                    }

                    pictureView.RenderTransform = new RotateTransform { Angle = Angle, CenterX = width / 2, CenterY = h / 2 };
                    CountSpin = 0;
                }
            }

            else if(MoveCount >= 20)
            {
                Console.WriteLine("何もしないタッチアップ");
                First = 0;
                MoveFlag = 0;
                win.border.BorderBrush = null;
                cameraMove.Source = null;
                win.border.BorderBrush = null;
                CountSpin = 0;
                ZoomTimer.Stop();
                if (Value < 0)
                {
                    Value = 0;
                }

                //var prop = CameraControlProperty.Focus;
                //int flag = (int)CameraControlFlags.Manual;
                //int value = 110;
                //if (Value >= 0 && Value < 10)
                //{
                //    value = 110;
                //    CountSpin = 0;
                //}
                //else if (Value >= 10 && Value < 20)
                //{
                //    value = 108;
                //    CountSpin = 0;
                //}
                //if (Value >= 20 && Value < 30)
                //{
                //    value = 105;
                //    CountSpin = 0;
                //}
                //if (Value >= 30 && Value < 40)
                //{
                //    value = 103;
                //    CountSpin = 0;
                //}
                //if (Value >= 40 && Value < 50)
                //{
                //    value = 100;
                //    CountSpin = 0;
                //}
                //if (Value >= 50 && Value < 60)
                //{
                //    value = 98;
                //    CountSpin = 0;
                //}
                //if (Value >= 60 && Value < 70)
                //{
                //    value = 95;
                //    CountSpin = 0;
                //}
                //if (Value >= 70 && Value < 80)
                //{
                //    value = 94;
                //    CountSpin = 0;
                //}
                //if (Value >= 80 && Value < 90)
                //{
                //    value = 92;
                //    CountSpin = 0;
                //}
                //if (Value >= 90 && Value < 100)
                //{
                //    value = 90;
                //    CountSpin = 0;
                //}

                //try
                //{
                //    HRESULT hr;
                //    hr = (HRESULT)CameraControl.Set(prop, value, flag);
                //    if (hr < HRESULT.S_OK)
                //        throw new CxDSException(hr);
                //}
                //catch (System.Exception)
                //{
                //    focusSlider.IsEnabled = false;
                //}
            }
        }

        private void cameraMove_TouchMove(object sender, TouchEventArgs e)
        {
            MoveCount++;
            TouchPoint p = e.GetTouchPoint(this);

            if (DownCount == 0)
            {
                Console.WriteLine("check");
                if (MoveCount == 20)
                {
                    PreDistance = Math.Sqrt(Math.Pow(Point1X - Point2X, 2) + Math.Pow(Point1Y - Point2Y, 2));
                    Console.WriteLine("タイマースタート");
                    SoftZoomTimer = new DispatcherTimer();
                    SoftZoomTimer.Interval = new TimeSpan(0, 0, 0, 0, 3);
                    SoftZoomTimer.Tick += new EventHandler(softZoomTimer_Tick);
                    SoftZoomTimer.Start();
                }

                else if (MoveCount / 20 == 0)
                {
                    if (e.TouchDevice.Id == TouchID1)
                    {
                        double P1X = p.Position.X;
                        double P1Y = p.Position.Y;
                    }

                    else if (e.TouchDevice.Id == TouchID2)
                    {
                        double P2X = p.Position.X;
                        double P2Y = p.Position.Y;

                        double NowDistance = Math.Sqrt(Math.Pow(P1X - P2X, 2) + Math.Pow(P1Y - P2Y, 2));
                        if (PreDistance < NowDistance)
                        {
                            ZoomFlag = true;
                            PreDistance = NowDistance;
                        }
                        else if (PreDistance > NowDistance)
                        {
                            ZoomFlag = false;
                            PreDistance = NowDistance;
                        }
                    }
                }
            }

            else if (DownCount == 1)
            {
                if (MoveCount > 20)
                {
                    SoftMove((int)p.Position.X, (int)p.Position.Y);
                }
            }
        }

        private void cameraMove_TouchUp(object sender, TouchEventArgs e)
        {
            if (MoveCount < 20)
            {
                if (DownCount == 0)
                {
                    Console.WriteLine("ソフトで回転");
                    double Vector_A = Math.Sqrt((SpinX[0] - SpinX[1]) * (SpinX[0] - SpinX[1]) + (SpinY[0] - SpinY[1]) * (SpinY[0] - SpinY[1]));
                    double Vector_B = Math.Sqrt((SpinX[0] - SpinX[1]) * (SpinX[0] - SpinX[1]) + (SpinY[1] - SpinY[1]) * (SpinY[1] - SpinY[1]));
                    double InnerProduct = (SpinX[0] - SpinX[1]) * (SpinX[0] - SpinX[1]) + (SpinY[0] - SpinY[1]) * (SpinY[1] - SpinY[1]);

                    double CosSita = InnerProduct / (Vector_A * Vector_B);
                    double Sita = Math.Acos(CosSita);
                    if (SpinX[0] < SpinX[1] && SpinY[0] > SpinY[1])
                    {
                        DSita = Sita * 180 / Math.PI;
                    }
                    else if (SpinX[0] > SpinX[1] && SpinY[0] < SpinY[1])
                    {
                        DSita = Sita * 180 / Math.PI;
                    }
                    else if (SpinX[0] < SpinX[1] && SpinY[0] < SpinY[1])
                    {
                        ESita = -Sita * 180 / Math.PI;
                    }
                    else
                    {
                        ESita = -Sita * 180 / Math.PI;
                    }

                    if (SpinX[0] < SpinX[1] && SpinY[0] > SpinY[1])
                    {
                        Angle = Angle + DSita;
                    }
                    else if (SpinX[0] > SpinX[1] && SpinY[0] < SpinY[1])
                    {
                        Angle = Angle + DSita;
                    }
                    else if (SpinX[0] < SpinX[1] && SpinY[0] < SpinY[1])
                    {
                        Angle = Angle + ESita;
                    }
                    else
                    {
                        Angle = Angle + ESita;
                    }

                    cameraMove.RenderTransform = new RotateTransform { Angle = Angle, CenterX = width / 2, CenterY = h / 2 };
                    CountSpin = 0;
                    MoveCount = 0;
                }
            }

            else if (MoveCount >= 20)
            {
                Console.WriteLine("普通にタッチアップ");
                //MoveCount = 0;
                win.border.BorderBrush = null;
                win.border.BorderBrush = null;
                CountSpin = 0;
                //MoveCount = 0;
                SoftFlag = false;
                //cameraMove.Source = null;
                win.border.BorderBrush = null;
                DownCount = 0;
                aaaa++;
                if(aaaa >= 1)
                {
                    //SoftZoomTimer.Stop();
                    MoveCount = 0;
                }
            }
        }

        private void cameraMove_TouchDown(object sender, TouchEventArgs e)
        {
            SoftFlag = true;
            TouchPoint p = e.GetTouchPoint(this);

            if (DownCount == 0)
            {
                Console.WriteLine("1点目ダウン");
                double tPointX = p.Position.X;
                double tPointY = p.Position.Y;

                TouchID1 = p.TouchDevice.Id;

                SpinX[CountSpin] = (int)tPointX;
                SpinY[CountSpin] = (int)tPointY;

                DownCount++;
                CountSpin++;
            }

            else if (DownCount == 1)
            {
                Console.WriteLine("2点目ダウン");
                double tPointX = p.Position.X;
                double tPointY = p.Position.Y;

                SpinX[CountSpin] = (int)tPointX;
                SpinY[CountSpin] = (int)tPointY;

                TouchID2 = p.TouchDevice.Id;

                DownCount = 0;
                CountSpin = 0;
            }
        }

        private void softZoomTimer_Tick(object sender, EventArgs e)
        {
            if (ZoomFlag == true)
            {
                w-=2;
                x-=2;
                y-=2;
                z-=2;
                cameraMove.Margin = new Thickness(w, x, y, z);
                Console.WriteLine("aaaaa");
            }
            else if (ZoomFlag == false)
            {
                w+=2;
                x+=2;
                y+=2;
                z+=2;
                cameraMove.Margin = new Thickness(w, x, y, z);
                Console.WriteLine("bbbbb");
            }
        }

        public void SoftMove(int a, int b)
        {
            cameraMove.IsEnabled = true;
            Thickness margin = new Thickness(w, x, y, z);
            cameraMove.Margin = margin;
            //cameraMove.Source = pictureView.Source;
            win.border.BorderBrush = new SolidColorBrush(Colors.Yellow);
            win.border.BorderThickness = new Thickness(15, 15, 15, 15);

            double transX = (StartMovePointX - a);
            double transY = (StartMovePointY - b);
            //w = cameraMove.Margin.Left;
            //x = cameraMove.Margin.Top;
            //y = cameraMove.Margin.Right;
            //z = cameraMove.Margin.Bottom;

            //Console.WriteLine(a + "," + b);

            if (a <= 0 && b <= 0)
            {
                cameraMove.Margin = new Thickness(w + transX, x + transY, y - transX, z - transY);
            }

            else if (a < 0 && b > 0)
            {
                cameraMove.Margin = new Thickness(w + transX, x - transY, y - transX, z + transY);
            }

            else if (a > 0 && b < 0)
            {
                cameraMove.Margin = new Thickness(w - transX, x + transY, y + transX, z - transY);
            }

            else if (a > 0 && b > 0)
            {
                cameraMove.Margin = new Thickness(w - transX, x - transY, y + transX, z + transY);
            }

        }

        public void ZoomMove(int dis)
        {
            int a = 0;
            MoveFlag = MoveCount % 2;

            if (MoveCount == 1)
            {
                return;
            }
            else if (MoveFlag == 0)
            {
                First = dis;
            }
            else if (MoveFlag == 1)
            {
                a = dis - First;
                Value = Value + a;
            }
        }

        #region 補助機能を色々

        public void WindowClose()
        {
            this.Close();
        }

        public void zoomArea(int x, int y, int xx, int yy)
        {
            if (x < xx && y < yy)
            {
                ZoomStartX = x;
                ZoomStartY = y;
                ZoomEndX = xx;
                ZoomEndY = yy;
            }
            else if (x < xx && y > yy)
            {
                ZoomStartX = x;
                ZoomStartY = yy;
                ZoomEndX = xx;
                ZoomEndY = y;
            }
            else if (x > xx && y < yy)
            {
                ZoomStartX = xx;
                ZoomStartY = y;
                ZoomEndX = x;
                ZoomEndY = yy;
            }
            else if (x > xx && y > yy)
            {
                ZoomStartX = xx;
                ZoomStartY = yy;
                ZoomEndX = x;
                ZoomEndY = y;
            }

            int zoomWidth = ZoomEndX - ZoomStartX;
            int zoomHeight = ZoomEndY - ZoomStartY;
            //int centerX = zoomStartX + (zoomWidth / 2);
            //int centerY = zoomStartY + (zoomHeight / 2);
            float scaleX = (float)width / (float)zoomWidth;
            float scaleY = (float)h / (float)zoomHeight;

            pictureView.RenderTransform = new ScaleTransform { CenterX = ZoomStartX, CenterY = ZoomStartY, ScaleX = scaleX, ScaleY = scaleY };
        }

        public void funcionReset()
        {
            cameraMove.Source = null;
            pictureView.Visibility = Visibility.Visible;
            var prop = CameraControlProperty.Zoom;
            int flag = (int)CameraControlFlags.Manual;
            int value = 0;
            HRESULT hr;
            hr = (HRESULT)CameraControl.Set(prop, value, flag);

            if (hr < HRESULT.S_OK)
                throw new CxDSException(hr);

            var prop2 = CameraControlProperty.Focus;
            int flag2 = (int)CameraControlFlags.Manual;
            int value2 = 110;

            try
            {
                HRESULT hr2;
                hr2 = (HRESULT)CameraControl.Set(prop2, value2, flag2);
                if (hr2 < HRESULT.S_OK)
                    throw new CxDSException(hr2);
            }
            catch (System.Exception)
            {
                focusSlider.IsEnabled = false;
            }

            Angle = 0;
            pictureView.RenderTransform = new RotateTransform { Angle = Angle, CenterX = width / 2, CenterY = h / 2 };
        }

        public void CameraStop()
        {
            imageCapure.Source = pictureView.Source;
            imageCapure.RenderTransform = new RotateTransform { Angle = Angle, CenterX = width / 2, CenterY = h / 2 };
            win.border.BorderBrush = new SolidColorBrush(Colors.Black);
            win.border.BorderThickness = new Thickness(15, 15, 15, 15);
        }

        public void CameraStart()
        {
            imageCapure.Source = null;
            win.border.BorderBrush = null;
        }

        public void CameraReset()
        {
        }

        #endregion

        #region おまけ

        private void zoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void focusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void exposureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }
        #endregion
    }
}