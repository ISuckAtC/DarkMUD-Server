using Objects;
using System;
using System.Threading.Tasks;

namespace Monsters
{
    class Goblin : Objects.Monster
    {
        public Goblin(string Name, string WeaponSound, int Health, int RespawnTime, int Damage, Coordinate Position)
        {
            this.Name = Name;
            this.WeaponSound = WeaponSound;
            this.Health = Health;
            this.MaxHealth = Health;
            this.RespawnTime = RespawnTime;
            this.Damage = Damage;
            this.Position = Position;
            Retaliate = true;
            Dead = 0;
            Cooldown = 0;
        }

        public override void Behavior()
        {
            if (Health <= 0) Die();
            if (Dead == 0 && Cooldown == 0 && Target != null)
            {
                if (Target.Dead > 0 || !Target.Position.Same(Position)) Target = null;
                else
                {
                    MAction attack = MAction.ResolveAction(Actions.Basic, this, 1, 5);
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
    }
}