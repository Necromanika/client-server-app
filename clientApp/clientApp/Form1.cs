using System;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace clientApp
{
    public partial class Form1 : Form
    {
        private const int _port = 13031; // порт
        private const string _server = "127.0.0.1"; // IP
        private bool _conn = false; // подключение есть/нет
        private AutoResetEvent _waitHandler = new AutoResetEvent(false); // очередь
        private TcpClient _client; // клиент
        private NetworkStream _stream; // поток
        private byte[] _text; // текст для отправки
        private byte[] _mess;  // сообщения для отправки
        private Thread _dataOut;
        private Thread _dataIn;
        
        public Form1()
        {
            InitializeComponent();
            textBox2.Enabled = false;
            btn_Enter.Enabled = false;
        }

        private async void Btn_connect_Click(object sender, EventArgs e)
        {
            if (!_conn) // если не подключены - подключаемся
            {
                try
                {
                    _client = new TcpClient();
                    await Task.Run(() =>
                    {
                        _client.Connect(_server, _port);
                        textBox1.Invoke((MethodInvoker)(() => textBox1.AppendText("\r\nПодключен к сервверу: " + _server + ":" + _port.ToString()))); // логируем

                        _stream = _client.GetStream(); // получаем поток
                        btn_connect.Invoke((MethodInvoker)(() => btn_connect.Text = "Отключиться"));
                        _conn = true;
                        textBox2.Invoke((MethodInvoker)(() => textBox2.Enabled = true));
                        btn_Enter.Invoke((MethodInvoker)(() => btn_Enter.Enabled = true));

                        _dataOut = new Thread(() => Write_Data());// создаем  потоки
                        _dataOut.Start(); // запускаем поток

                        _dataIn = new Thread(Read_Data);
                        _dataIn.Start(); // запускаем поток

                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else //отключаемся
            {
                byte[] data = new byte[] { Convert.ToByte(2), 0x0f }; // посылаем сигнал о отключении на сервер
                _stream.Write(data, 0, data.Length);
                _stream.Close();
                _client.Close();
                textBox1.AppendText("\r\nОтключен от сервера: " + _server + ":" + _port.ToString()); // логируем
                btn_connect.Text = "Подключиться";
                _conn = false;
                _text = null;
                _waitHandler.Reset();
                textBox2.Enabled = false;
                btn_Enter.Enabled = false;
                _dataIn.Abort();
                _dataOut.Abort();
            }
        }

        private void Btn_Enter_Click(object sender, EventArgs e)
        {
            if (textBox2.Text.Length == 0) // проверка пустой строки
                return;
            _text = Encoding.UTF8.GetBytes(textBox2.Text);
            _mess = new byte[] { Convert.ToByte(_text.Length+2), 0x01}; // сообщение на отправку 1байт - размер, 2байт - управляющий байт
            _mess = _mess.Concat(_text).ToArray();
            _waitHandler.Set();                       
        }

        private void Write_Data()
        {
            while (_conn)
            {
                _waitHandler.WaitOne(); // ставим в очередь
                _stream.Write(_mess, 0, _mess.Length); // отправляем сообщение
                _waitHandler.Set(); // освобождаем место в очереди
            }
        }

        private void Read_Data()
        {
            while (_conn)
            {
                _waitHandler.WaitOne(); // ставим в очередь
                byte[] dataResponse = new byte[256];
                string response = "";
                int bytes = _stream.Read(dataResponse, 0, dataResponse.Length); // считываем полученные данные
                new Thread(() =>
                {
                    response = Encoding.UTF8.GetString(dataResponse, 2, bytes-2);
                    if (bytes != Convert.ToInt32(dataResponse[0]) || bytes < 2) // проверка полученных данных
                        return;
                    if (dataResponse[1] == 0x01)
                    {
                        textBox3.Invoke((MethodInvoker)(() => textBox3.AppendText("\n" + response.ToString()))); // обновляем UI
                        textBox2.Invoke((MethodInvoker)(() => textBox2.Text = ""));
                        textBox2.Invoke((MethodInvoker)(() => textBox2.Focus()));
                    }
                }).Start();
            }

        }
        private void TextBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) // ентер = нажатие кнопки отправить
                Btn_Enter_Click(sender, e);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(_conn) // отключаемся при закрытии формы
                Btn_connect_Click(sender, e);
        }
    }
}
