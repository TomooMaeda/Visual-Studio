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

        int ZoomStartX = 0;
        int ZoomStartY = 0;
        int ZoomEndX = 0;
        int ZoomEndY = 0;

        //ディスプレイの高さと幅
        int h = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
        int w = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;

        int TouchID1 = 0;
        int TouchID2 = 0;
        int[] id = new int[2];



        //windowが表示した時の処理
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            cameraZoom.IsEnabled = false;
            //win.IsManipulationEnabled = false;

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
            {
                bool enable = Camera_Connected;
                bool running = Camera_IsRunning;
            }

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

        ////タッチイベント開始のお知らせ
        //private void win_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        //{
        //    Console.WriteLine("mani");
        //    e.ManipulationContainer = this;
        //    e.Handled = true;
        //    e.IsSingleTouchEnabled = false;

        //    //if (downCount == 1 || downCount == 2)
        //    //{
        //    //    e.Cancel();
        //    //}
        //}

        ////タッチイベント中に実行されるよ
        //private void win_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        //{
        //    Console.WriteLine("manidelta");
        //    double scale = Math.Max(e.DeltaManipulation.Scale.X, e.DeltaManipulation.Scale.Y);

        //    double w = cameraZoom.Margin.Left;
        //    double x = cameraZoom.Margin.Top;
        //    double y = cameraZoom.Margin.Right;
        //    double z = cameraZoom.Margin.Bottom;

        //    if (countSpin == 0)
        //    {
        //        cameraZoom.IsEnabled = true;
        //        cameraZoom.Source = pictureView.Source;
        //        cameraZoom.RenderTransform = new RotateTransform { Angle = angle, CenterX = w / 2, CenterY = h / 2 };
        //        win.border.BorderBrush = new SolidColorBrush(Colors.Red);
        //        win.border.BorderThickness = new Thickness(15, 15, 15, 15);

        //        //Expansionは変位を取得
        //        //Lengthによりその長さを取得
        //        int distance = (int)e.DeltaManipulation.Expansion.Length;
        //        //double transX = e.DeltaManipulation.Translation.X;
        //        //double transY = e.DeltaManipulation.Translation.Y;

        //        //if (transX >= 0 && transY >= 0)
        //        //{
        //        //    Console.WriteLine("1");
        //        //    cameraZoom.Margin = new Thickness(w + transX, x + transY, y - transX, z - transY);
        //        //}

        //        //else if (transX > 0 && transY < 0)
        //        //{
        //        //    Console.WriteLine("2");
        //        //    cameraZoom.Margin = new Thickness(w + transX, x - transY, y - transX, z + transY);
        //        //}

        //        //else if (transX < 0 && transY > 0)
        //        //{
        //        //    Console.WriteLine("3");
        //        //    cameraZoom.Margin = new Thickness(w - transX, x + transY, y + transX, z - transY);
        //        //}

        //        //else if (transX < 0 && transY < 0)
        //        //{
        //        //    Console.WriteLine("4");
        //        //    cameraZoom.Margin = new Thickness(w - transX, x - transY, y + transX, z + transY);
        //        //}

        //        //ExpansionはどうやらVevtor型らしい。zoomoutは正zoominは負だよ
        //        if (e.DeltaManipulation.Expansion.X > 0)
        //        {
        //            zoomX = zoomX + distance;
        //            cameraZoom.Margin = new Thickness(w - zoomX, x - zoomX, y - zoomX, z - zoomX);
        //        }
        //        else if (e.DeltaManipulation.Expansion.X <= 0)
        //        {
        //            zoomX = zoomX - distance;
        //            cameraZoom.Margin = new Thickness(w + zoomX, x + zoomX, y + zoomX, z + zoomX);
        //        }

        //        try
        //        {
        //            var prop = CameraControlProperty.Zoom;
        //            int flag = (int)CameraControlFlags.Manual;
        //            int value = zoomX;

        //            //zoom値が0以下の時は0にしてそれ以下の変化をお断り！！
        //            //zoom値が255以上の時は255にしてそれ以上の変化をお断り！！
        //            if (value > 0 && value < 256)
        //            {
        //                value = zoomX;
        //            }
        //            else if (value < 0)
        //            {
        //                value = 0;
        //                zoomX = 0;
        //                cameraZoom.Margin = new Thickness(0, 0, 0, 0);
        //            }
        //            else if (value > 255)
        //            {
        //                value = 255;
        //                zoomX = 255;
        //            }

        //            HRESULT hr;
        //            hr = (HRESULT)CameraControl.Set(prop, value / 5, flag);
        //            if (hr < HRESULT.S_OK)
        //                throw new CxDSException(hr);

        //        }
        //        catch (System.Exception)
        //        {
        //            zoomSlider.IsEnabled = false;
        //        }
        //    }
        //}

        //private void win_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e)
        //{
        //    cameraZoom.Source = null;
        //    win.border.BorderBrush = null;
        //    prevTouch = "";

        //    return;
        //}

        //画面に指が触れたとき

        private void pictureView_TouchDown(object sender, TouchEventArgs e)
        {
            Console.WriteLine("down");
            if (DownCount == 0)
            {
                TouchPoint p = e.GetTouchPoint(this);
                double tPointX = p.Position.X;
                double tPointY = p.Position.Y;
                TouchID1 = p.TouchDevice.Id;
                Console.WriteLine(TouchID1);
                Console.WriteLine(p.Position.X);

                SpinX[CountSpin] = (int)tPointX;
                SpinY[CountSpin] = (int)tPointY;
                id[0] = TouchID1; 

                DownCount++;
                CountSpin++;
            }
            
            else if(DownCount == 1)
            {
                TouchPoint p = e.GetTouchPoint(this);
                double tPointX = p.Position.X;
                double tPointY = p.Position.Y;

                SpinX[CountSpin] = (int)tPointX;
                SpinY[CountSpin] = (int)tPointY;

                TouchID2 = p.TouchDevice.Id;
                id[1] = TouchID2;
                Console.WriteLine(TouchID2);
                Console.WriteLine(p.Position.X);

                DownCount = 0;
                CountSpin = 0;
            }
        }

        private void pictureView_TouchMove(object sender, TouchEventArgs e)
        {
            TouchPoint p = e.GetTouchPoint(this);
            double PointX = p.Position.X;

            if (e.TouchDevice.Id == TouchID1)
            {
                Console.WriteLine("a");
                Console.WriteLine(PointX);
            }

            else if (e.TouchDevice.Id == TouchID2)
            {
                Console.WriteLine("b");
                Console.WriteLine(p.Position.X);
            }

            double Distance = Math.Sqrt(Math.Pow(SpinX[1] - SpinX[0], 2) + Math.Pow(SpinY[1] - SpinY[0], 2));
            Console.WriteLine(Distance);
        }

        //画面から指が離れたとき。ピンチならそのまま。傾き補正なら座標取るよ
        private void pictureView_TouchUp(object sender, TouchEventArgs e)
        {
            Console.WriteLine("up");
            if (DownCount == 0)
            {
                Console.WriteLine("2tennmespin");

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

                pictureView.RenderTransform = new RotateTransform { Angle = Angle, CenterX = w / 2, CenterY = h / 2 };
                CountSpin = 0;
                DownCount = 0;
            }

        
            //if (DownCount == 1)
            //{
            //    Console.WriteLine("zoomfinish");
            //    CountSpin = 0;
            //    DownCount = 0;

            //    var prop = CameraControlProperty.Focus;
            //    int flag = (int)CameraControlFlags.Manual;
            //    int value = 110;
            //    if (ZoomValue >= 0 && ZoomValue < 10)
            //    {
            //        value = 110;
            //        CountSpin = 0;
            //    }
            //    else if (ZoomValue >= 10 && ZoomValue < 20)
            //    {
            //        value = 108;
            //        CountSpin = 0;
            //    }
            //    if (ZoomValue >= 20 && ZoomValue < 30)
            //    {
            //        value = 105;
            //        CountSpin = 0;
            //    }
            //    if (ZoomValue >= 30 && ZoomValue < 40)
            //    {
            //        value = 103;
            //        CountSpin = 0;
            //    }
            //    if (ZoomValue >= 40 && ZoomValue < 50)
            //    {
            //        value = 100;
            //        CountSpin = 0;
            //    }
            //    if (ZoomValue >= 50 && ZoomValue < 60)
            //    {
            //        value = 98;
            //        CountSpin = 0;
            //    }
            //    if (ZoomValue >= 60 && ZoomValue < 70)
            //    {
            //        value = 95;
            //        CountSpin = 0;
            //    }
            //    if (ZoomValue >= 70 && ZoomValue < 80)
            //    {
            //        value = 94;
            //        CountSpin = 0;
            //    }
            //    if (ZoomValue >= 80 && ZoomValue < 90)
            //    {
            //        value = 92;
            //        CountSpin = 0;
            //    }
            //    if (ZoomValue >= 90 && ZoomValue < 100)
            //    {
            //        value = 90;
            //        CountSpin = 0;
            //    }

            //    try
            //    {
            //        HRESULT hr;
            //        hr = (HRESULT)CameraControl.Set(prop, value, flag);
            //        if (hr < HRESULT.S_OK)
            //            throw new CxDSException(hr);
            //    }
            //    catch (System.Exception)
            //    {
            //        focusSlider.IsEnabled = false;
            //    }
            //}
        }

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
            float scaleX = (float)w / (float)zoomWidth;
            float scaleY = (float)h / (float)zoomHeight;

            pictureView.RenderTransform = new ScaleTransform { CenterX = ZoomStartX, CenterY = ZoomStartY, ScaleX = scaleX, ScaleY = scaleY };
        }

        public void funcionReset()
        {

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
            pictureView.RenderTransform = new RotateTransform { Angle = Angle, CenterX = w / 2, CenterY = h / 2 };
        }

        public void CameraStop()
        {
            imageCapure.Source = pictureView.Source;
            imageCapure.RenderTransform = new RotateTransform { Angle = Angle, CenterX = w / 2, CenterY = h / 2 };
            win.border.BorderBrush = new SolidColorBrush(Colors.Black);
            win.border.BorderThickness = new Thickness(15, 15, 15, 15);
        }

        public void CameraStart()
        {
            imageCapure.Source = null;
            win.border.BorderBrush = null;
        }

        private void zoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void focusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void exposureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        
    }
}