using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Objects
{
    public class Coordinate
    {
        public int x, y;
        public Coordinate(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Same(Coordinate c) { return x == c.x && y == c.y; }
    }


    public enum Actions
    {
        Basic
    }

    public class MAction
    {
        public string result;
        public int cooldown;

        public static MAction ResolveAction(Actions action, Character s = null, int d = 0, int c = 0)
        {
            switch (action)
            {
                case Actions.Basic: return Basic(s, d, c);
                default: return new MAction() {result = "Unknown action"};
            }
        }
        public static MAction Basic(Character source, int Damage, int cooldown)
        {
            source.Target.Health -= Damage;
            if (source.Target.Retaliate && source.Target.Target == null) source.Target.Target = source;
            string result = source.Name + " " + source.WeaponSound + " " + source.Target.Name + " for " + Damage + " damage!";
            return new MAction(){result = result, cooldown = cooldown};
        }
    }

    public class Character
    {
        public string Name, WeaponSound;

        public Character Target;

        public bool Retaliate;

        public virtual void Behavior() {}

        public virtual void Die() 
        {
            Target = null;
            Health = MaxHealth;
            Task.Run(() => Methods.Announce(Name + " has died!", Position));
        }

        public int Health, MaxHealth, Dead;
        public Coordinate Position;
        
    }

    public abstract class Monster : Character
    {
        public int Damage;
        public int RespawnTime;
        public int Cooldown;

        public override void Die()
        {
            Target = null;
            Dead = RespawnTime;
            Health = MaxHealth;
            Task.Run(() => Methods.Announce(Name + " has died!", Position));
        }
    }

    public class Player : Character
    {
        public string Username;

        public int Cooldown;
        public List<string> Skills;
        public string Password;
        public bool admin = false;

        public Coordinate Respawn;

        public override void Die()
        {
            Target = null;
            Health = MaxHealth;
            Coordinate deathLoc = new Coordinate(Position.x, Position.y);
            Task.Run(() => Methods.Announce(Name + " has died!", deathLoc, this));
            Task.Run(() => Methods.Send(Server.Server.Sessions.Find(x => x.Player == this).Client.GetStream(), "Oof, you are dead!"));
            Position = Respawn;
        }

        public override void Behavior()
        {
            if (Health <= 0) Die();
            if (Cooldown == 0 && Target != null)
            {
                if (Target.Dead > 0 || !Target.Position.Same(Position)) Target = null;
                else
                {
                    MAction attack = MAction.ResolveAction(Actions.Basic, this, 3, 3);
                    Task.Run(() => Methods.Announce(attack.result, Position));
                    Cooldown += attack.cooldown;
                }
            }
            if (Cooldown > 0)
            {
                --Cooldown;
            }
        }

        public string Examine()
        {
            Tile tile = Server.Server.Tiles[Position.x, Position.y];
            string examine = tile.description;


            if (Server.Server.Sessions.Count > 1)
            {
                var activePlayers = Server.Server.Sessions.FindAll(x => x.Player != this && x.Player.Position.Same(Position));

                if (activePlayers.Count > 0)
                {
                    examine += "\nYou can see ";
                    examine += activePlayers[0].Player.Name;
                    if (activePlayers.Count > 1)
                    {
                        for (int x = 0; x < activePlayers.Count; ++x)
                        {
                            examine += (activePlayers.Count == x + 1 ? " and " + activePlayers[x].Player.Name : ", " + activePlayers[x].Player.Name);
                        }
                    }
                    examine += ".\n";
                }
            }

            List<Monster> monsters = Server.Server.Monsters.FindAll(x => x.Dead == 0 && x.Position.Same(Position));

            if (monsters.Count > 0)
            {
                examine += "\nYou can see ";
                examine += monsters[0].Name;
                if (monsters.Count > 1) 
                {
                    for (int x = 0; x < monsters.Count; ++x) examine += (monsters.Count == x + 1 ? " and " + monsters[x].Name : ", " + monsters[x].Name);
                }
                examine += ".\n";
            }

            examine += "\nExits: " + (!(tile.n || tile.s || tile.e || tile.w) ? "None" : "") + (tile.n ? "North" + (tile.s || tile.e || tile.w ? " : " : "") : "") + (tile.s ? "South" + (tile.e || tile.w ? " : " : "") : "") + (tile.e ? "East" + (tile.w ? " : " : "") : "") + (tile.w ? "West" : "") + ".";

            return examine;
        }

        public Player(string username, string password, Coordinate respawn, int Health)
        {
            this.Username = username;
            this.Name = username;
            this.Password = password;
            this.Position = respawn;
            this.Respawn = respawn;
            this.Health = Health;
            this.MaxHealth = Health;
            Retaliate = false;
            WeaponSound = "punches";
            Skills = new List<string>();
            Skills.Add("Basic");
        }

        
    }

    public class Tile
    {
        public string description;

        public bool n, s, w, e, pvp;

        public Tile(string description = "Test description", bool n = false, bool s = false, bool w = false, bool e = false, bool pvp = false)
        {
            this.description = description;
            this.n = n;
            this.s = s;
            this.w = w;
            this.e = e;
            this.pvp = pvp;
        }
    }

    public static class Methods
    {
        static int bufferSize = 8192;
        public static T[,] InitiateCollection<T>(this T[,] arr, Func<T> init)
        {
            for (int x = 0; x < arr.GetLength(0); ++x) for (int y = 0; y < arr.GetLength(1); ++y)
                {
                    arr[x, y] = init();
                }
            return arr;
        }

        public static string Namelist(this List<Player> players)
        {
            string s = "";
            for (int x = 0; x < players.Count; ++x) s += (players[x].admin ? "[A] " : "") + players[x].Name + "\n";
            return s;
        }

        public static async Task Send(NetworkStream s, string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message + "END");
            await s.WriteAsync(bytes, 0, bytes.Length);
        }

        public static async Task<string> GetResponse(NetworkStream s)
        {
            Byte[] response = new Byte[bufferSize];

            while (!s.DataAvailable) ;
            while (s.DataAvailable) await s.ReadAsync(response, 0, response.Length);

            string responseS = Encoding.UTF8.GetString(response, 0, response.Length);

            return responseS.Substring(0, responseS.IndexOf("END"));
        }

        public static async Task Announce(string message, Coordinate position, Player self = null)
        {
            foreach (Server.SessionRef session in Server.Server.Sessions.FindAll((x => x.Player != self && x.Player.Position.Same(position))))
            {
                await Methods.Send(session.Client.GetStream(), message);
            }
        }
    }
}