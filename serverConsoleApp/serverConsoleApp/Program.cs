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
        const int port = 13031; // порт для прослушивания подключений
        static object locker = new object();
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
                if(server!=null)
                    server.Stop(); // останавливаем
            }
        }

        private static void connection(TcpClient client, List<TcpClient> clients)
        {
            lock (locker)
            {
                if (clients.Contains(null)) // если есть пустое место в списке, помещаем туда нового клиента
                {
                    clients.Insert(clients.IndexOf(null), client);
                    clients.Remove(null);
                }
                else
                {
                    clients.Add(client);
                }
            }
            Console.WriteLine("Подключен клиент № {0}", (clients.IndexOf(client) + 1).ToString()); // логирунм


            using (NetworkStream stream = client.GetStream())// получаем поток для чтения и записи
            {
                byte[] data = new byte[256];
                string read;
                try
                {
                    while (true)
                    {
                        int bytes = stream.Read(data, 0, data.Length);// читаем данные
                        read = Encoding.UTF8.GetString(data, 2, bytes-2);

                        if (bytes != Convert.ToInt32(data[0]) || bytes<2) // проверка полученных данных
                            continue;
                        if (data[1]==0x0f) // если пришло сообщение об отключении клиента (0x0f) отключаем его
                        {
                            break;
                        }
                        if (data[1] == 0x01)
                        {
                            Console.WriteLine("Клиент №" + (clients.IndexOf(client) + 1).ToString() + ": " + read); // логируем
                            byte[] dataResponse = Encoding.UTF8.GetBytes(read);
                            stream.Write(dataResponse, 0, dataResponse.Length); // отправляем обратно данные
                        }
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.ReadLine();
                    return;
                }
                finally
                {
                    stream.Close();// закрываем поток
                    client.Close();// закрываем подключение
                    Console.WriteLine("Клиент № " + (clients.IndexOf(client) + 1).ToString() + " отключен."); // логируем
                    clients.Insert(clients.IndexOf(client), null); // оставляем пустое место в списке
                    clients.Remove(client);
                }
            }
        }
    }
}
