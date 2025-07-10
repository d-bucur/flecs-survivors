using System;
using System.Collections.Generic;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;

namespace flecs_survivors;

record struct Powerup(ulong Value = 1);
record struct PowerCollector(float Range, float Exp = 2f) {
    public ulong AccumulatedCurrent = 0;
    public ulong AccumulatedTotal = 0;
    public ulong XpToNextLevel = 5;
    public ulong LevelCurrent = 1;
}

struct LevelUpChoices {
    public LevelUpChoices() { }
    public List<UpgradeChoice> Value = new();

    public bool Enabled = false;
    public Vector2 AnchorUI;
}
record struct UpgradeChoice(string Description, Action Apply);


class PowerupModule : IFlecsModule {
    public void InitModule(World world) {
        world.System<Powerup, Transform, PhysicsBody>()
            .Kind(Ecs.PreUpdate)
            .Iter(AttractPowerups);

        world.System<RenderCtx, LevelUpChoices>()
            .TermAt(0).Singleton()
            .Kind<PostRenderPhase>()
            .Each(DrawLevelUpScreen)
            .Entity.DependsOn(GameState.LevelUp);

        world.System<LevelUpChoices>()
            .With<Player>()
            .Kind(Ecs.PostLoad)
            .Each(ProcessUpgradeChoices)
            .Entity.DependsOn(GameState.LevelUp);
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

    private void ProcessUpgradeChoices(Entity e, ref LevelUpChoices choices) {
        if (!choices.Enabled) return;

        int choiceIdx = -1;
        if (Raylib.IsKeyPressed(KeyboardKey.W)) choiceIdx = 0;
        else if (Raylib.IsKeyPressed(KeyboardKey.A)) choiceIdx = 1;
        else if (Raylib.IsKeyPressed(KeyboardKey.S)) choiceIdx = 2;
        else if (Raylib.IsKeyPressed(KeyboardKey.D)) choiceIdx = 3;
        if (choiceIdx == -1) return;

        choices.Value[choiceIdx].Apply();
        e.Remove<LevelUpChoices>();
        GameState.ChangeState(GameState.Running);
    }

    internal static void CreateUpgradeChoices(Entity e) {
        GameState.ChangeState(GameState.LevelUp);
        Console.WriteLine($"Building upgrades");

        LevelUpChoices choices = new();
        choices.Value.Add(new UpgradeChoice("Shotgun Weapon", () => e.Get<Shooter>().Weapons.Add(Weapons.PresetShotgun)));
        choices.Value.Add(new UpgradeChoice("Spiral Weapon", () => e.Get<Shooter>().Weapons.Add(Weapons.PresetSpiral)));
        choices.Value.Add(new UpgradeChoice("Spread Weapon", () => e.Get<Shooter>().Weapons.Add(Weapons.PresetSpread)));
        choices.Value.Add(new UpgradeChoice("SMG Weapon", () => e.Get<Shooter>().Weapons.Add(Weapons.PresetClosestSMG)));
        e.Set(choices);

        new Tween(e).With(
            (ref LevelUpChoices c, Vector2 v) => { c.AnchorUI = v; },
            new Vector2(100, 600), new Vector2(100, 100),
            1500f, Ease.QuartOut, Vector2.Lerp,
            (ref LevelUpChoices c) => { c.Enabled = true; }
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
                    collector.XpToNextLevel *= (ulong)collector.Exp;
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

    private void DrawLevelUpScreen(ref RenderCtx ctx, ref LevelUpChoices choices) {
        var offset = choices.AnchorUI;
        int width = ctx.WinSize.X / 3;
        int height = ctx.WinSize.Y / 3;
        Render.DrawTextShadowed("Choose an upgrade", (int)offset.X + width, (int)offset.Y - 30);
        DrawUpgradeChoice($"{choices.Value[0].Description}\nPress W", new Rectangle(offset.X, offset.Y, width, height));
        DrawUpgradeChoice($"{choices.Value[1].Description}\nPress A", new Rectangle(offset.X, offset.Y + height, width, height));
        DrawUpgradeChoice($"{choices.Value[2].Description}\nPress S", new Rectangle(offset.X + width, offset.Y, width, height));
        DrawUpgradeChoice($"{choices.Value[3].Description}\nPress D", new Rectangle(offset.X + width, offset.Y + height, width, height));
    }

    private void DrawUpgradeChoice(string text, Rectangle rect) {
        float borderThick = 3;
        Raylib.DrawRectangleRec(rect, Raylib.Fade(Color.DarkGray, 0.7f));
        Raylib.DrawRectangleLinesEx(rect, borderThick, Raylib.Fade(Color.Black, 0.3f));
        Render.DrawTextShadowed(text, (int)(rect.X + 30), (int)(rect.Y + 30));
    }
}