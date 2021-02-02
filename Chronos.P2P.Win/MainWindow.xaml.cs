using Chronos.P2P.Client;
using Chronos.P2P.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Chronos.P2P.Win
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Peer peer;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                peer?.Dispose();
                var port = int.Parse((FindName("Port") as TextBox).Text);
                peer = new Peer(port, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
                Content = new ConnectPage();
            }
            catch (Exception)
            {
                MessageBox.Show("请输入合法的端口值！");
            }
        }
    }
}
