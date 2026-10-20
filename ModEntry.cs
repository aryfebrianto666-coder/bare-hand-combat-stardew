using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Monsters;

namespace BareHandCombatAndroid;

internal sealed class ModEntry : Mod
{
    private const int PunchDamage = 20;
    private const int ButtonWidth = 150;
    private const int ButtonHeight = 62;
    private const int ButtonRightMargin = 24;
    private const int ButtonBottomMargin = 180;

    private Rectangle buttonBounds;
    private bool buttonVisible;

    public override void Entry(IModHelper helper)
    {
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.Display.WindowResized += (_, _) => UpdateButtonBounds();
        UpdateButtonBounds();

        Monitor.Log(
            "Development build loaded. Touch punch button enabled; damage is configured to 20. Not yet verified on Android.",
            LogLevel.Info
        );
    }

    private void UpdateButtonBounds()
    {
        int width = Math.Max(1, Game1.uiViewport.Width);
        int height = Math.Max(1, Game1.uiViewport.Height);

        buttonBounds = new Rectangle(
            Math.Max(8, width - ButtonWidth - ButtonRightMargin),
            Math.Max(8, height - ButtonHeight - ButtonBottomMargin),
            ButtonWidth,
            ButtonHeight
        );
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        buttonVisible = Context.IsWorldReady && Game1.activeClickableMenu == null;
        if (!buttonVisible)
            return;

        UpdateButtonBounds();
        SpriteBatch b = e.SpriteBatch;

        // High-contrast touch target; this is a drawn HUD button, not a game menu.
        b.Draw(
            Game1.staminaRect,
            buttonBounds,
            new Color(65, 38, 31, 230)
        );
        b.Draw(
            Game1.staminaRect,
            new Rectangle(buttonBounds.X + 3, buttonBounds.Y + 3, buttonBounds.Width - 6, buttonBounds.Height - 6),
            new Color(130, 75, 55, 235)
        );

        string label = "PUNCH";
        Vector2 size = Game1.smallFont.MeasureString(label);
        Vector2 pos = new Vector2(
            buttonBounds.Center.X - size.X / 2f,
            buttonBounds.Center.Y - size.Y / 2f - 2f
        );
        b.DrawString(Game1.smallFont, label, pos, Color.White);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!buttonVisible || !Context.IsWorldReady || Game1.activeClickableMenu != null)
            return;

        if (e.Button != SButton.MouseLeft)
            return;

        Vector2 cursor = e.Cursor.GetScaledScreenPixels();
        if (!buttonBounds.Contains((int)cursor.X, (int)cursor.Y))
            return;

        Helper.Input.Suppress(SButton.MouseLeft);
        Punch();
    }

    private void Punch()
    {
        Farmer player = Game1.player;
        if (player == null || Game1.currentLocation == null)
            return;

        // Find a monster in close range and generally in the direction the farmer faces.
        Vector2 playerCenter = player.GetBoundingBox().Center.ToVector2();
        Vector2 facing = player.FacingDirection switch
        {
            0 => new Vector2(0, -1),
            1 => new Vector2(1, 0),
            2 => new Vector2(0, 1),
            3 => new Vector2(-1, 0),
            _ => Vector2.Zero
        };

        Monster? target = Game1.currentLocation.characters
            .OfType<Monster>()
            .Where(m => m.Health > 0)
            .Select(m => new
            {
                Monster = m,
                Delta = m.GetBoundingBox().Center.ToVector2() - playerCenter
            })
            .Where(x => x.Delta.LengthSquared() > 0.01f && x.Delta.Length() <= 112f && Vector2.Dot(Vector2.Normalize(x.Delta), facing) >= 0.15f)
            .OrderBy(x => x.Delta.LengthSquared())
            .Select(x => x.Monster)
            .FirstOrDefault();

        if (target == null)
        {
            Game1.playSound("swing");
            return;
        }

        Vector2 away = target.Position - player.Position;
        int xTrajectory = away.X < 0 ? -1 : 1;
        int yTrajectory = away.Y < 0 ? -1 : 1;

        target.takeDamage(PunchDamage, xTrajectory, yTrajectory, false, 0.0, player);
        Game1.playSound("clubhit");

        // Lightweight impact feedback. The exact sprite index and appearance still need in-game verification.
        Game1.currentLocation.temporarySprites.Add(
            new TemporaryAnimatedSprite(362, target.Position, Color.OrangeRed)
        );
    }
}
