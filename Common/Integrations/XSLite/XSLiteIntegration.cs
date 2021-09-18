﻿namespace Common.Integrations.XSLite
{
    using StardewModdingAPI;

    /// <inheritdoc />
    internal class XSLiteIntegration : ModIntegration<IXSLiteAPI>
    {
        /// <summary>Initializes a new instance of the <see cref="XSLiteIntegration"/> class.</summary>
        /// <param name="modRegistry">SMAPI's mod registry.</param>
        public XSLiteIntegration(IModRegistry modRegistry)
            : base(modRegistry, "furyx639.ExpandedStorage")
        {
        }
    }
}