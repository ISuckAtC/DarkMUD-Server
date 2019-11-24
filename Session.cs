using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Session
{
    class SessionHost
    {
        static int bufferSize = 8192;

        static int secondsTimeout = 300;

        int id;

        Player.PlayerClass Player;

        public async Task Session(TcpClient c, int _id)
        {
            id = _id;

            Console.WriteLine("[{0}] Client connected", id);

            TcpClient client = c;

            NetworkStream stream = client.GetStream();

            Console.WriteLine("[{0}] Aquired stream...", id);

            Authenticate(stream);

            while (true)
            {
                CancellationTokenSource timeout = new CancellationTokenSource();
                Task timing = Task.Run(() => Timeout(secondsTimeout, stream, client, timeout));
                
                string message = await GetResponse(stream);
                Console.WriteLine("[{0}] CLIENT: " + message, id);

                timeout.Cancel();

                if (message.ToLower() == "q") break;

                string[] command = message.Split(' ');

                switch (command[0].ToLower())
                {
                    case "ping":
                        await Send(stream, "Pong!");
                        break;

                    case "shout":
                        foreach (DarkMUD_Server.SessionRef sRef in DarkMUD_Server.Server.Sessions.Where(x => x != null))
                        {
                            try {await Send(sRef.client.GetStream(), Player.username + ": " + string.Join(' ', command.Skip(1)));}
                            catch (Exception e) { Console.WriteLine("[{0}] " + e, id);}
                        }
                        break;

                    default:
                        await Send(stream, "Unknown Command.");
                        break;
                }
            }

            Console.WriteLine("[{0}] Closing session...", id);

            stream.Close();
            client.Close();

            DarkMUD_Server.Server.Sessions[id] = null;

            return;
        }

        async void Authenticate(NetworkStream s)
        {
            await Send(s, "What is your name: ");
            string username = await GetResponse(s);
            if (username == string.Empty)
            {
                await Send(s, "Your username cannot be empty!\n");
                await GetResponse(s);
                Authenticate(s);
            }
            else
            {
                if (DarkMUD_Server.Server.Players.Exists(x => x.username == username))
                {
                    Player.PlayerClass player = DarkMUD_Server.Server.Players.Find(x => x.username == username);
                    await Send(s, "Password: ");
                    string password = await GetResponse(s);

                    if (player.password == password)
                    {
                        await Send(s, "You have logged in!\n");
                        Player = player;
                        DarkMUD_Server.Server.Sessions[id].player = Player;
                    }
                    else
                    {
                        await Send(s, "Wrong password!\n");
                        Authenticate(s);
                    }
                }
                else
                {
                    await Send(s, "You're new around these parts, please pick a password to create a new account, or leave it blank to return to the username selection\nPassword: ");
                    string password = await GetResponse(s);
                    if (password == string.Empty)
                    {
                        await Send(s, string.Empty);
                        await GetResponse(s);
                        Authenticate(s);
                    }
                    else
                    {
                        Player = new Player.PlayerClass(username, password);
                        DarkMUD_Server.Server.Players.Add(Player);
                        await Send(s, "You have logged in!\n");
                    }
                }
            }
            return;
        }

        async Task Timeout(int to, NetworkStream s, TcpClient c, CancellationTokenSource cancel)
        {
            for(int x = 0; x < to; ++x) 
            {
                await Task.Delay(1000);
                if (cancel.IsCancellationRequested) throw new TaskCanceledException();
            }
            Console.WriteLine("[{0}] Session timed out!", id);
            try{await Send(s, "Your session has timed out!");}
            catch(Exception e) {Console.WriteLine("[{0}] {1}", id, e);}
            

            Console.WriteLine("[{0}] Closing session...", id);

            s.Close();
            c.Close();
            DarkMUD_Server.Server.Sessions[id] = null;
        }

        async Task Send(NetworkStream s, string message) 
        { 
            byte[] bytes = Encoding.ASCII.GetBytes(message + "END");
            await s.WriteAsync(bytes, 0, bytes.Length); 
        }

        async Task<string> GetResponse(NetworkStream s)
        {
            Byte[] response = new Byte[bufferSize];

            Console.WriteLine("[{0}] Listening...", id);

            while(!s.DataAvailable);
            while(s.DataAvailable) await s.ReadAsync(response, 0, response.Length);

            string responseS = Encoding.ASCII.GetString(response, 0, response.Length);

            return responseS.Substring(0, responseS.IndexOf("END"));
        }
    }
}