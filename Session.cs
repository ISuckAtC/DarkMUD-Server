using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Objects;
using MainServer;

namespace Session
{
    class SessionHost
    {
        static int startTimeout = 30;
        static int secondsTimeout = 300;


        int id;

        Player Player;

        TcpClient client;

        NetworkStream stream;

        public void Yeet()
        {
            stream.Close();
            client.Close();
            Console.WriteLine("{0} yeet", id);
            Server.Sessions.Remove(Server.Sessions.Find(x => x.id == id));
        }

        Task timing;

        public async Task Session(TcpClient c, int _id)
        {
            try
            {
            id = _id;

            Console.WriteLine("[{0}] Client connected", id);

            client = c;

            stream = client.GetStream();

            Console.WriteLine("[{0}] Aquired stream...", id);

            Authenticate(stream);

            while (true)
            {
                CancellationTokenSource timeout = new CancellationTokenSource();

                timing = Task.Run(() => Timeout(secondsTimeout, stream, client, timeout));

                string message = await Methods.GetResponse(stream);

                Console.WriteLine("[{0}] CLIENT: " + message, id);

                timeout.Cancel();

                if (message.ToLower() == "q") break;

                string[] command = message.Split(' ');

                switch (command[0].ToLower())
                {
                    case "ping":
                        await Methods.Send(stream, "Pong!");
                        break;

                    case "shout":
                        foreach (SessionRef sRef in Server.Sessions)
                        {
                            try { await Methods.Send(sRef.Client.GetStream(), Player.Name + ": " + string.Join(' ', command.Skip(1))); }
                            catch (Exception e) { Console.WriteLine("[{0}] " + e, id); }
                        }
                        break;

                    case "attack":
                        if (command.Length > 1)
                        {
                            string target = string.Join(' ', command.Skip(1));
                            var monster = Server.Monsters.Find(x => x.Position.Same(Player.Position) && x.Dead == 0 && x.Name == target);

                            SessionRef session;

                            if (Server.Sessions.Count > 1) session = Server.Sessions.Find(x => x.Player.Position.Same(Player.Position) && x.Player.Name == target);
                            else session = null;

                            if (monster != null) Player.Target = monster;
                            else if (session != null) 
                            {
                                
                                if (Server.Tiles[Player.Position.x, Player.Position.y].pvp) Player.Target = session.Player;
                                else await Methods.Send(stream, "PvP is not enabled here!\n");
                            }
                            else await Methods.Send(stream, "There are no targets by that name.\n");
                        } else await Methods.Send(stream, "Please provide a target to attack.\n");
                        break;

                    case "stopattack":
                        Player.Target = null;
                        break;

                    case "examine":
                        if (command.Length == 1) await Methods.Send(stream, "You examine your surroundings.\n" + Player.Examine());
                        if (command.Length > 1) await Methods.Send(stream, Player.ExamineObject(string.Join(' ', command.Skip(1))));
                        break;

                    case "take":
                        if (command.Length > 1)
                        {
                            string itemName = string.Join(' ', command.Skip(1));
                            if (Server.Tiles[Player.Position.x, Player.Position.y].Items.Exists(x => x.Name == itemName))
                            {
                                Item item = Server.Tiles[Player.Position.x, Player.Position.y].Items.Find(x => x.Name == itemName);
                                Player.Inventory.Add(item);
                                Server.Tiles[Player.Position.x, Player.Position.y].Items.Remove(item);
                                await Methods.Send(stream, "Added " + item.Name + " to your inventory.\n");
                            } else await Methods.Send(stream, "There is no \"" + itemName + "\" to take.");
                        } else await Methods.Send(stream, "Please specify what item you're trying to take.\n");
                        break;

                    case "inv":
                    case "inventory":
                        string inventory = "\n - Inventory - \n\n";
                        foreach(Item item in Player.Inventory) inventory += "- " + item.Name + "\n";
                        await Methods.Send(stream, inventory);
                        break;

                    case "n":
                    case "north":
                        if (Server.Tiles[Player.Position.x, Player.Position.y].n)
                        {
                            Player.Position = new Coordinate(Player.Position.x, Player.Position.y + 1);
                            await Methods.Send(stream, "You walk north.");
                        } else await Methods.Send(stream, "There is no exit to the north!");
                        break;

                    case "s":
                    case "south":
                        if (Server.Tiles[Player.Position.x, Player.Position.y].s)
                        {
                            Player.Position = new Coordinate(Player.Position.x, Player.Position.y - 1);
                            await Methods.Send(stream, "You walk south.");
                        } else await Methods.Send(stream, "There is no exit to the south!");
                        break;

                    case "e":
                    case "east":
                        if (Server.Tiles[Player.Position.x, Player.Position.y].e)
                        {
                            Player.Position = new Coordinate(Player.Position.x + 1, Player.Position.y); 
                            await Methods.Send(stream, "You walk east.");
                        } else await Methods.Send(stream, "There is no exit to the east!");
                        break;

                    case "w":
                    case "west":
                        if (Server.Tiles[Player.Position.x, Player.Position.y].w)
                        {
                            Player.Position = new Coordinate(Player.Position.x - 1, Player.Position.y);
                            await Methods.Send(stream, "You walk west.");
                        } else await Methods.Send(stream, "There is no exit to the west!");
                        break;


                    case "playerlist":
                        if (Player.admin)
                        {
                            await Methods.Send(stream, Server.Players.Namelist());
                        } else await Methods.Send(stream, "This is an admin only command!");
                        break;

                    case "online":
                        foreach (SessionRef sRef in Server.Sessions) await Methods.Send(stream, sRef.Player.Username + "\n");
                        break;

                    case "serversave":
                        if (Player.admin)
                        {
                            Server.Save();
                            await Methods.Send(stream, "Saved!");
                        } else await Methods.Send(stream, "This is an admin only command!");
                        break;

                    default:
                        await Methods.Send(stream, "Unknown Command.");
                        break;
                }
            }

            Console.WriteLine("[{0}] Closing session...", id);

            Yeet();
            }
            catch (Exception e) { Console.WriteLine("[{0}]: {1}", id, e); }

            return;
        }

        async void Authenticate(NetworkStream s)
        {
            CancellationTokenSource authTimeout = new CancellationTokenSource();

            timing = Task.Run(() => Timeout(secondsTimeout, stream, client, authTimeout));

            await Methods.Send(s, "What is your name: ");
            string username = await Methods.GetResponse(s);
            if (username == string.Empty)
            {
                await Methods.Send(s, "Your username cannot be empty!\n");
                await Methods.GetResponse(s);
                authTimeout.Cancel();
                Authenticate(s);
            }
            else
            {
                if (Server.Players.Exists(x => x.Name == username))
                {
                    Player player = Server.Players.Find(x => x.Name == username);
                    await Methods.Send(s, "Password: ");
                    string password = await Methods.GetResponse(s);

                    if (player.Password == password)
                    {
                        Player = player;
                        if (Server.Sessions.Exists(x => x.Player == Player))
                        {
                            await Methods.Send(s, "Another session is active on this player.");
                            await Methods.GetResponse(s);
                            authTimeout.Cancel();
                            Authenticate(s);
                        } 
                        else
                        {
                            await Methods.Send(s, "You have logged in!\n");
                            authTimeout.Cancel();
                            Server.Sessions.Find(x => x.id == id).Player = Player;
                        }
                    }
                    else
                    {
                        await Methods.Send(s, "Wrong password!\n");
                        await Methods.GetResponse(s);
                        authTimeout.Cancel();
                        Authenticate(s);
                    }
                }
                else
                {
                    await Methods.Send(s, "You're new around these parts, please pick a password to create a new account, or leave it blank to return to the username selection\nPassword: ");
                    string password = await Methods.GetResponse(s);
                    if (password == string.Empty)
                    {
                        await Methods.Send(s, string.Empty);
                        await Methods.GetResponse(s);
                        authTimeout.Cancel();
                        Authenticate(s);
                    }
                    else
                    {
                        Player = new Player(username, password, Server.startPosition, 50);
                        Server.Players.Add(Player);
                        if (Server.Sessions.Exists(x => x.Player == Player))
                        {
                            await Methods.Send(s, "Another session was active on this player, they have been booted off.");
                            Server.Sessions.Find(x => x.Player == Player).id = -1;
                        }
                        Server.Sessions.Find(x => x.id == id).Player = Player;
                        await Methods.Send(s, "You have logged in!\n");
                        authTimeout.Cancel();
                    }
                }
            }
            return;
        }

        async Task Timeout(int to, NetworkStream s, TcpClient c, CancellationTokenSource cancel)
        {
            for (int x = 0; x < to; ++x)
            {
                await Task.Delay(1000);
                if (cancel.IsCancellationRequested) throw new TaskCanceledException();
            }
            Console.WriteLine("[{0}] Session timed out!", id);
            try { await Methods.Send(s, "Your session has timed out!"); }
            catch (Exception e) { Console.WriteLine("[{0}] {1}", id, e); }


            Console.WriteLine("[{0}] Closing session...", id);

            Yeet();
        }
    }
}