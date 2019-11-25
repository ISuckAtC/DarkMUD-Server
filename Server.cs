using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Objects;
using Server;



namespace Server
{
    class SessionRef
    {
        public Player player;
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
        public static SessionRef[] Sessions = new SessionRef[MaxPlayers];

        public static int autoSaveInterval = int.Parse(config[3]);

        public static List<Player> Players = new List<Player>();

        public static Tile[,] Tiles = new Tile[int.Parse(config[4].Substring(0, config[4].IndexOf('x'))), int.Parse(config[4].Substring(config[4].IndexOf('x') + 1))];

        public static Coordinate startPosition = new Coordinate(2, 2);

        public async Task AutoSave()
        {
            await Task.Delay(autoSaveInterval);
            Save();
            Console.WriteLine("Autosave complete!");
        }

        public static void Save()
        {
            File.WriteAllText("players.json", JArray.FromObject(Players).ToString());
            File.WriteAllText("tiles.json", JArray.FromObject(Tiles).ToString());
        }



        static void Main(string[] args)
        {
            if (File.ReadAllText("players.json").Length != 0) Players = JArray.Parse(File.ReadAllText("players.json")).ToObject(typeof(List<Player>)) as List<Player>;
            if (File.ReadAllText("tiles.json").Length != 0) Tiles = JArray.Parse(File.ReadAllText("tiles.json")).ToObject(typeof(Tile[,])) as Tile[,];
            else Tiles = new Tile[5, 5].InitiateCollection(() => new Tile());

            Console.WriteLine("Loaded {0} players", Players.Count);

            string ipAdress = config[0];
            int port = int.Parse(config[1]);

            Console.WriteLine("Starting server...");
            TcpListener server = new TcpListener(IPAddress.Parse(ipAdress), port);

            server.Start();
            Console.WriteLine("Server started on {0}:{1} \n", ipAdress, port);

            Task autoSave = Task.Run(() => new global::Server.Server().AutoSave());

            while (true)
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