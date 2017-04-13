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
    /// SubFunctionWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SubFunctionWindow : Window
    {
        public SubFunctionWindow()
        {
            InitializeComponent();
        }

        private void BubunZoomButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new BubunZoomWindow();
            win.Owner = (MainWindow)this.Owner;
            win.Show();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)this.Owner).CameraStop();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)this.Owner).funcionReset();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)this.Owner).WindowClose();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)this.Owner).CameraStart();
        }

        private void RecameraButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)this.Owner).CameraReset();
        }
    }
}
