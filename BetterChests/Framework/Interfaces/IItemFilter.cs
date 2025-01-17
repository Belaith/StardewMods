namespace StardewMods.BetterChests.Framework.Interfaces;

/// <summary>Represents an interface for filtering items.</summary>
internal interface IItemFilter
{
    /// <summary>Determines whether the given item matches any filter conditions.</summary>
    /// <param name="item">The item to be checked.</param>
    /// <returns>true if the item matches; otherwise, false.</returns>
    public bool MatchesFilter(Item item);
}