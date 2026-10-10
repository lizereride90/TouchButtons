using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using System;
using xTile.Dimensions;

namespace TouchButtons
{
    internal class ModEntry : Mod
    {
        private ModConfig config;

        // slide mode cursor, in screen pixels
        private Vector2 slideCursor;
        private bool slideCursorSet;
        private bool pointerDown;

        public override void Entry(IModHelper helper)
        {
            this.config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.Input.ButtonReleased += this.OnButtonReleased;
            helper.Events.Input.CursorMoved += this.OnCursorMoved;
            helper.Events.Display.Rendered += this.OnRendered;

            helper.ConsoleCommands.Add("touchbuttons", "Turns Touch Buttons on or off.", this.OnToggleCommand);
        }

        private void OnToggleCommand(string cmd, string[] args)
        {
            this.config.Enabled = !this.config.Enabled;
            this.Helper.WriteConfig(this.config);
            this.Monitor.Log($"Touch Buttons is now {(this.config.Enabled ? "on" : "off")}.", LogLevel.Info);
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.config)
            );

            gmcm.AddBoolOption(this.ModManifest, () => this.config.Enabled, v => this.config.Enabled = v,
                () => "Show buttons", () => "Master switch for the whole overlay.");
            gmcm.AddBoolOption(this.ModManifest, () => this.config.ShowToolbar, v => this.config.ShowToolbar = v,
                () => "Number row", () => "Bottom row, tap 1-0 to pick a toolbar slot.");
            gmcm.AddBoolOption(this.ModManifest, () => this.config.ShowLeftRight, v => this.config.ShowLeftRight = v,
                () => "L / R buttons", () => "Right side: L uses the tool, R does the action.");
            gmcm.AddBoolOption(this.ModManifest, () => this.config.ShowUtility, v => this.config.ShowUtility = v,
                () => "Utility buttons", () => "Top left: inventory, map, close.");
            gmcm.AddBoolOption(this.ModManifest, () => this.config.ShowModeToggle, v => this.config.ShowModeToggle = v,
                () => "Touch / slide toggle", () => "Switch between direct taps and a touchpad cursor.");
            gmcm.AddBoolOption(this.ModManifest, () => this.config.SlideMode, v => this.config.SlideMode = v,
                () => "Slide mode", () => "On = drag moves a cursor, L taps where it points.");
            gmcm.AddNumberOption(this.ModManifest, () => this.config.ButtonAlpha, v => this.config.ButtonAlpha = v,
                () => "Opacity", () => "How solid the buttons look. Lower lets the game show through.",
                min: 0.1f, max: 1f, interval: 0.05f);
        }

        /********* input *********/

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (this.config == null || !Context.IsWorldReady)
                return;

            if (e.Button == SButton.MouseLeft)
                this.pointerDown = true;

            if (!this.config.Enabled || !Context.IsPlayerFree)
                return;

            if (e.Button != SButton.MouseLeft && e.Button != SButton.ControllerA)
                return;

            Vector2 screen = this.Helper.Input.GetCursorPosition().ScreenPixels;
            string hit = this.HitTest(screen);

            if (hit == null)
            {
                // in slide mode a bare tap moves the cursor there instead of acting
                if (this.config.SlideMode && this.config.ShowModeToggle && e.Button == SButton.MouseLeft)
                {
                    this.slideCursor = screen;
                    this.slideCursorSet = true;
                    this.Helper.Input.Suppress(SButton.MouseLeft);
                }
                return;
            }

            this.Helper.Input.Suppress(e.Button);
            this.Press(hit);
        }

        private void OnButtonReleased(object sender, ButtonReleasedEventArgs e)
        {
            if (e.Button == SButton.MouseLeft)
                this.pointerDown = false;
        }

        private void OnCursorMoved(object sender, CursorMovedEventArgs e)
        {
            // slide mode: dragging moves the floating cursor by the same delta
            if (this.config == null || !this.config.Enabled || !this.config.SlideMode)
                return;
            if (!this.pointerDown || !Context.IsWorldReady || !Context.IsPlayerFree)
                return;

            Vector2 delta = e.NewPosition.ScreenPixels - e.OldPosition.ScreenPixels;
            if (!this.slideCursorSet)
            {
                this.slideCursor = e.NewPosition.ScreenPixels;
                this.slideCursorSet = true;
            }
            else
            {
                this.slideCursor += delta * 1.5f;
            }

            var view = Game1.uiViewport;
            this.slideCursor.X = MathHelper.Clamp(this.slideCursor.X, 0, view.Width);
            this.slideCursor.Y = MathHelper.Clamp(this.slideCursor.Y, 0, view.Height);
        }

        /********* actions *********/

        private void Press(string hit)
        {
            Farmer who = Game1.player;
            if (who == null)
                return;

            if (hit == "mode")
            {
                this.config.SlideMode = !this.config.SlideMode;
                this.Helper.WriteConfig(this.config);
                Game1.addHUDMessage(new HUDMessage(this.config.SlideMode ? "Slide cursor" : "Direct touch", 2));
                return;
            }

            if (hit == "util_inventory")
            {
                Game1.activeClickableMenu = new GameMenu(GameMenu.inventoryTab);
                return;
            }

            if (hit == "util_map")
            {
                Game1.activeClickableMenu = new GameMenu(GameMenu.mapTab);
                return;
            }

            if (hit == "util_close")
            {
                var menu = Game1.activeClickableMenu;
                if (menu != null)
                    menu.exitThisMenu();
                else
                    Game1.activeClickableMenu = new GameMenu(GameMenu.optionsTab);
                return;
            }

            if (hit.StartsWith("slot_"))
            {
                if (int.TryParse(hit.Substring(5), out int slot) && slot >= 0 && slot < who.Items.Count)
                    who.CurrentToolIndex = slot;
                return;
            }

            if (hit == "L")
            {
                this.UseToolAt(this.AimTile());
                return;
            }

            if (hit == "R")
            {
                this.DoActionAt(this.AimTile());
            }
        }

        private Vector2 AimTile()
        {
            // tile under the slide cursor, or under the real cursor otherwise
            if (this.config.SlideMode && this.slideCursorSet)
            {
                int x = (int)(Game1.viewport.X + this.slideCursor.X) / Game1.tileSize;
                int y = (int)(Game1.viewport.Y + this.slideCursor.Y) / Game1.tileSize;
                return new Vector2(x, y);
            }
            return this.Helper.Input.GetCursorPosition().GrabTile;
        }

        private void UseToolAt(Vector2 tile)
        {
            Farmer who = Game1.player;
            GameLocation loc = Game1.currentLocation;
            if (who == null || loc == null)
                return;

            if (who.CurrentTool is Tool tool)
            {
                int x = (int)tile.X;
                int y = (int)tile.Y;
                int power = who.toolPower.Value;
                try { tool.DoFunction(loc, x * Game1.tileSize, y * Game1.tileSize, power, who); }
                catch (Exception ex) { this.Monitor.Log($"Tool use failed: {ex.Message}", LogLevel.Trace); }
            }
        }

        private void DoActionAt(Vector2 tile)
        {
            GameLocation loc = Game1.currentLocation;
            Farmer who = Game1.player;
            if (loc == null || who == null)
                return;

            try { loc.checkAction(new Location((int)tile.X, (int)tile.Y), Game1.viewport, who); }
            catch (Exception ex) { this.Monitor.Log($"Action failed: {ex.Message}", LogLevel.Trace); }
        }

        /********* layout *********/

        private Rectangle ToolbarBox(int slot)
        {
            var view = Game1.uiViewport;
            const int size = 52;
            const int gap = 6;
            const int count = 12;
            int total = count * (size + gap) - gap;
            int x = (view.Width - total) / 2 + slot * (size + gap);
            int y = view.Height - size - 16;
            return new Rectangle(x, y, size, size);
        }

        private Rectangle LeftButton => this.SideButton(0);
        private Rectangle RightButton => this.SideButton(1);
        private Rectangle ModeButton => this.SideButton(2);

        private Rectangle SideButton(int index)
        {
            var view = Game1.uiViewport;
            const int size = 64;
            const int gap = 10;
            // stack leftwards from the right edge, above the toolbar
            int x = view.Width - (index + 1) * (size + gap) - 12;
            int y = view.Height - 52 - 16 - gap - size;
            return new Rectangle(x, y, size, size);
        }

        private Rectangle UtilButton(int index)
        {
            const int size = 56;
            const int gap = 8;
            return new Rectangle(12 + index * (size + gap), 12, size, size);
        }

        private string HitTest(Vector2 screen)
        {
            if (this.config.ShowUtility)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (this.UtilButton(i).Contains((int)screen.X, (int)screen.Y))
                        return i == 0 ? "util_inventory" : i == 1 ? "util_map" : "util_close";
                }
            }

            if (this.config.ShowLeftRight)
            {
                if (this.LeftButton.Contains((int)screen.X, (int)screen.Y))
                    return "L";
                if (this.RightButton.Contains((int)screen.X, (int)screen.Y))
                    return "R";
            }

            if (this.config.ShowModeToggle)
            {
                if (this.ModeButton.Contains((int)screen.X, (int)screen.Y))
                    return "mode";
            }

            if (this.config.ShowToolbar)
            {
                for (int i = 0; i < 12; i++)
                {
                    if (this.ToolbarBox(i).Contains((int)screen.X, (int)screen.Y))
                        return $"slot_{i}";
                }
            }

            return null;
        }

        /********* drawing *********/

        private void OnRendered(object sender, RenderedEventArgs e)
        {
            if (this.config == null || !this.config.Enabled)
                return;
            if (!Context.IsWorldReady)
                return;
            if (Game1.activeClickableMenu != null)
                return;

            float alpha = MathHelper.Clamp(this.config.ButtonAlpha, 0.1f, 1f);
            var batch = e.SpriteBatch;

            if (this.config.ShowUtility)
            {
                this.DrawBox(batch, this.UtilButton(0), "E", alpha, "Bag");
                this.DrawBox(batch, this.UtilButton(1), "M", alpha, "Map");
                this.DrawBox(batch, this.UtilButton(2), "X", alpha, "Menu");
            }

            if (this.config.ShowLeftRight)
            {
                this.DrawBox(batch, this.LeftButton, "L", alpha, "Use");
                this.DrawBox(batch, this.RightButton, "R", alpha, "Do");
            }

            if (this.config.ShowModeToggle)
            {
                this.DrawBox(batch, this.ModeButton, this.config.SlideMode ? "S" : "T", alpha,
                    this.config.SlideMode ? "Slide" : "Touch");
            }

            if (this.config.ShowToolbar)
            {
                Farmer who = Game1.player;
                for (int i = 0; i < 12; i++)
                {
                    var box = this.ToolbarBox(i);
                    bool selected = who != null && who.CurrentToolIndex == i;
                    this.DrawBox(batch, box, this.SlotLabel(i), alpha, null, selected);

                    var item = who != null && i < who.Items.Count ? who.Items[i] : null;
                    if (item != null)
                    {
                        try
                        {
                            item.drawInMenu(batch, new Vector2(box.X + 6, box.Y + 10), 0.75f, 1f, 0.9f,
                                StackDrawType.Hide, Microsoft.Xna.Framework.Color.White, false);
                        }
                        catch { }
                    }
                }
            }

            if (this.config.SlideMode && this.slideCursorSet)
                this.DrawSlideCursor(batch, alpha);
        }

        private string SlotLabel(int slot)
        {
            if (slot < 9) return (slot + 1).ToString();
            if (slot == 9) return "0";
            if (slot == 10) return "-";
            return "=";
        }

        private void DrawBox(SpriteBatch batch, Rectangle box, string big, float alpha, string small, bool highlight = false)
        {
            var fill = Microsoft.Xna.Framework.Color.Black * alpha;
            var edge = (highlight ? Microsoft.Xna.Framework.Color.Gold : Microsoft.Xna.Framework.Color.White)
                * Math.Min(1f, alpha + 0.3f);

            batch.Draw(Game1.staminaRect, box, fill);

            int t = 2;
            batch.Draw(Game1.staminaRect, new Rectangle(box.X, box.Y, box.Width, t), edge);
            batch.Draw(Game1.staminaRect, new Rectangle(box.X, box.Bottom - t, box.Width, t), edge);
            batch.Draw(Game1.staminaRect, new Rectangle(box.X, box.Y, t, box.Height), edge);
            batch.Draw(Game1.staminaRect, new Rectangle(box.Right - t, box.Y, t, box.Height), edge);

            var textColor = Microsoft.Xna.Framework.Color.White * Math.Min(1f, alpha + 0.5f);
            var font = Game1.smallFont;

            Vector2 bigSize = font.MeasureString(big);
            batch.DrawString(font, big,
                new Vector2(box.Center.X - bigSize.X / 2, box.Y + 2), textColor);

            if (small != null)
            {
                Vector2 smallSize = font.MeasureString(small);
                float scale = 0.65f;
                batch.DrawString(font, small,
                    new Vector2(box.Center.X - smallSize.X * scale / 2, box.Bottom - smallSize.Y * scale - 2),
                    textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawSlideCursor(SpriteBatch batch, float alpha)
        {
            var c = new Vector2(this.slideCursor.X, this.slideCursor.Y);
            var col = Microsoft.Xna.Framework.Color.White * Math.Min(1f, alpha + 0.4f);
            int r = 18;
            int t = 2;

            batch.Draw(Game1.staminaRect, new Rectangle((int)c.X - r, (int)c.Y - t / 2, r * 2, t), col);
            batch.Draw(Game1.staminaRect, new Rectangle((int)c.X - t / 2, (int)c.Y - r, t, r * 2), col);
        }
    }
}
