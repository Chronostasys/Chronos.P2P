using Chronos.P2P.Client;
using Chronos.P2P.Client.Audio;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Chronos.P2P.Win
{
    /// <summary>
    /// Interaction logic for ConnectedPage.xaml
    /// </summary>
    public partial class ConnectedPage : Page
    {
        Peer peer;
        bool invitor = false;
        public ConnectedPage(Peer _peer)
        {
            peer = _peer;
            peer.OnInitFileTransfer = info =>
            {
                if (invitor)
                {
                    return Task.FromResult((true, ""));
                }
                var re = MessageBox.Show($"Remote peer request to transfer a stream of len {info.Length}"
                    + ", do you want to accept?",
                    $"Peer request: {info.Name}", MessageBoxButton.YesNo);
                if (re is MessageBoxResult.Yes)
                {
                    peer.StartSendLiveAudio("Live audio chat");
                    return Task.FromResult((true, ""));
                }
                return Task.FromResult((false, ""));
            };
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            peer.StartSendLiveAudio("Live audio chat");
            invitor = true;
        }
    }
}
