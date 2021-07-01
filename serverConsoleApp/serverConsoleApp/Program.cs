using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace serverConsoleApp
{
    class Program
    {
        const int port = 80; // порт для прослушивания подключений
        static AutoResetEvent waitHandler = new AutoResetEvent(true); // очередь
        static void Main(string[] args)
        {
            List<TcpClient> clients = new List<TcpClient>(); // список клиентов
            TcpListener server = null; // сервер
            try
            {
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");
                server = new TcpListener(localAddr, port); // запуск сервера
                server.Start();

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();// получаем входящее подключение
                    Thread clientConn = new Thread(() => connection(client, clients)); // создаем новый поток
                    clientConn.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                server.Stop(); // останавливаем
            }
        }

        private static void connection(TcpClient client, List<TcpClient> clients)
        {
            if (clients.Contains(null)) // если есть пустое место в списке, помещаем туда нового клиента
            {
                clients.Insert(clients.IndexOf(null), client);
                clients.Remove(null);
            }
            else
                clients.Add(client);
            Console.WriteLine("Подключен клиент № " + (clients.IndexOf(client) + 1).ToString()); // логирунм

            NetworkStream stream = client.GetStream();// получаем поток для чтения и записи

            while (true)
            {
                byte[] data = new byte[256];
                string read = "";
                try
                {
                    int bytes = stream.Read(data, 0, data.Length);// читаем данные
                    waitHandler.WaitOne(); // ставим в очередь
                    read += Encoding.UTF8.GetString(data, 0, bytes);
                }
                catch
                {
                    return;
                }
                if (read == "stop_conn") // если пришло сообщение об отключении клиента отключаем его
                {
                    stream.Close();// закрываем поток
                    client.Close();// закрываем подключение
                    Console.WriteLine("Клиент № " + (clients.IndexOf(client) + 1).ToString() + " отключен."); // логируем
                    clients.Insert(clients.IndexOf(client), null); // оставляем пустое место в списке
                    clients.Remove(client);
                    waitHandler.Set();
                    break;
                }

                Console.WriteLine("Клиент №" + (clients.IndexOf(client) + 1).ToString() + ": " + read); // логируем
                byte[] dataResponse = Encoding.UTF8.GetBytes(read);
                stream.Write(dataResponse, 0, dataResponse.Length); // отправляем обратно данные
                waitHandler.Set(); // освобождаем очередь
            }

        }
    }
}
