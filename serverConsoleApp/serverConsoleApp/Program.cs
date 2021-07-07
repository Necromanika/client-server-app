using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;

namespace serverConsoleApp
{
    class Program
    {
        const int port = 13031; // порт для прослушивания подключений
        static object locker = new object();
        static TimerCallback tcb = new TimerCallback(currentTime);
        static Timer timer;
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

        private static void currentTime(object obj)
        {
            NetworkStream stream = (NetworkStream)obj;
            byte[] mess;
            byte[] time = Encoding.UTF8.GetBytes(DateTime.Now.TimeOfDay.ToString("hh\\:mm\\:ss"));
            mess = new byte[] { 0xff, 0x01 }; 
            mess = mess.Concat(time).ToArray();
            mess = mess.Concat(new byte[255 - mess.Length]).ToArray();
            stream.Write(mess, 0, mess.Length);
        }
        private static async void connection(TcpClient client, List<TcpClient> clients)
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
                await Task.Run(() =>
                {
                    timer = new Timer(tcb, stream, 0, 10000);
                });
                byte[] data = new byte[255];
                string read;
                byte[] mess;
                try
                {
                    while (true)
                    {
                        int bytes = stream.Read(data, 0, data.Length);// читаем данные
                        if (bytes != Convert.ToInt32(data[0]) || bytes < 255) // проверка полученных данных
                            continue;
                        read = Encoding.UTF8.GetString(data, 2, bytes-2).TrimEnd('\0');
                        
                        if (data[1]==0x0f) // если пришло сообщение об отключении клиента (0x0f) отключаем его
                        {
                            break;
                        }
                        if (data[1] == 0x01)
                        {
                            Console.WriteLine("Клиент №" + (clients.IndexOf(client) + 1).ToString() + ": " + read); // логируем
                            byte[] dataResponse = Encoding.UTF8.GetBytes(read);
                            mess = new byte[] { 0xff, 0x01 }; // сообщение на отправку 1байт - размер, 2байт - управляющий байт
                            mess = mess.Concat(dataResponse).ToArray();
                            mess = mess.Concat(new byte[255 - mess.Length]).ToArray();
                            stream.Write(mess, 0, mess.Length); // отправляем обратно данные
                        }
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    timer.Dispose();
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
