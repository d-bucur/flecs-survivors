using System;
using System.Collections.Generic;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;

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
    public List<UpgradeChoice> Value = new();
    public bool Enabled = false;
    public Vector2 AnchorUI;
}
struct UpgradeDeck() {
    public List<UpgradeChoice> Value = new();
}

class PowerupModule : IFlecsModule {
    public void InitModule(World world) {
        InitPowerupDeck(world);

        world.System<Powerup, Transform, PhysicsBody>()
            .Kind(Ecs.PreUpdate)
            .Iter(AttractPowerups);

        world.System<RenderCtx, CurrentUpgradeChoices>()
            .TermAt(0).Singleton()
            .Kind<PostRenderPhase>()
            .Each(DrawLevelUpScreen)
            .Entity.DependsOn(GameState.LevelUp);

        world.System<CurrentUpgradeChoices>()
            .With<Player>()
            .Kind(Ecs.PostLoad)
            .Each(ProcessUpgradeChoices)
            .Entity.DependsOn(GameState.LevelUp);
    }

    private static void InitPowerupDeck(World world) {
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

    private void ProcessUpgradeChoices(Entity e, ref CurrentUpgradeChoices choices) {
        if (!choices.Enabled) return;

        int choiceIdx = -1;
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) choiceIdx = 0;
        else if (Raylib.IsKeyPressed(KeyboardKey.E)) choiceIdx = 1;
        else if (Raylib.IsKeyPressed(KeyboardKey.Z)) choiceIdx = 2;
        else if (Raylib.IsKeyPressed(KeyboardKey.C)) choiceIdx = 3;
        if (choiceIdx == -1) return;

        choices.Value[choiceIdx].Apply(e);
        var deck = e.CsWorld().Get<UpgradeDeck>();
        for (int i = 0; i < choices.Value.Count; i++) {
            bool reshuffle = i != choiceIdx || choices.Value[choiceIdx].IsRepeatable;
            if (reshuffle)
                deck.Value.Add(choices.Value[i]);
        }
        e.Remove<CurrentUpgradeChoices>();
        GameState.ChangeState(GameState.Running);
    }

    internal static void CreateUpgradeChoices(Entity e) {
        GameState.ChangeState(GameState.LevelUp);

        var deck = e.CsWorld().Get<UpgradeDeck>();
        CurrentUpgradeChoices choices = new();
        for (int i = 0; i < 4 && deck.Value.Count > 0; i++) {
            int randomIdx = Random.Shared.Next() % deck.Value.Count;
            UpgradeChoice selected = deck.Value[randomIdx];
            deck.Value.RemoveAt(randomIdx);
            choices.Value.Add(selected);
        }
        e.Set(choices);

        // Tween in UI animation
        new Tween(e).With(
            (ref CurrentUpgradeChoices c, Vector2 v) => { c.AnchorUI = v; },
            new Vector2(100, 600), new Vector2(100, 100),
            1500f, Ease.QuartOut, Vector2.Lerp,
            (ref CurrentUpgradeChoices c) => { c.Enabled = true; }
        ).RegisterEcs();
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

    // Old code for weapons upgrades
    // static void UpdateWeaponLevels(ref PowerCollector collector, ref Shooter shooter) {
    //     var level = 1 + (uint)collector.AccumulatedCurrent / 5;
    //     foreach (var weapon in shooter.Weapons) {
    //         if (weapon.Level == level) continue;
    //         weapon.Level = level;
    //         Console.WriteLine($"Updated weapon level to {level}");
    //     }
    // }

    private void DrawLevelUpScreen(ref RenderCtx ctx, ref CurrentUpgradeChoices choices) {
        var offset = choices.AnchorUI;
        int width = ctx.WinSize.X / 3;
        int height = ctx.WinSize.Y / 3;
        Render.DrawTextShadowed("Choose an upgrade", (int)offset.X + width, (int)offset.Y - 30);
        if (choices.Value.Count > 0)
            DrawUpgradeChoice($"{choices.Value[0].Description}\nPress Q", new Rectangle(offset.X, offset.Y, width, height));
        if (choices.Value.Count > 1)
            DrawUpgradeChoice($"{choices.Value[1].Description}\nPress E", new Rectangle(offset.X, offset.Y + height, width, height));
        if (choices.Value.Count > 2)
            DrawUpgradeChoice($"{choices.Value[2].Description}\nPress Z", new Rectangle(offset.X + width, offset.Y, width, height));
        if (choices.Value.Count > 3)
            DrawUpgradeChoice($"{choices.Value[3].Description}\nPress C", new Rectangle(offset.X + width, offset.Y + height, width, height));
    }

    private void DrawUpgradeChoice(string text, Rectangle rect) {
        float borderThick = 3;
        Raylib.DrawRectangleRec(rect, Raylib.Fade(Color.DarkGray, 0.7f));
        Raylib.DrawRectangleLinesEx(rect, borderThick, Raylib.Fade(Color.Black, 0.3f));
        Render.DrawTextShadowed(text, (int)(rect.X + 30), (int)(rect.Y + 30));
    }
}