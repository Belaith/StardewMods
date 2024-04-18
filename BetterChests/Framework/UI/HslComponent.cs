namespace StardewMods.BetterChests.Framework.UI;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Utilities;
using StardewMods.BetterChests.Framework.Interfaces;
using StardewMods.BetterChests.Framework.Services;
using StardewMods.Common.Models;
using StardewValley.Menus;
using StardewValley.Objects;

internal sealed class HslComponent
{
    private static readonly PerScreen<HslColor> SavedColor = new(() => HslComponent.Transparent);
    private static readonly HslColor Transparent = new(0, 0, 0);

    private readonly Chest chest;
    private readonly ClickableComponent chestComponent;
    private readonly DiscreteColorPicker colorPicker;
    private readonly Rectangle copyArea;
    private readonly ClickableTextureComponent copyComponent;
    private readonly IReflectedField<int> currentLidFrame;
    private readonly Rectangle defaultColorArea;
    private readonly ClickableTextureComponent defaultColorComponent;
    private readonly Func<Color> getColor;
    private readonly Slider hue;
    private readonly IInputHelper inputHelper;
    private readonly ItemGrabMenuManager itemGrabMenuManager;
    private readonly Slider lightness;
    private readonly Slider saturation;
    private readonly Action<Color> setColor;
    private readonly int xPosition;
    private readonly int yPosition;

    private Slider? holding;
    private bool hoverChest;
    private int lidFrameCount;

    /// <summary>Initializes a new instance of the <see cref="HslComponent" /> class.</summary>
    /// <param name="assetHandler">Dependency used for handling assets.</param>
    /// <param name="colorPicker">The vanilla color picker component.</param>
    /// <param name="inputHelper">Dependency used for checking and changing input state.</param>
    /// <param name="itemGrabMenuManager">Dependency used for managing the item grab menu.</param>
    /// <param name="reflectionHelper">Dependency used for reflecting into external code.</param>
    /// <param name="modConfig">Dependency used for accessing config data.</param>
    /// <param name="getColor">Get method for the current color.</param>
    /// <param name="setColor">Set method for the current color.</param>
    /// <param name="xPosition">The x-coordinate of the component.</param>
    /// <param name="yPosition">The y-coordinate of the component.</param>
    public HslComponent(
        AssetHandler assetHandler,
        DiscreteColorPicker colorPicker,
        IInputHelper inputHelper,
        ItemGrabMenuManager itemGrabMenuManager,
        IReflectionHelper reflectionHelper,
        IModConfig modConfig,
        Func<Color> getColor,
        Action<Color> setColor,
        int xPosition,
        int yPosition)
    {
        this.colorPicker = colorPicker;
        this.inputHelper = inputHelper;
        this.itemGrabMenuManager = itemGrabMenuManager;
        this.getColor = getColor;
        this.setColor = setColor;
        this.xPosition = xPosition;
        this.yPosition = yPosition;
        this.chest = (Chest)colorPicker.itemToDrawColored!;
        this.currentLidFrame = reflectionHelper.GetField<int>(this.chest, "currentLidFrame");
        this.currentLidFrame.SetValue(this.chest.startingLidFrame.Value);

        var playerChoiceColor = getColor();
        this.CurrentColor = HslComponent.Transparent;
        if (!playerChoiceColor.Equals(Color.Black))
        {
            this.CurrentColor = HslColor.FromColor(playerChoiceColor);
            colorPicker.colorSelection =
                (playerChoiceColor.R << 0) | (playerChoiceColor.G << 8) | (playerChoiceColor.B << 16);

            this.chest.playerChoiceColor.Value = playerChoiceColor;
        }

        this.chestComponent = new ClickableComponent(
            new Rectangle(
                xPosition,
                yPosition - Game1.tileSize - (IClickableMenu.borderWidth / 2),
                Game1.tileSize,
                Game1.tileSize),
            this.chest)
        {
            myID = (int)Math.Pow(yPosition - Game1.tileSize - (IClickableMenu.borderWidth / 2f), 2) + xPosition,
        };

        this.copyArea = new Rectangle(xPosition + 30, yPosition - 4, 36, 36);
        this.copyComponent = new ClickableTextureComponent(
            new Rectangle(xPosition + 34, yPosition + 2, 8, 8),
            assetHandler.Icons.Value,
            new Rectangle(116, 4, 8, 8),
            3)
        {
            myID = (int)Math.Pow(yPosition + 2, 2) + xPosition + 34,
        };

        this.defaultColorArea = new Rectangle(xPosition - 6, yPosition - 4, 36, 36);
        this.defaultColorComponent = new ClickableTextureComponent(
            new Rectangle(xPosition - 2, yPosition, 7, 7),
            Game1.mouseCursors,
            new Rectangle(295, 503, 7, 7),
            Game1.pixelZoom)
        {
            myID = (int)Math.Pow(yPosition, 2) + xPosition,
        };

        this.hue = new Slider(
            assetHandler.HslTexture,
            () => this.CurrentColor.H,
            value =>
            {
                this.CurrentColor = this.colorPicker.colorSelection == 0
                    ? assetHandler.HslColors[(int)(value * assetHandler.HslColors.Length)]
                    : this.CurrentColor with { H = value };

                this.UpdateColor();
            },
            new Rectangle(xPosition, yPosition + 36, 23, 522),
            modConfig.HslColorPickerHueSteps);

        this.saturation = new Slider(
            value => (this.CurrentColor with
            {
                S = this.colorPicker.colorSelection == 0 ? 0 : value,
                L = Math.Max(0.01f, this.colorPicker.colorSelection == 0 ? value : this.CurrentColor.L),
            }).ToRgbColor(),
            () => this.CurrentColor.S,
            value =>
            {
                this.CurrentColor = this.colorPicker.colorSelection == 0
                    ? new HslColor(0, value, 0.5f)
                    : this.CurrentColor with
                    {
                        S = value,
                        L = Math.Max(0.01f, this.CurrentColor.L),
                    };

                this.UpdateColor();
            },
            new Rectangle(xPosition + 32, yPosition + 300, 23, 256),
            modConfig.HslColorPickerSaturationSteps);

        this.lightness = new Slider(
            value => (this.CurrentColor with
            {
                S = this.colorPicker.colorSelection == 0 ? 0 : this.CurrentColor.S,
                L = value,
            }).ToRgbColor(),
            () => this.CurrentColor.L,
            value =>
            {
                this.CurrentColor = this.CurrentColor with { L = value };
                this.UpdateColor();
            },
            new Rectangle(xPosition + 32, yPosition + 36, 23, 256),
            modConfig.HslColorPickerLightnessSteps);

        if (itemGrabMenuManager.CurrentMenu is null)
        {
            return;
        }

        this.SetupBorderNeighbors();
        itemGrabMenuManager.CurrentMenu.allClickableComponents.Add(this.chestComponent);
        itemGrabMenuManager.CurrentMenu.allClickableComponents.Add(this.copyComponent);
        itemGrabMenuManager.CurrentMenu.allClickableComponents.Add(this.defaultColorComponent);
        itemGrabMenuManager.CurrentMenu.allClickableComponents.AddRange(this.hue.Bars);
        itemGrabMenuManager.CurrentMenu.allClickableComponents.AddRange(this.lightness.Bars);
        itemGrabMenuManager.CurrentMenu.allClickableComponents.AddRange(this.saturation.Bars);
    }

    /// <summary>Gets the current hsl color.</summary>
    public HslColor CurrentColor { get; private set; }

    /// <summary>Sets up the border neighbors for the color picker.</summary>
    public void SetupBorderNeighbors()
    {
        if (this.itemGrabMenuManager.CurrentMenu is not
            { } menu)
        {
            return;
        }

        var buttons =
            menu
                .allClickableComponents.Where(
                    c => c.bounds.Left == menu.colorPickerToggleButton.bounds.Left || c == menu.trashCan)
                .ToList();

        if (!Game1.player.showChestColorPicker)
        {
            foreach (var button in buttons)
            {
                button.rightNeighborID = -1;
            }

            return;
        }

        // Set left-right neighbors
        this.defaultColorComponent.rightNeighborID = this.copyComponent.myID;
        this.copyComponent.leftNeighborID = this.defaultColorComponent.myID;

        // Set up-down neighbors
        this.chestComponent.downNeighborID = this.defaultColorComponent.myID;
        this.defaultColorComponent.upNeighborID = this.chestComponent.myID;
        this.copyComponent.upNeighborID = this.chestComponent.myID;

        this.defaultColorComponent.downNeighborID = this.hue.Bars[0].myID;
        this.hue.Bars[0].upNeighborID = this.defaultColorComponent.myID;

        this.copyComponent.downNeighborID = this.lightness.Bars[0].myID;
        this.lightness.Bars[0].upNeighborID = this.copyComponent.myID;

        this.lightness.Bars[^1].downNeighborID = this.saturation.Bars[0].myID;
        this.saturation.Bars[0].upNeighborID = this.lightness.Bars[^1].myID;

        var neighborComponents = new List<ClickableComponent>
        {
            this.chestComponent,
            this.defaultColorComponent,
        };

        neighborComponents.AddRange(this.hue.Bars);

        // Assign buttons to neighbors
        foreach (var button in buttons)
        {
            var neighborComponent = neighborComponents.MinBy(c => Math.Abs(c.bounds.Center.Y - button.bounds.Center.Y));
            button.rightNeighborID = neighborComponent?.myID ?? -1;
        }

        // Assign neighbors to buttons
        foreach (var neighborComponent in neighborComponents)
        {
            var button = buttons.MinBy(c => Math.Abs(c.bounds.Center.Y - neighborComponent.bounds.Center.Y));
            neighborComponent.leftNeighborID = button?.myID ?? -1;
        }

        neighborComponents.Clear();
        neighborComponents.AddRange(this.saturation.Bars);
        neighborComponents.AddRange(this.lightness.Bars);

        // Assign hue components to saturation and lightness
        foreach (var hueBar in this.hue.Bars)
        {
            var neighborComponent = neighborComponents.MinBy(c => Math.Abs(c.bounds.Center.Y - hueBar.bounds.Center.Y));
            hueBar.rightNeighborID = neighborComponent?.myID ?? -1;
        }

        // Assign saturation components to hue
        foreach (var neighborComponent in neighborComponents)
        {
            var hueBar = this.hue.Bars.MinBy(c => Math.Abs(c.bounds.Center.Y - neighborComponent.bounds.Center.Y));
            neighborComponent.leftNeighborID = hueBar?.myID ?? -1;
        }
    }

    /// <summary>Performs a left-click action based on the given mouse coordinates.</summary>
    /// <param name="mouseX">The x-coordinate of the mouse.</param>
    /// <param name="mouseY">The y-coordinate of the mouse.</param>
    /// <returns>true if the left-click action was successfully performed; otherwise, false.</returns>
    public bool LeftClick(int mouseX, int mouseY)
    {
        if (this.holding is not null)
        {
            return false;
        }

        if (this.defaultColorArea.Contains(mouseX, mouseY))
        {
            this.CurrentColor = HslComponent.Transparent;
            this.colorPicker.colorSelection = 0;
            this.UpdateColor();
            return true;
        }

        if (this.copyArea.Contains(mouseX, mouseY))
        {
            HslComponent.SavedColor.Value = this.CurrentColor;
            return true;
        }

        if (this.hue.LeftClick(mouseX, mouseY))
        {
            this.holding = this.hue;
            return true;
        }

        if (this.saturation.LeftClick(mouseX, mouseY))
        {
            this.holding = this.saturation;
            return true;
        }

        if (this.lightness.LeftClick(mouseX, mouseY))
        {
            this.holding = this.lightness;
            return true;
        }

        return false;
    }

    /// <summary>Performs a right-click action based on the given mouse coordinates.</summary>
    /// <param name="mouseX">The x-coordinate of the mouse.</param>
    /// <param name="mouseY">The y-coordinate of the mouse.</param>
    /// <returns>true if the right-click action was successfully performed; otherwise, false.</returns>
    public bool RightClick(int mouseX, int mouseY)
    {
        if (this.holding is not null)
        {
            return false;
        }

        if (this.copyArea.Contains(mouseX, mouseY))
        {
            this.CurrentColor = HslComponent.SavedColor.Value;
            this.UpdateColor();
            return true;
        }

        return false;
    }

    /// <summary>Draw the color picker.</summary>
    /// <param name="spriteBatch">The sprite batch used for drawing.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        // Get Input states
        var (mouseX, mouseY) = Game1.getMousePosition(true);

        // Update components
        this.hue.Update(mouseX, mouseY);
        this.saturation.Update(mouseX, mouseY);
        this.lightness.Update(mouseX, mouseY);

        // Background
        IClickableMenu.drawTextureBox(
            spriteBatch,
            this.xPosition - (IClickableMenu.borderWidth / 2),
            this.yPosition - (IClickableMenu.borderWidth / 2),
            58 + IClickableMenu.borderWidth,
            558 + IClickableMenu.borderWidth,
            Color.LightGray);

        // Default color component
        this.defaultColorComponent.draw(spriteBatch);

        // Copy component
        this.copyComponent.draw(spriteBatch);

        // Hue slider
        this.hue.Draw(spriteBatch);

        // Saturation slider
        this.saturation.Draw(spriteBatch);

        // Lightness slider
        this.lightness.Draw(spriteBatch);

        // Default color selected
        if (this.colorPicker.colorSelection == 0)
        {
            IClickableMenu.drawTextureBox(
                spriteBatch,
                Game1.mouseCursors,
                new Rectangle(375, 357, 3, 3),
                this.defaultColorArea.X,
                this.defaultColorArea.Y,
                36,
                36,
                Color.Black,
                Game1.pixelZoom,
                false);
        }

        // Chest
        var hovering = this.chestComponent.containsPoint(mouseX, mouseY);
        if (hovering != this.hoverChest)
        {
            this.lidFrameCount = 0;
            this.hoverChest = hovering;
        }
        else if (++this.lidFrameCount < 5)
        {
            // Do nothing
        }
        else if (this.hoverChest)
        {
            this.lidFrameCount = 0;
            var nextFrame = Math.Min(this.chest.getLastLidFrame(), this.currentLidFrame.GetValue() + 1);
            this.currentLidFrame.SetValue(nextFrame);
        }
        else
        {
            this.lidFrameCount = 0;
            var nextFrame = Math.Max(this.chest.startingLidFrame.Value, this.currentLidFrame.GetValue() - 1);
            this.currentLidFrame.SetValue(nextFrame);
        }

        this.chest.draw(spriteBatch, this.chestComponent.bounds.X, this.chestComponent.bounds.Y, local: true);

        var isDown = this.inputHelper.IsDown(SButton.MouseLeft) || this.inputHelper.IsSuppressed(SButton.MouseLeft);
        if (!isDown)
        {
            if (this.holding is not null)
            {
                this.holding.Holding = false;
                this.holding = null;
            }
        }
    }

    private void UpdateColor()
    {
        var c = this.CurrentColor.ToRgbColor();
        this.setColor(c);
        this.colorPicker.colorSelection = (c.R << 0) | (c.G << 8) | (c.B << 16);
        this.chest.playerChoiceColor.Value = this.colorPicker.colorSelection == 0 ? Color.Black : c;
    }
}