using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ChatClient
{
    public partial class MainWindow : Window
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private Thread _receiveThread;
        private bool _isConnected;
        private string _username;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _client = new TcpClient();

                string ip = ServerIpTextBox.Text;
                int port = int.Parse(ServerPortTextBox.Text);
                _username = UsernameTextBox.Text;

                _client.Connect(ip, port);
                _stream = _client.GetStream();

                byte[] data = Encoding.UTF8.GetBytes(_username);
                _stream.Write(data, 0, data.Length);

                _isConnected = true;

                _receiveThread = new Thread(ReceiveMessage);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();

                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
                SendButton.IsEnabled = true;
                MessageTextBox.Focus();

                AddMessageToChat($"Вы подключены к {ip}:{port} как {_username}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            if (_isConnected)
            {
                _isConnected = false;
                _stream?.Close();
                _client?.Close();

                ConnectButton.IsEnabled = true;
                DisconnectButton.IsEnabled = false;
                SendButton.IsEnabled = false;

                AddMessageToChat("Отключено от сервера");
            }
        }

        private void ReceiveMessage()
        {
            while (_isConnected)
            {
                try
                {
                    byte[] data = new byte[256];
                    StringBuilder builder = new StringBuilder();
                    int bytes;

                    do
                    {
                        bytes = _stream.Read(data, 0, data.Length);
                        builder.Append(Encoding.UTF8.GetString(data, 0, bytes));
                    } while (_stream.DataAvailable);

                    string message = builder.ToString();

                    if (message.StartsWith("USERLIST:"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            UsersListBox.Items.Clear();
                            string[] users = message.Substring(9).Split(',');
                            foreach (var user in users)
                            {
                                if (!string.IsNullOrEmpty(user))
                                    UsersListBox.Items.Add(user);
                            }
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() => AddMessageToChat(message));
                    }
                }
                catch
                {
                    Dispatcher.Invoke(Disconnect);
                }
            }
        }

        private void AddMessageToChat(string message)
        {
            ChatTextBlock.Text += message + "\n";
            // Автоматическая прокрутка вниз
            var scrollViewer = GetChildOfType<ScrollViewer>(ChatTextBlock.Parent);
            scrollViewer?.ScrollToEnd();
        }

        private static T GetChildOfType<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = (child as T) ?? GetChildOfType<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void MessageTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                SendMessage();
            }
        }

        private void SendMessage()
        {
            if (!_isConnected || string.IsNullOrWhiteSpace(MessageTextBox.Text))
                return;

            try
            {
                string message = MessageTextBox.Text;
                byte[] data = Encoding.UTF8.GetBytes(message);
                _stream.Write(data, 0, data.Length);

                // Показываем своё сообщение сразу
                AddMessageToChat($"{_username}: {message}");
                MessageTextBox.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки сообщения: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Disconnect();
            base.OnClosed(e);
        }
    }
}