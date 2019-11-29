using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Objects;

namespace Session
{
    class SessionHost
    {
        static int secondsTimeout = 300;


        int id;

        CancellationTokenSource timeout = new CancellationTokenSource();

        Player Player;

        TcpClient client;

        NetworkStream stream;

        public void Yeet()
        {
            stream.Close();
            client.Close();
            Server.Server.Sessions.Remove(Server.Server.Sessions.Find(x => x.id == id));
        }

        public async Task Session(TcpClient c, int _id)
        {
            id = _id;

            Console.WriteLine("[{0}] Client connected", id);

            client = c;

            stream = client.GetStream();

            Console.WriteLine("[{0}] Aquired stream...", id);

            Authenticate(stream);

            while (true)
            {
                Task timing = Task.Run(() => Timeout(secondsTimeout, stream, client, timeout));

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
                        foreach (Server.SessionRef sRef in Server.Server.Sessions)
                        {
                            try { await Methods.Send(sRef.Client.GetStream(), Player.Name + ": " + string.Join(' ', command.Skip(1))); }
                            catch (Exception e) { Console.WriteLine("[{0}] " + e, id); }
                        }
                        break;

                    case "attack":
                        if (command.Length > 1)
                        {
                            string target = string.Join(' ', command.Skip(1));
                            var monster = Server.Server.Monsters.Find(x => x.Position.Same(Player.Position) && x.Dead == 0 && x.Name == target);

                            Server.SessionRef session;

                            if (Server.Server.Sessions.Count > 1) session = Server.Server.Sessions.Find(x => x.Player.Position.Same(Player.Position) && x.Player.Name == target);
                            else session = null;

                            if (monster != null) Player.Target = monster;
                            else if (session != null) 
                            {
                                
                                if (Server.Server.Tiles[Player.Position.x, Player.Position.y].pvp) Player.Target = session.Player;
                                else await Methods.Send(stream, "PvP is not enabled here!\n");
                            }
                            else await Methods.Send(stream, "There are no targets by that name.\n");
                        } else await Methods.Send(stream, "Please provide a target to attack.\n");
                        break;

                    case "stopattack":
                        Player.Target = null;
                        break;

                    case "examine":
                        if (command.Length == 1)
                        {
                            await Methods.Send(stream, "You examine your surroundings.\n" + Player.Examine());
                        }
                        break;

                    case "n":
                    case "north":
                        if (Server.Server.Tiles[Player.Position.x, Player.Position.y].n)
                        {
                            Player.Position = new Coordinate(Player.Position.x, Player.Position.y + 1);
                            await Methods.Send(stream, "You walk north.");
                        } else await Methods.Send(stream, "There is no exit to the north!");
                        break;

                    case "s":
                    case "south":
                        if (Server.Server.Tiles[Player.Position.x, Player.Position.y].s)
                        {
                            Player.Position = new Coordinate(Player.Position.x, Player.Position.y - 1);
                            await Methods.Send(stream, "You walk south.");
                        } else await Methods.Send(stream, "There is no exit to the south!");
                        break;

                    case "e":
                    case "east":
                        if (Server.Server.Tiles[Player.Position.x, Player.Position.y].e)
                        {
                            Player.Position = new Coordinate(Player.Position.x + 1, Player.Position.y); 
                            await Methods.Send(stream, "You walk east.");
                        } else await Methods.Send(stream, "There is no exit to the east!");
                        break;

                    case "w":
                    case "west":
                        if (Server.Server.Tiles[Player.Position.x, Player.Position.y].w)
                        {
                            Player.Position = new Coordinate(Player.Position.x - 1, Player.Position.y);
                            await Methods.Send(stream, "You walk west.");
                        } else await Methods.Send(stream, "There is no exit to the west!");
                        break;


                    case "playerlist":
                        if (Player.admin)
                        {
                            await Methods.Send(stream, Server.Server.Players.Namelist());
                        } else await Methods.Send(stream, "This is an admin only command!");
                        break;

                    case "online":
                        foreach (Server.SessionRef sRef in Server.Server.Sessions) await Methods.Send(stream, sRef.Player.Username + "\n");
                        break;

                    case "serversave":
                        if (Player.admin)
                        {
                            Server.Server.Save();
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

            return;
        }

        async void Authenticate(NetworkStream s)
        {
            await Methods.Send(s, "What is your name: ");
            string username = await Methods.GetResponse(s);
            if (username == string.Empty)
            {
                await Methods.Send(s, "Your username cannot be empty!\n");
                await Methods.GetResponse(s);
                Authenticate(s);
            }
            else
            {
                if (Server.Server.Players.Exists(x => x.Name == username))
                {
                    Player player = Server.Server.Players.Find(x => x.Name == username);
                    await Methods.Send(s, "Password: ");
                    string password = await Methods.GetResponse(s);

                    if (player.Password == password)
                    {
                        Player = player;
                        if (Server.Server.Sessions.Exists(x => x.Player == Player))
                        {
                            await Methods.Send(s, "Another session is active on this player.");
                            await Methods.GetResponse(s);
                            Authenticate(s);
                        } 
                        else
                        {
                            await Methods.Send(s, "You have logged in!\n");
                            Server.Server.Sessions.Find(x => x.id == id).Player = Player;
                        }
                    }
                    else
                    {
                        await Methods.Send(s, "Wrong password!\n");
                        await Methods.GetResponse(s);
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
                        Authenticate(s);
                    }
                    else
                    {
                        Player = new Player(username, password, Server.Server.startPosition, 50);
                        Server.Server.Players.Add(Player);
                        if (Server.Server.Sessions.Exists(x => x.Player == Player))
                        {
                            await Methods.Send(s, "Another session was active on this player, they have been booted off.");
                            Server.Server.Sessions.Find(x => x.Player == Player).id = -1;
                        }
                        Server.Server.Sessions.Find(x => x.id == id).Player = Player;
                        await Methods.Send(s, "You have logged in!\n");
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