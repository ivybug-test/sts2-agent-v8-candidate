using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace STS2AIAgent.Game;

internal static partial class GameStateService
{
    public static NMainMenuTextButton? GetMainMenuCompendiumButton(NMainMenu mainMenu)
    {
        return mainMenu.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/CompendiumButton")
            ?? FindDescendants<NMainMenuTextButton>(mainMenu).FirstOrDefault(button =>
                button.Name.ToString().Contains("Compendium", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Read the active compendium page and its filtered card catalog.</summary>
    private static object? BuildCompendiumPayload(IScreenContext? currentScreen)
    {
        var page = GetCompendiumPage(currentScreen);
        if (page == null)
        {
            return null;
        }

        var buttons = GetCompendiumButtons(currentScreen)
            .Select((button, index) => new
            {
                index,
                node_name = button.Name.ToString(),
                label = GetButtonLabel(button) ?? string.Empty
            }).ToArray();

        var cards = new List<SelectionCardPayload>();
        var catalogErrors = new List<object>();
        if (page is NCardLibrary)
        {
            foreach (var card in GetVisibleCompendiumCards(page))
            {
                try
                {
                    cards.Add(BuildSelectionCardPayload(card, cards.Count, false));
                }
                catch (Exception error)
                {
                    catalogErrors.Add(new { card_id = card.Id.ToString(), variant = "window", error = error.GetType().Name });
                }
            }
        }

        var catalog = page is NCardLibrary
            ? FindDescendants<NCardLibraryGrid>(page).FirstOrDefault()?.VisibleCards.ToArray()
                ?? Array.Empty<CardModel>()
            : Array.Empty<CardModel>();
        var catalogCards = new List<SelectionCardPayload>();
        foreach (var card in catalog.Take(1000))
        {
            try
            {
                catalogCards.Add(BuildSelectionCardPayload(card, catalogCards.Count, false));
            }
            catch (Exception error)
            {
                catalogErrors.Add(new { card_id = card.Id.ToString(), variant = "base", error = error.GetType().Name });
            }
            try
            {
                if (card.IsUpgradable)
                {
                    var upgraded = card.ToMutable();
                    upgraded.UpgradeInternal();
                    catalogCards.Add(BuildSelectionCardPayload(upgraded, catalogCards.Count, false));
                }
            }
            catch (Exception error)
            {
                catalogErrors.Add(new { card_id = card.Id.ToString(), variant = "upgrade", error = error.GetType().Name });
            }
        }

        object[] scrolls = page is NCardLibrary
            ? GetCompendiumScrollbars(page)
                .Select((scroll, index) => (object)new
                {
                    index,
                    node_name = scroll.Name.ToString(),
                    value = scroll.Value,
                    min_value = scroll.MinValue,
                    max_value = scroll.MaxValue,
                    page = scroll.Page
                }).ToArray()
            : Array.Empty<object>();

        return new
        {
            page = page is NCardLibrary ? "CARD_LIBRARY" : "COMPENDIUM",
            buttons,
            cards,
            catalog_count = catalog.Length,
            catalog_truncated = catalog.Length > 1000,
            catalog_cards = catalogCards,
            catalog_errors = catalogErrors,
            scrolls
        };
    }

    public static Node? GetCompendiumPage(IScreenContext? currentScreen)
    {
        if (currentScreen is NCardLibrary or NCompendiumSubmenu)
        {
            return (Node)currentScreen;
        }
        if (currentScreen is NCapstoneSubmenuStack container &&
            container.Stack?.Peek() is NCardLibrary or NCompendiumSubmenu)
        {
            return container.Stack.Peek();
        }
        return null;
    }

    public static IReadOnlyList<NButton> GetCompendiumButtons(IScreenContext? currentScreen)
    {
        var page = GetCompendiumPage(currentScreen);
        return page == null ? Array.Empty<NButton>() : FindDescendants<NButton>(page)
            .Where(button => GodotObject.IsInstanceValid(button) && button.IsVisibleInTree() && button.IsEnabled)
            .Take(100)
            .ToArray();
    }

    public static IReadOnlyList<NScrollbar> GetCompendiumScrollbars(Node page)
    {
        return FindDescendants<NScrollbar>(page)
            .Where(GodotObject.IsInstanceValid)
            .ToArray();
    }

    public static IReadOnlyList<CardModel> GetVisibleCompendiumCards(Node page)
    {
        var cards = new List<CardModel>();
        var seen = new HashSet<CardModel>();
        foreach (var node in FindDescendants<Node>(page))
        {
            if (node is not CanvasItem item || !item.IsVisibleInTree())
            {
                continue;
            }
            CardModel? card = node is NCardHolder holder ? holder.CardModel : null;
            if (card == null && node.GetType().Name == "NCard")
            {
                card = node.GetType().GetProperty("Model")?.GetValue(node) as CardModel;
            }
            if (card != null && seen.Add(card))
            {
                cards.Add(card);
            }
            if (cards.Count >= 500)
            {
                break;
            }
        }
        return cards;
    }
}
