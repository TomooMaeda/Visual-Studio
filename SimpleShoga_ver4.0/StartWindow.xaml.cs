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
using System.Windows.Threading;

namespace DocumentCameraTool
{
    /// <summary>
    /// StartWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class StartWindow : Window
    {
        public StartWindow()
        {
            InitializeComponent();
        }

        private DispatcherTimer Timer;
        int count = 0;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Timer = new DispatcherTimer();
            Timer.Interval = new TimeSpan(0, 0, 0, 1);
            Timer.Tick += new EventHandler(Timer_Tick);
            Timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (count == 2)
            {
                var win = new MainWindow();
                win.Show();

                Close();
            }
            count++;
        }
    }
}
