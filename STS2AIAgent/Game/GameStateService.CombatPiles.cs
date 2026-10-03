using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2AIAgent.Game;

internal static partial class GameStateService
{
    private static CombatPileCardPayload[] BuildCombatPilePayload(
        CombatState combatState, IEnumerable<CardModel> cards)
    {
        // A pile's membership is visible; sorting does not reveal its internal draw order.
        return cards.Select(card => new CombatPileCardPayload
        {
            card_id = card.Id.Entry,
            upgraded = card.IsUpgraded,
            is_dupe = card.IsDupe,
            is_upgradable = card.IsUpgradable,
            is_unplayable = card.Keywords.Contains(CardKeyword.Unplayable),
            exhaust_on_play = card.Keywords.Contains(CardKeyword.Exhaust),
            is_strike = card.Tags.Contains(CardTag.Strike),
            replay_count = card.GetEnchantedReplayCount(),
            card_type = card.Type.ToString(),
            target_type = card.TargetType.ToString(),
            requires_target = CardRequiresTarget(card),
            target_index_space = GetCardTargetIndexSpace(card),
            valid_target_indices = GetCardTargetIndices(combatState, card),
            costs_x = card.EnergyCost.CostsX,
            star_costs_x = card.HasStarCostX,
            energy_cost = card.EnergyCost.GetWithModifiers(CostModifiers.All),
            base_energy_cost = card.EnergyCost.GetWithModifiers(CostModifiers.None),
            local_energy_cost = card.EnergyCost.GetWithModifiers(CostModifiers.Local),
            star_cost = Math.Max(0, card.GetStarCostWithModifiers()),
            dynamic_values = BuildCardDynamicValuePayloads(card)
        }).OrderBy(card => card.card_id, StringComparer.Ordinal)
          .ThenBy(card => card.upgraded)
          .ThenBy(card => card.energy_cost)
          .ThenBy(card => card.star_cost)
          .ThenBy(card => string.Join(",", card.dynamic_values.Select(value =>
              $"{value.name}:{value.current_value}")), StringComparer.Ordinal)
          .ToArray();
    }
}
