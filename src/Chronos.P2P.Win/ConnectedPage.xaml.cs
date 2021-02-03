using Chronos.P2P.Client;
using Chronos.P2P.Client.Audio;
using Microsoft.Win32;
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
                if (invitor&&info.Length==-1)
                {
                    return Task.FromResult((true, ""));
                }
                var re = MessageBox.Show($"Remote peer request to transfer a stream of len {info.Length}"
                    + $", named {info.Name}, do you want to accept?",
                    $"Peer request", MessageBoxButton.YesNo);
                if (re is MessageBoxResult.Yes)
                {
                    if (info.Length==-1)
                    {
                        peer.StartSendLiveAudio("Live audio chat");
                        return Task.FromResult((true, ""));
                    }
                    else
                    {
                        var sd = new SaveFileDialog
                        {
                            FileName = info.Name
                        };
                        var red = sd.ShowDialog();
                        if (red is not null&&red.Value)
                        {
                            return Task.FromResult((true, sd.FileName));
                        }
                    }
                }
                return Task.FromResult((false, ""));
            };
            InitializeComponent();
            ChatHandler.OnChatMsgReceived += ChatHandler_OnChatMsgReceived;
        }

        private void ChatHandler_OnChatMsgReceived(object sender, string e)
        {
            Dispatcher.Invoke(() =>
            {
                chatList.Items.Add($"remote: {e}");
            });
            
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            invitor = true;
            try
            {
                await peer.StartSendLiveAudio("Live audio chat");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var txt = chatBox.Text;
            chatBox.Text = "";
            chatList.Items.Add($"you: {txt}");
            peer.SendDataToPeerReliableAsync(1, txt);
        }

        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var d = new OpenFileDialog();
            d.CheckFileExists = true;
            d.InitialDirectory = Environment.CurrentDirectory;
            d.Title = "选择发送的文件";
            d.ShowDialog();
            try
            {
                await peer.SendFileAsync(d.FileName, 10);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            MessageBox.Show("Transfer complete!");
            
        }
    }
}
