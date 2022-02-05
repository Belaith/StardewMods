﻿namespace StardewMods.BetterChests.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using Common.Helpers;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewMods.BetterChests.Interfaces;
using StardewMods.BetterChests.Models;
using StardewMods.FuryCore.Interfaces;

/// <inheritdoc cref="StardewMods.FuryCore.Interfaces.IModService" />
internal class AssetHandler : IModService, IAssetLoader
{
    private const string CraftablesData = "Data/BigCraftablesInformation";

    private IReadOnlyDictionary<string, IChestData> _cachedChestData;
    private IReadOnlyDictionary<int, string[]> _cachedCraftables;
    private IReadOnlyDictionary<string, string[]> _cachedTabData;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AssetHandler" /> class.
    /// </summary>
    /// <param name="config">The <see cref="IConfigData" /> for options set by the player.</param>
    /// <param name="helper">SMAPI helper for events, input, and content.</param>
    public AssetHandler(IConfigModel config, IModHelper helper)
    {
        this.Config = config;
        this.Helper = helper;
        this.Helper.Content.AssetLoaders.Add(this);

        this.InitChestData();
        this.InitTabData();

        this.Helper.Events.GameLoop.DayEnding += this.OnDayEnding;
        this.Helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
        if (Context.IsMainPlayer)
        {
            this.Helper.Events.Multiplayer.PeerConnected += this.OnPeerConnected;
        }
    }

    /// <summary>
    ///     Gets the collection of chest data for all known chest types in the game.
    /// </summary>
    public IReadOnlyDictionary<string, IChestData> ChestData
    {
        get => this._cachedChestData ??= (
                from data in this.Helper.Content.Load<IDictionary<string, IDictionary<string, string>>>($"{BetterChests.ModUniqueId}/Chests", ContentSource.GameContent)
                select (data.Key, Value: new SerializedChestData(data.Value)))
            .ToDictionary(data => data.Key, data => (IChestData)data.Value);
    }

    /// <summary>
    ///     Gets the game data for Big Craftables.
    /// </summary>
    public IReadOnlyDictionary<int, string[]> Craftables
    {
        get => this._cachedCraftables ??= this.Helper.Content.Load<IDictionary<int, string>>(AssetHandler.CraftablesData, ContentSource.GameContent)
                                              .ToDictionary(
                                                  info => info.Key,
                                                  info => info.Value.Split('/'));
    }

    /// <summary>
    ///     Gets the collection of tab data.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> TabData
    {
        get => this._cachedTabData ??= (
                from tab in
                    from data in this.Helper.Content.Load<IDictionary<string, string>>($"{BetterChests.ModUniqueId}/Tabs", ContentSource.GameContent)
                    select (data.Key, info: data.Value.Split('/'))
                orderby int.Parse(tab.info[2]), tab.info[0]
                select (tab.Key, tab.info))
            .ToDictionary(
                data => data.Key,
                data => data.info);
    }

    private IConfigModel Config { get; }

    private IModHelper Helper { get; }

    private IDictionary<string, IDictionary<string, string>> LocalChestData { get; set; }

    private IDictionary<string, string> LocalTabData { get; set; }

    /// <summary>
    ///     Adds new Chest Data and saves to assets/chests.json.
    /// </summary>
    /// <param name="id">The qualified item id of the chest.</param>
    /// <param name="data">The chest data to add.</param>
    /// <returns>True if new chest data was added.</returns>
    public bool AddChestData(string id, IChestData data = default)
    {
        if (this.Craftables.All(info => info.Value[0] != id))
        {
            return false;
        }

        data ??= new ChestData();
        if (this.LocalChestData.ContainsKey(id))
        {
            return false;
        }

        this.LocalChestData.Add(id, SerializedChestData.GetData(data));
        return true;
    }

    /// <inheritdoc />
    public bool CanLoad<T>(IAssetInfo asset)
    {
        return asset.AssetNameEquals($"{BetterChests.ModUniqueId}/Chests")
               || asset.AssetNameEquals($"{BetterChests.ModUniqueId}/Tabs")
               || asset.AssetNameEquals($"{BetterChests.ModUniqueId}/Tabs/Texture");
    }

    /// <inheritdoc />
    public T Load<T>(IAssetInfo asset)
    {
        var segment = PathUtilities.GetSegments(asset.AssetName);
        return segment[1] switch
        {
            "Chests" when segment.Length == 2
                => (T)this.LocalChestData,
            "Tabs" when segment.Length == 3 && segment[2] == "Texture"
                => (T)(object)this.Helper.Content.Load<Texture2D>("assets/tabs.png"),
            "Tabs" when segment.Length == 2
                => (T)(object)this.LocalTabData.ToDictionary(
                    data => data.Key,
                    data =>
                    {
                        var (key, value) = data;
                        var info = value.Split('/');
                        info[0] = this.Helper.Translation.Get($"tabs.{key}.name");
                        return string.Join('/', info);
                    }),
            _ => default,
        };
    }

    /// <summary>
    ///     Saves the currently cached chest data back to the local chest data.
    /// </summary>
    public void SaveChestData()
    {
        foreach (var (key, data) in this._cachedChestData)
        {
            this.LocalChestData[key] = SerializedChestData.GetData(data);
        }

        this.Helper.Data.WriteJsonFile("assets/chests.json", this.LocalChestData);
        this.Helper.Multiplayer.SendMessage(this.LocalChestData, "ChestData", new[] { BetterChests.ModUniqueId });
    }

    private void InitChestData()
    {
        // Load Chest Data
        try
        {
            this.LocalChestData = this.Helper.Data.ReadJsonFile<IDictionary<string, IDictionary<string, string>>>("assets/chests.json");
        }
        catch (Exception)
        {
            // ignored
        }

        // Initialize Chest Data
        if (this.LocalChestData is null)
        {
            this.LocalChestData = new Dictionary<string, IDictionary<string, string>>
            {
                { "Chest", SerializedChestData.GetData(new ChestData()) },
                { "Stone Chest", SerializedChestData.GetData(new ChestData()) },
                { "Junimo Chest", SerializedChestData.GetData(new ChestData()) },
                { "Mini-Fridge", SerializedChestData.GetData(new ChestData()) },
                { "Mini-Shipping Bin", SerializedChestData.GetData(new ChestData()) },
                { "Fridge", SerializedChestData.GetData(new ChestData()) },
                { "Auto-Grabber", SerializedChestData.GetData(new ChestData()) },
            };
            this.Helper.Data.WriteJsonFile("assets/chests.json", this.LocalChestData);
        }
    }

    private void InitTabData()
    {
        // Load Tab Data
        try
        {
            this.LocalTabData = this.Helper.Content.Load<Dictionary<string, string>>("assets/tabs.json");
        }
        catch (Exception)
        {
            // ignored
        }

        // Initialize Tab Data
        if (this.LocalTabData is null)
        {
            this.LocalTabData = new Dictionary<string, string>
            {
                {
                    "Clothing",
                    "Clothing/furyx639.BetterChests\\Tabs\\Texture/0/category_clothing category_boots category_hat"
                },
                {
                    "Cooking",
                    "Cooking/furyx639.BetterChests\\Tabs\\Texture/1/category_syrup category_artisan_goods category_ingredients category_sell_at_pierres_and_marnies category_sell_at_pierres category_meat category_cooking category_milk category_egg"
                },
                {
                    "Crops",
                    "Crops/furyx639.BetterChests\\Tabs\\Texture/2/category_greens category_flowers category_fruits category_vegetable"
                },
                {
                    "Equipment",
                    "/furyx639.BetterChests\\Tabs\\Texture/3/category_equipment category_ring category_tool category_weapon"
                },
                {
                    "Fishing",
                    "/furyx639.BetterChests\\Tabs\\Texture/4/category_bait category_fish category_tackle category_sell_at_fish_shop"
                },
                {
                    "Materials",
                    "/furyx639.BetterChests\\Tabs\\Texture/5/category_monster_loot category_metal_resources category_building_resources category_minerals category_crafting category_gem"
                },
                {
                    "Misc",
                    "/furyx639.BetterChests\\Tabs\\Texture/6/category_big_craftable category_furniture category_junk"
                },
                {
                    "Seeds",
                    "/furyx639.BetterChests\\Tabs\\Texture/7/category_seeds category_fertilizer"
                },
            };
            this.Helper.Data.WriteJsonFile("assets/tabs.json", this.LocalTabData);
        }
    }

    private void OnDayEnding(object sender, DayEndingEventArgs e)
    {
        this._cachedCraftables = null;
        this._cachedChestData = null;
        this._cachedTabData = null;
    }

    private void OnModMessageReceived(object sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != BetterChests.ModUniqueId)
        {
            return;
        }

        switch (e.Type)
        {
            case "ChestData":
                Log.Trace("Loading ChestData from Host");
                this.LocalChestData = e.ReadAs<IDictionary<string, IDictionary<string, string>>>();
                break;
            case "DefaultChest":
                Log.Trace("Loading DefaultChest Config from Host");
                var chestData = e.ReadAs<ChestData>();
                ((IChestData)chestData).CopyTo(this.Config.DefaultChest);
                break;
        }
    }

    private void OnPeerConnected(object sender, PeerConnectedEventArgs e)
    {
        if (e.Peer.IsHost)
        {
            return;
        }

        this.Helper.Multiplayer.SendMessage(this.LocalChestData, "ChestData", new[] { BetterChests.ModUniqueId });
        this.Helper.Multiplayer.SendMessage(this.Config.DefaultChest, "DefaultChest", new[] { BetterChests.ModUniqueId });
    }
}