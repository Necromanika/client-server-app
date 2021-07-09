using System;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace clientApp
{
    public partial class Form1 : Form
    {
        private const int _port = 13031; // порт
        private const string _server = "127.0.0.1"; // IP
        private bool _conn = false; // подключение есть/нет
        private TcpClient _client; // клиент
        private NetworkStream _stream; // поток
        private Queue<byte[]> _messageQ = new Queue<byte[]>(); // очередь
        private SemaphoreSlim _sem = new SemaphoreSlim(1); // семафор
        private bool _writeOn = false;

        public Form1()
        {
            InitializeComponent();
            textBox2.Enabled = false;
            btn_Enter.Enabled = false;
        }

        private void Btn_connect_Click(object sender, EventArgs e)
        {
            Conn_Disconn();
        }

        private async Task Conn_Disconn()
        {
            if (!_conn) // если не подключены - подключаемся
            {
                _client = new TcpClient();
                try
                {
                    await _client.ConnectAsync(_server, _port);
                    textBox1.AppendText(String.Format("\r\nПодключен к сервверу: {0}:{1}", _server, _port)); // логируем

                    _stream = _client.GetStream(); // получаем поток
                    btn_connect.Text = "Отключиться";
                    _conn = true;
                    textBox2.Enabled = true;
                    btn_Enter.Enabled = true;
                    _writeOn = true;

                    Write_Data();
                    await Read_Data();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else //отключаемся
            {
                _writeOn = false;
                byte[] data = new byte[] { 0x0f }; // посылаем сигнал о отключении на сервер
                AddInQ(data);
                Discon();
            }
        }

        private void Discon()
        {
            textBox1.AppendText(String.Format("\r\nОтключен от сервера: {0}:{1}", _server, _port)); // логируем
            btn_connect.Text = "Подключиться";
            _conn = false;
            textBox2.Enabled = false;
            btn_Enter.Enabled = false;
        }

        private  void Btn_Enter_Click(object sender, EventArgs e)
        {
            Create_Message();
        }

        private void AddInQ(byte[] message)
        {
            _messageQ.Enqueue(message); // ставим в очередь сообщений
            _sem.Release(); // сигналим
        }

        private void Create_Message()
        {
            byte[] text; // текст для отправки
            byte[] mess;  // сообщения для отправки
            if (textBox2.Text.Length == 0) // проверка пустой строки
                return;
            text = Encoding.UTF8.GetBytes(textBox2.Text);
            mess = new byte[] { 0x01, Convert.ToByte(text.Length) }.Concat(text).ToArray(); // сообщение на отправку 2байт - размер, 1байт - управляющий байт
            AddInQ(mess);
            textBox2.Text = "";
            textBox2.Focus();
        }

        private async Task Write_Data()
        {
            byte[] mess;
            try
            {
                do
                {
                    await _sem.WaitAsync();
                    if (_messageQ.Count > 0) // для всех в очереди
                    {
                        mess = _messageQ.Dequeue();
                        await _stream.WriteAsync(mess, 0, mess.Length); // пишем в сокет
                    }
                } while (_writeOn);
                _stream.Close(); // закрываем соединение когда больше не пишем
                _client.Close();
            }
            catch
            {
                textBox1.AppendText(String.Format("\r\nСервер {0}:{1} не отвечает. Попытка переподключения.", _server, _port)); // логируем
                Discon(); // отключаемся 
                await Conn_Disconn(); // подключаемся заново
            }
        }

        private async Task Read_Data()
        {
            try
            {
                while (_conn)
                {
                    byte[] dataResponse = new byte[255];
                    int size;
                    string read;

                    await _stream.ReadAsyncAll(dataResponse, 0, 1);// читаем управляющий бит
                    byte control = dataResponse[0];

                    if (control == 0x01) // текстовое сообщение
                    {
                        await _stream.ReadAsyncAll(dataResponse, 0, 1); //читаем размер сообщения
                        size = dataResponse[0];
                        await _stream.ReadAsyncAll(dataResponse, 0, size); // читаем сообщение
                        read = Encoding.UTF8.GetString(dataResponse, 0, size);

                        textBox3.AppendText("\n" + read); // обновляем UI
                    }
                }
            }
            catch
            {
            }
        }
        private void TextBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)// ентер = нажатие кнопки отправить
            { 
                Create_Message();
            }
        }

        private  void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_conn) // отключаемся при закрытии формы
            {
                AddInQ(new byte[] { 0x0f });// посылаем сигнал о отключении на сервер
            }
        }
    }
    static class StreamExtension
    {
        public static async Task<int> ReadAsyncAll(this NetworkStream stream, byte[] buffer, int offset, int size)
        {
            int bytes;
            int bytesRead = 0;
            do
            {
                bytes = await stream.ReadAsync(buffer, offset + bytesRead, size - bytesRead);
                if (bytes == 0)
                    throw new IOException();
                bytesRead += bytes;
            } while (bytesRead < size);
            return bytesRead;
        }
    }
}
