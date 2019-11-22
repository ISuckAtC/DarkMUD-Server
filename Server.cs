using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

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
        static string[] config = File.ReadAllLines("config.txt");

        public static int MaxPlayers = int.Parse(config[2]);
        public static SessionRef[] Sessions = new SessionRef[1000];
        static void Main(string[] args)
        {
            string ipAdress = config[0];
            int port = int.Parse(config[1]);
	
		    Console.WriteLine("Starting server...");
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