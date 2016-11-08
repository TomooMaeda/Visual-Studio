using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DocumentCameraTool
{
    /// <summary>
    /// BubunZoomWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class BubunZoomWindow : Window
    {

        System.Windows.Shapes.Rectangle bubunZoom = new System.Windows.Shapes.Rectangle();
        public int start_x = 0;
        public int start_y = 0;
        public int end_x = 0;
        public int end_y = 0;
        public int current_x = 0;
        public int current_y = 0;

        public int clickFlag = 0;

        //ディスプレイの高さ
        int h = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
        //ディスプレイの幅
        int w = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;

        public BubunZoomWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(null);
            double pX = p.X;
            double pY = p.Y;

            start_x = (int)p.X;
            start_y = (int)p.Y;
            bubunZoom.Height = 1;
            bubunZoom.Width = 1;
            bubunZoom.Stroke = System.Windows.Media.Brushes.Black;
            bubunZoom.Margin = new Thickness(start_x, start_y, 0, 0);
            bubunZoom.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            bubunZoom.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            grid1.Children.Add(bubunZoom);
            clickFlag = 1;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (clickFlag == 1)
            {
                Point p = e.GetPosition(null);
                current_x = (int)p.X;
                current_y = (int)p.Y;

                if (start_x < current_x && start_y < current_y)
                {
                    bubunZoom.Margin = new Thickness(start_x, start_y, 0, 0);
                }
                else if (start_x < current_x && start_y > current_y)
                {
                    bubunZoom.Margin = new Thickness(start_x, current_y, 0, 0);
                }
                else if (start_x > current_x && start_y < current_y)
                {
                    bubunZoom.Margin = new Thickness(current_x, start_y, 0, 0);
                }
                else if (start_x > current_x && start_y > current_y)
                {
                    bubunZoom.Margin = new Thickness(current_x, current_y, 0, 0);
                }

                bubunZoom.Width = Math.Abs(start_x - current_x);
                bubunZoom.Height = Math.Abs(start_y - current_y);
            }

        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(null);
            double pX = p.X;
            double pY = p.Y;

            end_x = (int)p.X;
            end_y = (int)p.Y;
            clickFlag = 0;

            ((MainWindow)this.Owner).zoomArea(start_x, start_y, end_x, end_y);

            Close();
        }
    }   
}
