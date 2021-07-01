using System;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Threading;

namespace clientApp
{
    public partial class Form1 : Form
    {
        private const int port = 80; // порт
        private const string server = "127.0.0.1"; // IP
        private bool conn = false; // подключение есть/нет
        private AutoResetEvent waitHandler = new AutoResetEvent(true); // очередь
        private TcpClient client; // клиент
        private NetworkStream stream; // поток
        public Form1()
        {
            InitializeComponent();
            textBox2.Enabled = false;
            btn_Enter.Enabled = false;
        }

        private void Btn_connect_Click(object sender, EventArgs e)
        {
            if (!conn) // если не подключены - подключаемся
            {
                try
                {
                    client = new TcpClient();
                    client.Connect(server, port);
                    textBox1.AppendText("\r\nПодключен к сервверу: " + server + ":" + port.ToString()); // логируем

                    stream = client.GetStream(); // получаем поток
                    btn_connect.Text = "Отключиться";
                    conn = true;
                    textBox2.Enabled = true;
                    btn_Enter.Enabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else //отключаемся
            {
                byte[] data = Encoding.UTF8.GetBytes("stop_conn"); // посылаем сигнал о отключении на сервер
                stream.Write(data, 0, data.Length);
                stream.Close();
                client.Close();
                textBox1.AppendText("\r\nОтключен от сервера: " + server + ":" + port.ToString()); // логируем
                btn_connect.Text = "Подключиться";
                conn = false;
                textBox2.Enabled = false;
                btn_Enter.Enabled = false;
            }
        }

        private void Btn_Enter_Click(object sender, EventArgs e)
        {
            if (textBox2.Text.Length == 0) // проверка пустой строки
                return;
            var text = textBox2.Text;
            Thread dataOut = new Thread(()=> Write_Data(text));// создаем новые потоки
            dataOut.Start(); // запускаем поток
            
            Thread dataIn = new Thread(Read_Data);
            dataIn.Start(); // запускаем поток
                       
        }

        private void Write_Data(string text)
        {
            waitHandler.WaitOne(); // ставим в очередь
            byte[] data = Encoding.UTF8.GetBytes(text); // преобразуем сообщение в массив байт
            stream.Write(data, 0, data.Length); // отправляем сообщение
            waitHandler.Set(); // освобождаем место в очереди
        }

        private void Read_Data()
        {
            waitHandler.WaitOne(); // ставим в очередь
            byte[] dataResponse = new byte[256];
            string response = "";
            int bytes = stream.Read(dataResponse, 0, dataResponse.Length); // считываем полученные данные
            response += Encoding.UTF8.GetString(dataResponse, 0, bytes);
            textBox3.Invoke((MethodInvoker)(()=> textBox3.AppendText("\n" + response.ToString()))); // обновляем UI
            textBox2.Invoke((MethodInvoker)(() => textBox2.Text = ""));
            textBox2.Invoke((MethodInvoker)(() => textBox2.Focus()));
            waitHandler.Set(); // освобождаем место в очереди

        }
        private void TextBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) // ентер = нажатие кнопки отправить
                Btn_Enter_Click(sender, e);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(conn) // отключаемся при закрытии формы
                Btn_connect_Click(sender, e);
        }
    }
}
