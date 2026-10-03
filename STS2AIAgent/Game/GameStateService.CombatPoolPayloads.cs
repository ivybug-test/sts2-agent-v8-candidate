using System;

namespace STS2AIAgent.Game;

internal sealed class CombatPileCardPayload
{
    public string card_id { get; init; } = string.Empty;

    public bool upgraded { get; init; }

    public bool is_upgradable { get; init; }

    public bool is_unplayable { get; init; }

    public bool exhaust_on_play { get; init; }

    public bool is_strike { get; init; }

    public int replay_count { get; init; }

    public string card_type { get; init; } = string.Empty;

    public string target_type { get; init; } = string.Empty;

    public bool requires_target { get; init; }

    public string? target_index_space { get; init; }

    public int[] valid_target_indices { get; init; } = Array.Empty<int>();

    public bool costs_x { get; init; }

    public bool star_costs_x { get; init; }

    public int energy_cost { get; init; }

    public int base_energy_cost { get; init; }

    public int local_energy_cost { get; init; }

    public int star_cost { get; init; }

    public CardDynamicValuePayload[] dynamic_values { get; init; } = Array.Empty<CardDynamicValuePayload>();
}
