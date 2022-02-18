﻿namespace StardewMods.FuryCore.Events;

using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI.Utilities;
using StardewMods.FuryCore.Enums;
using StardewMods.FuryCore.Interfaces;
using StardewMods.FuryCore.Interfaces.ClickableComponents;
using StardewMods.FuryCore.Interfaces.CustomEvents;
using StardewMods.FuryCore.Models.ClickableComponents;
using StardewMods.FuryCore.Models.CustomEvents;
using StardewMods.FuryCore.Services;
using StardewValley.Menus;

/// <inheritdoc />
internal class MenuComponentsLoading : SortedEventHandler<MenuComponentsLoadingEventArgs>
{
    private readonly PerScreen<IClickableMenu> _menu = new();
    private readonly Lazy<MenuComponents> _menuComponents;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MenuComponentsLoading" /> class.
    /// </summary>
    /// <param name="services">Provides access to internal and external services.</param>
    public MenuComponentsLoading(IModServices services)
    {
        this._menuComponents = services.Lazy<MenuComponents>();
        services.Lazy<ICustomEvents>(
            customEvents => { customEvents.ClickableMenuChanged += this.OnClickableMenuChanged; });
    }

    private IClickableMenu Menu
    {
        get => this._menu.Value;
        set => this._menu.Value = value;
    }

    private MenuComponents MenuComponents
    {
        get => this._menuComponents.Value;
    }

    private void OnClickableMenuChanged(object sender, ClickableMenuChangedEventArgs e)
    {
        if (ReferenceEquals(this.Menu, e.Menu))
        {
            return;
        }

        this.Menu = e.Menu;
        this.MenuComponents.Components.Clear();
        if (this.Menu is null || this.HandlerCount == 0)
        {
            return;
        }

        var vanillaComponents = (
            from componentType in Enum.GetValues(typeof(ComponentType)).Cast<ComponentType>()
            where componentType is not ComponentType.Custom
            select new VanillaClickableComponent(this.Menu, componentType)
            into component
            where component.Component is not null
            orderby component.Component.bounds.X, component.Component.bounds.Y
            select component).ToList();
        var components = new List<IClickableComponent>();
        components.AddRange(vanillaComponents);
        this.InvokeAll(new(e.Menu, components));
        this.MenuComponents.Components.AddRange(components);

        foreach (var component in components)
        {
            if (!this.Menu.allClickableComponents.Contains(component.Component))
            {
                this.Menu.allClickableComponents.Add(component.Component);
            }
        }
    }
}