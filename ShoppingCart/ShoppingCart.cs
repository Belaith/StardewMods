﻿namespace StardewMods.ShoppingCart;

using System;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewMods.Common.Helpers;
using StardewMods.ShoppingCart.ShopHandlers;
using StardewValley.Menus;

/// <inheritdoc />
public class ShoppingCart : Mod
{
    private readonly PerScreen<VirtualShop?> _currentShop = new();

    private ModConfig? _config;
    private bool _showMenuBackground;

    private ModConfig Config
    {
        get
        {
            if (this._config is not null)
            {
                return this._config;
            }

            ModConfig? config = null;
            try
            {
                config = this.Helper.ReadConfig<ModConfig>();
            }
            catch (Exception)
            {
                // ignored
            }

            this._config = config ?? new ModConfig();
            Log.Trace(this._config.ToString());
            return this._config;
        }
    }

    private VirtualShop? CurrentShop
    {
        get => this._currentShop.Value;
        set => this._currentShop.Value = value;
    }

    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        I18n.Init(this.Helper.Translation);
        Log.Monitor = this.Monitor;

        // Events
        this.Helper.Events.Display.MenuChanged += this.OnMenuChanged;
        this.Helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;
        this.Helper.Events.Display.RenderingActiveMenu += this.OnRenderingActiveMenu;
        this.Helper.Events.Input.ButtonPressed += this.OnButtonPressed;
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (Game1.activeClickableMenu is not ShopMenu
         || this.CurrentShop is null
         || (!e.Button.IsActionButton() && e.Button is not (SButton.MouseLeft or SButton.MouseRight)))
        {
            return;
        }

        var (x, y) = Game1.getMousePosition(true);
        if (this.CurrentShop.LeftClick(x, y))
        {
            this.Helper.Input.Suppress(e.Button);
        }
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (e.NewMenu is ShopMenu newMenu)
        {
            // Create new virtual shop
            var newShop = new VirtualShop(this.Helper, this.Config, newMenu);

            // Migrate shopping cart
            this.CurrentShop?.MoveItems(newShop);
            this.CurrentShop = newShop;
            return;
        }

        if (e.OldMenu is not ShopMenu)
        {
            return;
        }

        // Clean-up old menu
        this.CurrentShop?.ReturnItems();
        this.CurrentShop = null;
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        if (Game1.activeClickableMenu is not ShopMenu)
        {
            return;
        }

        Game1.options.showMenuBackground = this._showMenuBackground;
    }

    private void OnRenderingActiveMenu(object? sender, RenderingActiveMenuEventArgs e)
    {
        if (Game1.activeClickableMenu is not ShopMenu)
        {
            return;
        }

        this.CurrentShop?.Draw(e.SpriteBatch);
        this._showMenuBackground = Game1.options.showMenuBackground;
        Game1.options.showMenuBackground = true;
    }
}