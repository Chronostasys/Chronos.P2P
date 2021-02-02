using Chronos.P2P.Client;
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
    /// Interaction logic for ConnectPage.xaml
    /// </summary>
    public partial class ConnectPage : Page
    {
        Peer peer;
        MainWindow window;
        public ConnectPage(Peer _peer, MainWindow _window)
        {
            InitializeComponent();
            window = _window;
            peer = _peer;
            peerList.MouseDoubleClick += PeerList_MouseDoubleClick;
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);
                    window.Dispatcher.Invoke(() =>
                    {
                        peerList.ItemsSource = peer.Peers?.Values;
                    });
                }
            });
            peer.PeerConnectionLost += Peer_PeerConnectionLost;
            peer.PeerConnected += Peer_PeerConnected;
            peer.OnPeerInvited = info =>
            {
                bool confirm = false;
                var re = MessageBox.Show($"Peer with id {info.Id} invite you to connect, "
                    + "would you like to accept the invitation?",
                    "Connection confirm", MessageBoxButton.YesNo);
                if (re is MessageBoxResult.Yes)
                {
                    confirm = true;
                }
                return confirm;
            };
            peer.StartPeer();
        }

        private void PeerList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(peerList, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                peer.SetPeer((item.DataContext as PeerInfo).Id);
                // ListBox item clicked - do some cool things here
            }
        }

        private void Peer_PeerConnected(object sender, EventArgs e)
        {
            MessageBox.Show("Connection established!");
            window.Dispatcher.Invoke(() =>
            {
                window.Content = new ConnectedPage(peer);
            });
        }

        private void Peer_PeerConnectionLost(object sender, EventArgs e)
        {
            MessageBox.Show("Connecion lost!", "ERROR",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void peerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
        }
    }
}
