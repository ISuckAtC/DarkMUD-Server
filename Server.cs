using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DarkMUD_Server
{
    class SessionRef
    {
        public TcpClient client;
        public Task task;

        public int id;
        public SessionRef(TcpClient client, Task task, int id)
        {
            this.client = client;
            this.task = task;
            this.id = id;
        }
    }
    class Server
    {
        public static int MaxPlayers = 1000;
        public static SessionRef[] Sessions = new SessionRef[1000];
        static void Main(string[] args)
        {
            string ipAdress = "192.168.1.146";
            int port = 52587;

            TcpListener server = new TcpListener(IPAddress.Parse(ipAdress), port);

            server.Start();
            Console.WriteLine("Server started on {0}:{1} \n", ipAdress, port);

            while(true)
            {
                Console.WriteLine("Waiting for client to connect...");
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Client connected! Setting up session...");


                int firstSpot = MaxPlayers - Sessions.SkipWhile(x => x != null).Count();

                Sessions[firstSpot] = new SessionRef(client, Task.Run(() => new Session.SessionHost().Session(client, firstSpot)), firstSpot);
            }
        }
    }
}