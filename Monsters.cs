using Objects;
using System;
using System.Threading.Tasks;

namespace Monsters
{
    class Goblin : Objects.Monster
    {
        public Goblin(string Name, string WeaponSound, int MaxHealth, int Strength, int AttackSpeed, int RespawnTime, Coordinate Position, Drop[] Drops = null, int DropId = 0, bool Retaliate = true)
        {
            this.Name = Name;
            this.WeaponSound = WeaponSound;
            this.Health = MaxHealth;
            this.MaxHealth = MaxHealth;
            this.RespawnTime = RespawnTime;
            this.Position = Position;
            this.Drops = Drops;
            this.DropId = DropId;
            this.Retaliate = Retaliate;
            this.Strength = Strength;
            this.AttackSpeed = AttackSpeed;
            Dead = 0;
            Cooldown = 0;
        }

        public override void Behavior()
        {
            try
            {
                if (Health <= 0) Die();
                if (Attacker != null)
                {
                    if (Attacker.Dead > 0 || !Attacker.Position.Same(Position)) Attacker = null;
                    else if (Target == null && Retaliate) Target = Attacker;
                }
                if (Dead == 0 && Cooldown == 0 && Target != null)
                {
                    if (Target.Dead > 0 || !Target.Position.Same(Position)) Target = null;
                    else
                    {
                        MAction attack = GetAction(Actions.Basic);
                        Task.Run(() => Methods.Announce(attack.result, Position));
                        Cooldown += attack.cooldown;
                    }
                }
                if (Dead > 0) if (--Dead == 0) Task.Run(() => Methods.Announce(Name + " has awakened!", Position));
                if (Cooldown > 0)
                {
                    --Cooldown;
                }
            }
            catch (Exception e) { Console.WriteLine(e); }
        }
    }

    class Orc : Monster
    {
        public Orc(string Name, string WeaponSound, int MaxHealth, int Strength, int AttackSpeed, int RespawnTime, Coordinate Position, Drop[] Drops = null, int DropId = 0, bool Retaliate = true)
        {
            this.Name = Name;
            this.WeaponSound = WeaponSound;
            this.Health = MaxHealth;
            this.MaxHealth = MaxHealth;
            this.RespawnTime = RespawnTime;
            this.Position = Position;
            this.Drops = Drops;
            this.DropId = DropId;
            this.Retaliate = Retaliate;
            this.Strength = Strength;
            this.AttackSpeed = AttackSpeed;
            Dead = 0;
            Cooldown = 0;
        }
        public override void Behavior()
        {
            try
            {
                if (Health <= 0) Die();
                if (Attacker != null)
                {
                    if (Attacker.Dead > 0 || !Attacker.Position.Same(Position)) Attacker = null;
                    else if (Target == null && Retaliate) Target = Attacker;
                }
                if (Dead == 0 && Cooldown == 0 && Target != null)
                {
                    if (Target.Dead > 0 || !Target.Position.Same(Position)) Target = null;
                    else
                    {
                        switch (new Random().Next(1, 5))
                        {
                            case 1:
                                MAction roar = GetAction(Actions.Roar);
                                Task.Run(() => Methods.Announce(roar.result, Position));
                                Cooldown += roar.cooldown;
                                break;

                            default:
                                MAction basic = GetAction(Actions.Basic);
                                Task.Run(() => Methods.Announce(basic.result, Position));
                                Cooldown += basic.cooldown;
                                break;
                        }

                    }
                }
                if (Dead > 0) if (--Dead == 0) Task.Run(() => Methods.Announce(Name + " has awakened!", Position));
                if (Cooldown > 0)
                {
                    --Cooldown;
                }
            }
            catch (Exception e) { Console.WriteLine(e); }
        }
    }
}