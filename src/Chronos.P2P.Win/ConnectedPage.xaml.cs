﻿using Chronos.P2P.Client;
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
using System.Windows.Media.Animation;
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
            peer.OnFileTransferDone += Peer_OnFileTransferDone;
            peer.FileReceiveProgressInvoker = p =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (p.Percent > 0.001)
                    {
                        progress.IsIndeterminate = false;
                        progress.Value = p.Percent;
                    }
                    progressText.Text = p.WorkProgress + $" {p.Percent:00.00}%";
                });
            };
            peer.OnInitFileTransfer = info =>
            {
                if (invitor&&info.Length==-1)
                {
                    return Task.FromResult((true, ""));
                }
                Dispatcher.Invoke(() =>
                {
                    if (info.Length==-1)
                    {
                        liveChat.IsEnabled = false;
                    }
                    else
                    {
                        sendFile.IsEnabled = false;
                    }
                });
                var re = MessageBox.Show($"Remote peer request to transfer a stream of len {info.Length}"
                    + $", named {info.Name}, do you want to accept?",
                    $"Peer request", MessageBoxButton.YesNo,MessageBoxImage.Question, MessageBoxResult.Cancel, MessageBoxOptions.DefaultDesktopOnly);
                if (re is MessageBoxResult.Yes)
                {
                    if (info.Length==-1)
                    {
                        peer.StartSendLiveAudio("Live audio chat");
                        return Task.FromResult((true, ""));
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            progressText.Text = "";
                            progress.IsIndeterminate = true;
                            progressText.Visibility = Visibility.Visible;
                            progress.Visibility = Visibility.Visible;
                        });
                        var sd = new SaveFileDialog
                        {
                            FileName = info.Name
                        };
                        var red = sd.ShowDialog();
                        if (red is not null&&red.Value)
                        {
                            return Task.FromResult((true, sd.FileName));
                        }
                        Dispatcher.Invoke(() =>
                        {
                            sendFile.IsEnabled = true;
                            progressText.Visibility = Visibility.Hidden;
                            progress.Visibility = Visibility.Hidden;
                        });
                    }
                }
                return Task.FromResult((false, ""));
            };
            InitializeComponent();
            ChatHandler.OnChatMsgReceived += ChatHandler_OnChatMsgReceived;
            chatBox.KeyDown += ChatBox_KeyDown;
        }

        private void ChatBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                Button_Click_1(null, null);
            }
        }

        private void Peer_OnFileTransferDone(object sender, (double speed, TimeSpan time) e)
        {
            MessageBox.Show($"Transfer complete! Speed: {e.speed}MBps, time: {e.time}", "Transfer Done", MessageBoxButton.OK,
                MessageBoxImage.Information, MessageBoxResult.Cancel, MessageBoxOptions.DefaultDesktopOnly);
            Dispatcher.Invoke(() =>
            {
                sendFile.IsEnabled = true;
                progressText.Visibility = Visibility.Hidden;
                progress.Visibility = Visibility.Hidden;
            });
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
            liveChat.IsEnabled = false;
            invitor = true;
            try
            {
                await peer.StartSendLiveAudio("Live audio chat");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.Cancel, MessageBoxOptions.DefaultDesktopOnly);
                Dispatcher.Invoke(() =>
                {
                    liveChat.IsEnabled = true;
                });
            }
            
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var txt = chatBox.Text;
            chatBox.Text = "";
            chatList.Items.Add($"you: {txt}");
            await peer.SendDataToPeerReliableAsync(1, txt);
        }

        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var d = new OpenFileDialog();
            d.CheckFileExists = true;
            d.InitialDirectory = Environment.CurrentDirectory;
            d.Title = "选择发送的文件";
            d.ShowDialog();
            sendFile.IsEnabled = false;
            progressText.Text = "";
            progress.IsIndeterminate = true;
            progressText.Visibility = Visibility.Visible;
            progress.Visibility = Visibility.Visible;
            try
            {
                await peer.SendFileAsync(d.FileName, 150, p=>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (p.Percent > 0.001)
                        {
                            progress.IsIndeterminate = false;
                            progress.Value = p.Percent;
                        }
                        progressText.Text = p.WorkProgress + $" {p.Percent:00.00}%";
                    });
                });
                MessageBox.Show("Transfer complete!", "Transfer Done", MessageBoxButton.OK,
                    MessageBoxImage.Information, MessageBoxResult.Cancel, MessageBoxOptions.DefaultDesktopOnly);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.Cancel,MessageBoxOptions.DefaultDesktopOnly);
            }
            finally
            {
                sendFile.IsEnabled = true;
                progressText.Visibility = Visibility.Hidden;
                progress.Visibility = Visibility.Hidden;
            }
            
        }

        private void chatBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
