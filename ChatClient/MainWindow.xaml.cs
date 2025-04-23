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
        private TcpClient _client;          // Клиент для TCP-соединения
        private NetworkStream _stream;      // Поток для сетевого обмена данными
        private Thread _receiveThread;      // Поток для получения сообщений
        private bool _isConnected;          // Флаг состояния подключения
        private string _username;           // Имя текущего пользователя

        public MainWindow()
        {
            InitializeComponent();  // Инициализация компонентов интерфейса из XAML
        }

        /// <summary>
        /// Обработчик нажатия кнопки подключения
        /// </summary>
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _client = new TcpClient();  // Создаем новый TCP-клиент

                // Получаем параметры подключения из интерфейса
                string ip = ServerIpTextBox.Text;
                int port = int.Parse(ServerPortTextBox.Text);
                _username = UsernameTextBox.Text;

                _client.Connect(ip, port);  // Устанавливаем соединение с сервером
                _stream = _client.GetStream();  // Получаем сетевой поток

                // Отправляем серверу имя пользователя
                byte[] data = Encoding.UTF8.GetBytes(_username);
                _stream.Write(data, 0, data.Length);

                _isConnected = true;  // Устанавливаем флаг подключения

                // Создаем и запускаем поток для получения сообщений
                _receiveThread = new Thread(ReceiveMessage);
                _receiveThread.IsBackground = true;  // Делаем поток фоновым
                _receiveThread.Start();

                // Обновляем состояние элементов интерфейса
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
                SendButton.IsEnabled = true;
                MessageTextBox.Focus();  // Устанавливаем фокус на поле ввода

                // Уведомляем пользователя об успешном подключении
                AddMessageToChat($"Вы подключены к {ip}:{port} как {_username}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки отключения
        /// </summary>
        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();  // Вызываем метод отключения
        }

        /// <summary>
        /// Метод для отключения от сервера
        /// </summary>
        private void Disconnect()
        {
            if (_isConnected)  // Проверяем, что подключение активно
            {
                _isConnected = false;  // Сбрасываем флаг подключения

                // Закрываем сетевой поток и соединение
                _stream?.Close();
                _client?.Close();

                // Обновляем состояние элементов интерфейса
                ConnectButton.IsEnabled = true;
                DisconnectButton.IsEnabled = false;
                SendButton.IsEnabled = false;

                // Уведомляем пользователя об отключении
                AddMessageToChat("Отключено от сервера");
            }
        }

        /// <summary>
        /// Метод для получения сообщений от сервера (работает в отдельном потоке)
        /// </summary>
        private void ReceiveMessage()
        {
            while (_isConnected)  // Работаем пока есть подключение
            {
                try
                {
                    byte[] data = new byte[256];  // Буфер для данных
                    StringBuilder builder = new StringBuilder();
                    int bytes;

                    // Читаем данные из потока пока они доступны
                    do
                    {
                        bytes = _stream.Read(data, 0, data.Length);
                        builder.Append(Encoding.UTF8.GetString(data, 0, bytes));
                    } while (_stream.DataAvailable);

                    string message = builder.ToString();  // Получаем сообщение

                    // Обрабатываем специальные сообщения (список пользователей)
                    if (message.StartsWith("USERLIST:"))
                    {
                        Dispatcher.Invoke(() =>  // Используем Dispatcher для работы с UI
                        {
                            UsersListBox.Items.Clear();  // Очищаем список пользователей
                            string[] users = message.Substring(9).Split(',');
                            foreach (var user in users)
                            {
                                if (!string.IsNullOrEmpty(user))
                                    UsersListBox.Items.Add(user);  // Добавляем пользователей
                            }
                        });
                    }
                    else  // Обычные сообщения чата
                    {
                        Dispatcher.Invoke(() => AddMessageToChat(message));
                    }
                }
                catch  // При ошибке отключаемся
                {
                    Dispatcher.Invoke(Disconnect);
                }
            }
        }

        /// <summary>
        /// Добавляет сообщение в чат
        /// </summary>
        /// <param name="message">Текст сообщения</param>
        private void AddMessageToChat(string message)
        {
            ChatTextBlock.Text += message + "\n";  // Добавляем сообщение

            // Автоматическая прокрутка вниз
            var scrollViewer = GetChildOfType<ScrollViewer>(ChatTextBlock.Parent);
            scrollViewer?.ScrollToEnd();
        }

        /// <summary>
        /// Рекурсивно ищет дочерний элемент указанного типа
        /// </summary>
        private static T GetChildOfType<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;

            // Перебираем все дочерние элементы
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = (child as T) ?? GetChildOfType<T>(child);  // Рекурсивный поиск
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Обработчик нажатия кнопки отправки сообщения
        /// </summary>
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();  // Вызываем метод отправки сообщения
        }

        /// <summary>
        /// Обработчик нажатия клавиши в поле ввода сообщения
        /// </summary>
        private void MessageTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)  // Если нажат Enter
            {
                SendMessage();  // Отправляем сообщение
            }
        }

        /// <summary>
        /// Метод для отправки сообщения на сервер
        /// </summary>
        private void SendMessage()
        {
            if (!_isConnected || string.IsNullOrWhiteSpace(MessageTextBox.Text))
                return;  // Проверяем подключение и наличие текста

            try
            {
                string message = MessageTextBox.Text;
                byte[] data = Encoding.UTF8.GetBytes(message);  // Кодируем сообщение
                _stream.Write(data, 0, data.Length);  // Отправляем на сервер

                // Показываем своё сообщение сразу (не дожидаясь ответа сервера)
                AddMessageToChat($"{_username}: {message}");
                MessageTextBox.Clear();  // Очищаем поле ввода
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки сообщения: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик закрытия окна
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            Disconnect();  // Отключаемся от сервера
            base.OnClosed(e);
        }
    }
}