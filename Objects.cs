using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MainServer;

namespace Objects
{
    public enum WeaponClass
    {
        None,
        Sword,
        Greatsword,
        Axe,
        Dagger,
        Club
    }
    public struct Item
    {
        
        public string Name, Description;
        public WeaponClass weaponClass;

        public Item(string Name, string Description, WeaponClass weaponClass = WeaponClass.None)
        {
            this.Name = Name;
            this.Description = Description;
            this.weaponClass = weaponClass;
        }
    }
    public struct Coordinate
    {
        public int x, y;
        public Coordinate(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Same(Coordinate c) { return x == c.x && y == c.y; }
    }

    public struct Drop
    {
        public Item Item;
        public uint Weight;

        public Drop(Item Item, uint Weight)
        {
            this.Item = Item;
            this.Weight = Weight;
        }

        public static Item DropItem(Drop[] Drops)
        {
            uint totalWeight = 0;
            foreach (Drop i in Drops) totalWeight += i.Weight;
            uint roll = (uint)(new Random().NextDouble() * totalWeight);
            totalWeight = 0;
            foreach (Drop i in Drops) if (i.Weight + totalWeight > roll) return i.Item; else totalWeight += i.Weight;
            return new Item("Nothing", "If you can see this, something went wrong.");
        }
    }

    public enum Actions
    {
        Basic,
        Roar
    }

    public struct MAction
    {
        public string result;
        public int cooldown;
        public static MAction Basic(Character source)
        {
            int Damage = source.Strength;
            source.Target.Health -= Damage;
            if (source.Target.Attacker == null) source.Target.Attacker = source;
            string result = source.Name + " " + source.WeaponSound + " " + source.Target.Name + " for " + Damage + " damage!";
            return new MAction(){result = result, cooldown = source.AttackSpeed};
        }

        public static MAction Roar(Character source)
        {
            int stun = source.Strength * 10;
            source.Target.Cooldown = stun;
            if (source.Target.Attacker == null) source.Target.Attacker = source;
            string result = source.Name + " roars!\n" + source.Target.Name + " is stunned for " + stun + " ticks!";
            return new MAction(){result = result, cooldown = source.AttackSpeed * 2};
        }
    }

    public struct Skill
    {
        public string Name;
        public uint Level;
        public ulong XP;
    }

    public class Character
    {
        public string Name, WeaponSound;

        public int Strength;

        public int AttackSpeed;

        public Character Target;

        public Character Attacker;

        public bool Retaliate;

        public int Cooldown {get;set;}

        public virtual void Behavior() {}

        public MAction GetAction(Actions action)
        {
            switch(action)
            {
                case Actions.Roar: return MAction.Roar(this);
                default: return MAction.Basic(this);
            }
        }

        public virtual void Die() 
        {
            Target = null;
            Health = MaxHealth;
            Task.Run(() => Methods.Announce(Name + " has died!", Position));
        }

        public int Health, MaxHealth, Dead;
        public Coordinate Position;
        
    }

    public class Monster : Character
    {
        public int RespawnTime;

        public Drop[] Drops;

        public int DropId;

        public override void Die()
        {
            Target = null;
            Dead = RespawnTime;
            Health = MaxHealth;
            Item drop;
            if (Drops != null) drop = Drop.DropItem(Drops);
            else drop = Drop.DropItem(Server.DropsTables[DropId]);
            string killer = Attacker.Name;
            Task.Run(() => Methods.Announce(killer + " killed " + Name + "!", Position));
            Attacker = null;
            if (drop.Name != "Nothing")
            {
                Server.Tiles[Position.x, Position.y].Items.Add(drop);
                Task.Run(() => Methods.Announce(Name + " dropped " + drop.Name + "!", Position));
            }
        }
    }

    public class MonsterReference : Monster
    {
        public string MonsterType;

        public MonsterReference(Monster reference = null)
        {
            if (reference != null)
            {
                this.Name = reference.Name;
                this.DropId = reference.DropId;
                this.Drops = reference.Drops;
                this.MaxHealth = reference.MaxHealth;
                this.Position = reference.Position;
                this.RespawnTime = reference.RespawnTime;
                this.Retaliate = reference.Retaliate;
                this.WeaponSound = reference.WeaponSound;
                this.Strength = reference.Strength;
                this.AttackSpeed = reference.AttackSpeed;

                this.MonsterType = reference.GetType().Name;
            }
        }
    }

    public class Player : Character
    {
        public float XPGrowth;
        public string Username;
        public Skill[] Skills;

        public List<Item> Inventory;

        public uint skillTotal;

        public Actions[] actions;

        public string Password;
        public bool admin = false;

        public Coordinate Respawn;

        public void GainXP(Skill skill, ulong xp)
        {
            skill.XP += xp;
            Task.Run(() => Methods.Send(Server.Sessions.Find(x => x.Player == this).Client.GetStream(), "You gained " + xp + " in " + skill.Name + "!"));
            if (skill.XP >= XPForNext(skill)) 
            {
                ++skill.Level;
                ++skillTotal;
                Task.Run(() => Methods.Send(Server.Sessions.Find(x => x.Player == this).Client.GetStream(), "You reached level " + skill.Level + " in " + skill.Name + "!"));
            }
        }

        public ulong XPForNext(Skill skill) { return (ulong)(Math.Pow(skillTotal + (skill.Level / 5), XPGrowth)); }


        public override void Die()
        {
            Target = null;
            Health = MaxHealth;
            Coordinate deathLoc = new Coordinate(Position.x, Position.y);
            string killer = Attacker.Name;
            Task.Run(() => Methods.Announce(killer + " killed " + Name + "!", deathLoc, this));
            Task.Run(() => Methods.Send(Server.Sessions.Find(x => x.Player == this).Client.GetStream(), "You were killed by " + killer + "!"));
            Attacker = null;
            Position = Respawn;
        }

        public override void Behavior()
        {
            if (Health <= 0) Die();
            if (Attacker != null)
            {
                if (Attacker.Dead > 0 || !Attacker.Position.Same(Position)) Attacker = null;
                else if (Target == null && Retaliate) Target = Attacker;
            }
            if (Cooldown == 0 && Target != null)
            {
                if (Target.Dead > 0 || !Target.Position.Same(Position)) Target = null;
                else
                {
                    MAction attack = GetAction(Actions.Basic);
                    Task.Run(() => Methods.Announce(attack.result, Position));
                    Cooldown += attack.cooldown;
                }
            }
            if (Cooldown > 0)
            {
                --Cooldown;
            }
        }

        public string ExamineObject(string name)
        {
            if (Server.Sessions.Exists(x => x.Player != null && x.Player.Name == name && x.Player.Position.Same(Position)))
            {
                Player player = Server.Sessions.Find(x => x.Player != null && x.Player != this && x.Player.Position.Same(Position)).Player;
                return player.Name + " | This is " + player.Name + "\n";
            }

            if (Server.Monsters.Exists(x => x.Name == name && x.Position.Same(Position)))
            {
                Monster monster = Server.Monsters.Find(x => x.Name == name && x.Position.Same(Position));
                return monster.Name + " | This is " + monster.Name + "\n";
            }

            if (Server.Tiles[Position.x, Position.y].Items.Exists(x => x.Name == name))
            {
                Item item = Server.Tiles[Position.x, Position.y].Items.Find(x => x.Name == name);
                return item.Name + " | " + item.Description + "\n";
            }

            if (Inventory.Exists(x => x.Name == name))
            {
                Item item = Inventory.Find(x => x.Name == name);
                return item.Name + " | " + item.Description + "\n";
            }

            return "There is nothing by that name.\n";
        }

        public string Examine()
        {
            Tile tile = Server.Tiles[Position.x, Position.y];
            string examine = tile.description;


            if (Server.Sessions.Count > 1)
            {
                var activePlayers = Server.Sessions.FindAll(x => x.Player != null && x.Player != this && x.Player.Position.Same(Position));

                if (activePlayers.Count > 0)
                {
                    examine += "\nYou can see ";
                    examine += activePlayers[0].Player.Name;
                    if (activePlayers.Count > 1)
                    {
                        for (int x = 1; x < activePlayers.Count; ++x)
                        {
                            examine += (activePlayers.Count == x ? " and " + activePlayers[x].Player.Name : ", " + activePlayers[x].Player.Name);
                        }
                    }
                    examine += ".\n";
                }
            }

            List<Monster> monsters = Server.Monsters.FindAll(x => x.Dead == 0 && x.Position.Same(Position));

            if (monsters.Count > 0)
            {
                Console.WriteLine(monsters[0].Name);
                examine += "\nYou can see ";
                examine += monsters[0].Name;
                if (monsters.Count > 1) 
                {
                    for (int x = 0; x < monsters.Count; ++x) examine += (monsters.Count == x + 1 ? " and " + monsters[x].Name : ", " + monsters[x].Name);
                }
                examine += ".\n";
            }

            Item[] items = tile.Items.ToArray();

            if (items.Length > 0)
            {
                examine += "\n - Items - \n\n";
                foreach(Item item in items) examine += "- " + item.Name + "\n";
            }

            examine += "\nExits: " + (!(tile.n || tile.s || tile.e || tile.w) ? "None" : "") 
            + (tile.n ? "North" + (tile.s || tile.e || tile.w ? " : " : "") : "") 
            + (tile.s ? "South" + (tile.e || tile.w ? " : " : "") : "") 
            + (tile.e ? "East" + (tile.w ? " : " : "") : "") 
            + (tile.w ? "West" : "") + ".";

            return examine;
        }

        public Player(string username, string password, Coordinate respawn, int Health/*, Skill[] Skills*/)
        {
            this.Username = username;
            this.Name = username;
            this.Password = password;
            this.Position = respawn;
            this.Respawn = respawn;
            this.Health = Health;
            this.MaxHealth = Health;
            XPGrowth = 1.3f;
            //this.Skills = Skills;
            this.Inventory = new List<Item>();
            Retaliate = false;
            WeaponSound = "punches";
            this.AttackSpeed = 1;
        }

        
    }

    public class Tile
    {
        public string description;

        public List<Item> Items = new List<Item>();

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
        static public List<MonsterReference> ToBase(this List<Monster> Monsters)
        {
            List<MonsterReference> Base = new List<MonsterReference>();
            Console.WriteLine("a");
            foreach (Monster Monster in Monsters) Base.Add(new MonsterReference(Monster));
            return Base;
        }

        static public List<Monster> FromBase(this List<MonsterReference> Base)
        {
            List<Monster> Monsters = new List<Monster>();
            foreach (MonsterReference mRef in Base)
            {
                dynamic Monster;
                switch (mRef.MonsterType)
                {
                    case "Goblin":
                        Console.WriteLine("Generated Goblin");
                        Monster = new Monsters.Goblin(mRef.Name, mRef.WeaponSound, mRef.MaxHealth, mRef.Strength, mRef.AttackSpeed, mRef.RespawnTime, mRef.Position, mRef.Drops, mRef.DropId, mRef.Retaliate);
                        break;

                    case "Orc":
                        Console.WriteLine("Generated Orc");
                        Monster = new Monsters.Orc(mRef.Name, mRef.WeaponSound, mRef.MaxHealth, mRef.Strength, mRef.AttackSpeed, mRef.RespawnTime, mRef.Position, mRef.Drops, mRef.DropId, mRef.Retaliate);
                        break;

                    default:
                        Monster = new Monster();
                        break;
                }
                Monsters.Add(Monster);
            }
            return Monsters;
        }

        static int bufferSize = 8192;

        public static T[,] InitiateArray2D<T>(this T[,] arr, Func<T> init)
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
            foreach (SessionRef session in Server.Sessions.FindAll((x => x.Player != self && x.Player.Position.Same(position))))
            {
                await Methods.Send(session.Client.GetStream(), message);
            }
        }
    }
}