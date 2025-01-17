namespace StardewMods.FauxCore;

using SimpleInjector;
using StardewMods.Common.Interfaces;
using StardewMods.Common.Services;
using StardewMods.Common.Services.Integrations.FauxCore;
using StardewMods.Common.Services.Integrations.GenericModConfigMenu;
using StardewMods.FauxCore.Framework;
using StardewMods.FauxCore.Framework.Interfaces;
using StardewMods.FauxCore.Framework.Services;

/// <inheritdoc />
public sealed class ModEntry : Mod
{
    private Container container = null!;

    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        // Init
        I18n.Init(this.Helper.Translation);
        this.container = new Container();

        // Configuration
        this.container.RegisterInstance(this.Helper);
        this.container.RegisterInstance(this.ModManifest);
        this.container.RegisterInstance(this.Monitor);
        this.container.RegisterInstance(this.Helper.Data);
        this.container.RegisterInstance(this.Helper.Events);
        this.container.RegisterInstance(this.Helper.GameContent);
        this.container.RegisterInstance(this.Helper.Input);
        this.container.RegisterInstance(this.Helper.ModContent);
        this.container.RegisterInstance(this.Helper.ModRegistry);
        this.container.RegisterInstance(this.Helper.Reflection);
        this.container.RegisterInstance(this.Helper.Translation);
        this.container.RegisterInstance<Func<IModConfig>>(this.GetConfig);
        this.container.RegisterSingleton<IModConfig, ConfigManager>();
        this.container.RegisterSingleton<ConfigManager, ConfigManager>();
        this.container.RegisterSingleton<IEventManager, EventManager>();
        this.container.RegisterSingleton<IEventPublisher, EventManager>();
        this.container.RegisterSingleton<IEventSubscriber, EventManager>();
        this.container.RegisterSingleton<GenericModConfigMenuIntegration>();
        this.container.RegisterSingleton<ILog, Log>();
        this.container.RegisterSingleton<IThemeHelper, ThemeHelper>();

        // Verify
        this.container.Verify();
    }

    /// <inheritdoc />
    public override object GetApi(IModInfo mod) =>
        new FauxCoreApi(
            mod,
            this.container.GetInstance<Func<IModConfig>>(),
            this.container.GetInstance<IThemeHelper>());

    private IModConfig GetConfig() => this.container.GetInstance<IModConfig>();
}