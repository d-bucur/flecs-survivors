using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Flecs.NET.Core;

namespace flecs_survivors;

record struct Powerup(ulong Value = 1);
record struct PowerCollector(float Range, float Exp = 1.6f) {
    public ulong AccumulatedCurrent = 0;
    public ulong AccumulatedTotal = 0;
    public ulong XpToNextLevel = 5;
    public ulong LevelCurrent = 1;
}

record struct UpgradeChoice(string Description, Action<Entity> Apply, bool IsRepeatable = true);
struct CurrentUpgradeChoices() {
    public List<UpgradeChoice> Value = [];
}
struct UpgradeDeck() {
    public List<UpgradeChoice> Value = [];
}

class PowerupModule : IFlecsModule {
	readonly UpgradeMenu _upgradeMenu = new();

    public void InitModule(World world) {
        GameState.InitGame.Observe<OnStateEntered>(() => InitPowerupDeck(ref world));
        _upgradeMenu.Init(world);

        world.System<Powerup, Transform, PhysicsBody>()
            .Kind(Ecs.PreUpdate)
            .Iter(AttractPowerups);
    }

    private static void InitPowerupDeck(ref World world) {
        UpgradeDeck deck = new();
        deck.Value = [
            new UpgradeChoice("Shotgun Weapon", MakeNewWeaponUpgrade(Weapons.PresetShotgun), false),
            new UpgradeChoice("Spiral Weapon", MakeNewWeaponUpgrade(Weapons.PresetSpiral), false),
            new UpgradeChoice("Spread Weapon", MakeNewWeaponUpgrade(Weapons.PresetSpread), false),
            new UpgradeChoice("SMG Weapon", MakeNewWeaponUpgrade(Weapons.PresetClosestSMG), false),
        ];
        world.Set(deck);
    }

    private static Action<Entity> MakeNewWeaponUpgrade(IBulletPattern weapon) {
        return entity => {
            entity.Get<Shooter>().Weapons.Add(weapon);
            entity.CsWorld().Get<UpgradeDeck>().Value.AddRange(weapon.Upgrades);
        };
    }

    static void AttractPowerups(Iter it, Field<Powerup> powerup, Field<Transform> transform, Field<PhysicsBody> body) {
        var collectorQ = it.World().QueryBuilder().With<PowerCollector>().Build();
        var collector = collectorQ.First().Get<Transform>();
        var rangeSq = MathF.Pow(collectorQ.First().Get<PowerCollector>().Range, 2);

        const float SPEED = 0.3f;
        foreach (var i in it) {
            Vector2 dist = collector.Pos - transform[i].Pos;
            if (dist.LengthSquared() >= rangeSq) continue;

            dist = Vector2.Normalize(dist);
            body[i].Vel = dist * SPEED;
        }
    }

    internal static void CreateUpgradeChoices(Entity e) {
        var deck = e.CsWorld().Get<UpgradeDeck>();
        CurrentUpgradeChoices choices = new();
        for (int i = 0; i < 4 && deck.Value.Count > 0; i++) {
            int randomIdx = Random.Shared.Next() % deck.Value.Count;
            UpgradeChoice selected = deck.Value[randomIdx];
            deck.Value.RemoveAt(randomIdx);
            choices.Value.Add(selected);
        }
        e.Set(choices);

        GameState.ChangeState(GameState.LevelUp);
    }

    internal static void HandlePowerCollected(Entity e, ref OnCollisionEnter collision) {
        if (!collision.Other.Has<Powerup>() || !e.Has<PowerCollector>()) return;
        bool shouldDestructOther = false;
        collision.Other.Read((ref readonly Powerup powerup) => {
            var powerupValue = powerup.Value;
            e.Write((ref PowerCollector collector) => {
                // Increment XP and level up
                collector.AccumulatedCurrent += powerupValue;
                collector.AccumulatedTotal += powerupValue;
                long diff = (long)collector.AccumulatedCurrent - (long)collector.XpToNextLevel;
                if (diff >= 0) {
                    CreateUpgradeChoices(e);
                    collector.AccumulatedCurrent = (ulong)diff;
                    collector.LevelCurrent += 1;
                    collector.XpToNextLevel = (ulong)((double)collector.XpToNextLevel * collector.Exp);
                }
                shouldDestructOther = true;
            });
        });
        if (shouldDestructOther)
            collision.Other.Destruct();
    }

    enum UpgradeMenuTag;
    class UpgradeMenu : MenuBase<UpgradeMenuTag> {
        public UpgradeMenu() : base(GameState.LevelUp, ["Placeholder"]) {
            ButtonHeight = 100;
        }

        protected override void EnterMenu(ref World world) {
            var choices = CachedQueries.currentUpgradeChoices.First().Get<CurrentUpgradeChoices>();
            Choices = [.. choices.Value.Select(e => e.Description)];
            base.EnterMenu(ref world);
        }

        override protected void HandleMenuTransition(Entity _, ref MenuSelectable selectable, ref InputManager actions) {
            if (!actions.IsPressed(InputActions.CONFIRM)) return;

            // Apply selected upgrade
            Entity choicesEntity = CachedQueries.currentUpgradeChoices.First();
			var choices = choicesEntity.Get<CurrentUpgradeChoices>();
            var choiceIdx = selectable.CurrentValue;
            choices.Value[choiceIdx].Apply(choicesEntity);

            // Shuffle remaining items into deck
            var deck = choicesEntity.CsWorld().Get<UpgradeDeck>();
            for (int i = 0; i < choices.Value.Count; i++) {
                bool reshuffle = i != choiceIdx || choices.Value[choiceIdx].IsRepeatable;
                if (reshuffle)
                    deck.Value.Add(choices.Value[i]);
            }

            // Cleanup
            choicesEntity.Remove<CurrentUpgradeChoices>();
            GameState.ChangeState(GameState.Running); 
        }
    }

    // Old code for weapons upgrades
    // static void UpdateWeaponLevels(ref PowerCollector collector, ref Shooter shooter) {
    //     var level = 1 + (uint)collector.AccumulatedCurrent / 5;
    //     foreach (var weapon in shooter.Weapons) {
    //         if (weapon.Level == level) continue;
    //         weapon.Level = level;
    //         Console.WriteLine($"Updated weapon level to {level}");
    //     }
    // }
}