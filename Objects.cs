using System;
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
    }

    public class Behaviors
    {
        public static async Task Behavior(List<Monster> mobs)
        {
            foreach(Monster c in mobs.FindAll(x => x.Dead == 0)) await Task.Run(() => c.Behavior);
            foreach(Monster c in mobs.FindAll(x => x.Dead != 0)) --c.Dead;
            await Task.Delay(Server.Server.TickLength);
        }

        public static async Task Goblin(Monster goblin)
        {
            if (goblin.Health <= 0) goblin.Dead = goblin.RespawnTime;
            if (goblin.Dead == 0)
            {
                if (goblin.Target != null)
                {
                    Action result = Action.ResolveAction("Basic", goblin, goblin.Target, 1);
                    foreach (Server.SessionRef s in Server.Server.Sessions.FindAll(x => x.Player.Position == goblin.Position)) await Session.SessionHost.Send(s.Client.GetStream(), result.result);
                }
            }
        }
    }

    public class Action
    {
        public string result;
        public int cooldown;

        public static Action ResolveAction(string action, Character t = null, Character s = null, int d = 0, int c = 0)
        {
            switch (action)
            {
                case "Basic": return Basic(t, s, d, c);
                default: return new Action() {result = "Unknown action"};
            }
        }
        public static Action Basic(Character target, Character source, int Damage, int cooldown)
        {
            string result = "" + ("{0} {1} {2} for {3} damage!", source.Name, source.WeaponSound, target.Name, Damage);
            return new Action(){result = result, cooldown = cooldown};
        }
    }

    public class Character
    {
        public string Name, WeaponSound;

        public Character Target;

        public int Health;
        public Coordinate Position;
    }

    public class Monster : Character
    {
        public int Dead;
        public Task Behavior;
        public int RespawnTime;

        public int Cooldown;

        public string MonsterType;

        public Monster(string Name, string WeaponSound, int Health, int RespawnTime, Task Behavior, Coordinate Position, string MonsterType)
        {
            this.Name = Name;
            this.WeaponSound = WeaponSound;
            this.Health = Health;
            this.RespawnTime = RespawnTime;
            this.Behavior = Behavior;
            this.Position = Position;
            this.MonsterType = MonsterType;
            Dead = 0;
            Cooldown = 0;
            Behavior = GetBehavior();
        }

        Task GetBehavior()
        {
            switch(MonsterType)
            {
                case "Goblin":
                return Behaviors.Goblin(this);

                default: return new Task(() => Console.WriteLine("you fucked up"));
            }
        }
    }

    public class Player : Character
    {
        public List<string> Actions;
        public string Password;
        public bool admin = false;

        public Player(string username, string password, Coordinate position, int Health)
        {
            this.Name = username;
            this.Password = password;
            this.Position = position;
            this.Health = Health;
            WeaponSound = "punches";
            Actions = new List<string>();
            Actions.Add("Basic");
        }

        
    }

    public class Tile
    {
        public string description;

        public bool n, s, w, e;

        public Tile(string description = "Test description", bool n = false, bool s = false, bool w = false, bool e = false)
        {
            this.description = description;
            this.n = n;
            this.s = s;
            this.w = w;
            this.e = e;
        }

        public string Examine()
        {
            string examine = description;

            examine += "\nExits: " + (!(n || s || e || w) ? "None" : "") + (n ? "North" + (s || e || w ? " : " : "") : "") + (s ? "South" + (e || w ? " : " : "") : "") + (e ? "East" + (w ? " : " : "") : "") + (w ? "West" : "") + ".";

            return examine;
        }
    }

    public static class Methods
    {
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
    }
}