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



namespace MainServer
{
    class SessionRef
    {
        public Player Player;
        public TcpClient Client;
        public Task iTask;

        public int id;
        public SessionRef(TcpClient client, Task task, int id)
        {
            this.Client = client;
            this.iTask = task;
            this.id = id;
        }
    }
    class Server
    {
        public static string[] config = File.ReadAllLines("config.txt");
        public static List<SessionRef> Sessions = new List<SessionRef>();

        public static int autoSaveInterval = int.Parse(config[3]);

        public static List<Player> Players = new List<Player>();

        public static List<Monster> Monsters = new List<Monster>();

        public static Tile[,] Tiles;

        public static Coordinate startPosition = new Coordinate(2, 2);

        public static List<Drop[]> DropsTables = new List<Drop[]>();

        public static int TickLength = int.Parse(config[5]);

        public static async Task Tick()
        {
            while (true)
            {
                foreach (Monster monster in Monsters) monster.Behavior();
                foreach (Player player in Players) player.Behavior();
                await Task.Delay(TickLength);
            }
        }

        public static async Task AutoSave()
        {
            while (true)
            {
                await Task.Delay(autoSaveInterval);
                Save();
                Console.WriteLine("Autosave complete!");
            }
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
            else 
            {
                Tiles = new Tile[int.Parse(config[4].Substring(0, config[4].IndexOf('x'))), int.Parse(config[4].Substring(config[4].IndexOf('x') + 1))].InitiateArray2D(() => new Tile());
                Tiles[2,2].n = true;
                Tiles[2,3].s = true;
            }
            if (File.ReadAllText("monsters.json").Length != 0) 
            {
                List<MonsterReference> references = (List<MonsterReference>)JArray.Parse(File.ReadAllText("monsters.json")).ToObject(typeof(List<MonsterReference>));
                Monsters = references.FromBase();
            }
            if (File.ReadAllText("drops.json").Length != 0)
            {
                DropsTables = (List<Drop[]>)JArray.Parse(File.ReadAllText("drops.json")).ToObject(typeof(List<Drop[]>));
            }

            Tiles[1,2] = new Tile(description:"Uneven rocky terrain", e:true);
            Tiles[2,2].w = true;

            Console.WriteLine("Loaded {0} monsters", Monsters.Count);
            Console.WriteLine("Loaded {0} players", Players.Count);

            string ipAdress = config[0];
            int port = int.Parse(config[1]);

            Drop[] drops = new Drop[3];
            drops[0] = new Drop(new Item("Goblin arm", "Back off, rich boy, I'm armed!", WeaponClass.Club), 100);
            drops[1] = new Drop(new Item("Nothing", ""), 1000);
            drops[2] = new Drop(new Item("Admin Login", "Username: Ross, Password: fucky"), 1);
            DropsTables.Add(new Drop[] {new Drop(new Item("Nothing", ""), 1)});
            DropsTables.Add(drops);

            /*Monsters.Add(new Monsters.Goblin("Steve the Goblin", "claws", 15, 30, new Coordinate(2, 3), DropId: 0));

            File.WriteAllText("monsters.json", JArray.FromObject(Monsters.ToBase()).ToString());*/

            Console.WriteLine("Starting server...");
            TcpListener server = new TcpListener(IPAddress.Parse(ipAdress), port);

            server.Start();
            Console.WriteLine("Server started on {0}:{1} \n", ipAdress, port);

            Task autoSave = Task.Run(() => AutoSave());
            Task tick = Task.Run(() => Tick());

            while (true)
            {
                Console.WriteLine("Waiting for client to connect...");
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Client connected! Setting up session...");

                int id = 0;

                for(int x = 0; x < Sessions.Count; ++x)
                {
                    if (Sessions[x].id == id) id++;
                }

                Sessions.Insert(id, new SessionRef(client, Task.Run(() => new Session.SessionHost().Session(client, id)), id));
            }
        }
    }
}