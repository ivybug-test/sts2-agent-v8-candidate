using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2AIAgent.Server;

/// <summary>
/// Captures completed combat-history entries as analysis-only events. These entries are emitted by
/// the game at resolution time; polling /state would miss transient draws and damage applications.
/// </summary>
[HarmonyPatch]
internal static class CombatHistoryTelemetryPatch
{
    private static int _warningLogged;

    private static readonly Type[] EntryTypes =
    [
        typeof(CardPlayStartedEntry), typeof(CardPlayFinishedEntry),
        typeof(CardDrawnEntry), typeof(CardDiscardedEntry),
        typeof(CardExhaustedEntry), typeof(CardGeneratedEntry),
        typeof(DamageReceivedEntry), typeof(CreatureAttackedEntry),
        typeof(BlockGainedEntry), typeof(EnergySpentEntry),
        typeof(PowerReceivedEntry), typeof(PotionUsedEntry),
        typeof(OrbChanneledEntry), typeof(SummonedEntry),
        typeof(StarsModifiedEntry),
    ];

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in EntryTypes)
        {
            foreach (var constructor in type.GetConstructors(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                yield return constructor;
            }
        }
    }

    private static void Postfix(object __instance)
    {
        if (!GameEventService.Instance.HasSubscribers)
        {
            return;
        }

        try
        {
            var run = RunManager.Instance?.DebugOnlyGetState();
            var combat = CombatManager.Instance?.DebugOnlyGetState();
            GameEventService.Instance.PublishCombatHistory(
                __instance.GetType().Name,
                run?.Rng.StringSeed ?? "run_unknown",
                run?.CurrentActIndex,
                run?.TotalFloor,
                combat?.RoundNumber,
                ReadFacts(__instance));
        }
        catch (Exception ex)
        {
            // Instrumentation may lose this entry, but it must never change combat execution.
            try
            {
                GameEventService.Instance.PublishCombatHistoryError(ex.GetType().Name);
            }
            catch
            {
                // The game's action must still complete if the stream itself has failed.
            }
            if (Interlocked.Exchange(ref _warningLogged, 1) == 0)
            {
                Log.Error($"[STS2AIAgent] Combat telemetry capture failed: {ex}");
            }
        }
    }

    /// <summary>
    /// Keep only primitive facts and stable public IDs. In particular, never serialize the full
    /// game object or an arbitrary ToString() value that might include hidden game information.
    /// </summary>
    private static Dictionary<string, object?> ReadFacts(object entry)
    {
        Dictionary<string, object?> facts = new(StringComparer.Ordinal);
        foreach (var property in entry.GetType().GetProperties(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            try
            {
                object? value = property.GetValue(entry);
                object? fact = value switch
                {
                    null => null,
                    string or bool or byte or short or int or long or float or double or decimal => value,
                    Enum e => e.ToString(),
                    CardModel card => new
                    {
                        card_id = card.Id.Entry,
                        instance_ref = RuntimeHelpers.GetHashCode(card)
                    },
                    Creature creature => new
                    {
                        instance_ref = RuntimeHelpers.GetHashCode(creature),
                        current_hp = creature.CurrentHp,
                        block = creature.Block
                    },
                    Player player => new
                    {
                        player_id = player.NetId.ToString(),
                        character_id = player.Character?.Id.Entry
                    },
                    AbstractModel model => new { model_id = model.Id.Entry },
                    _ => null
                };
                if (fact != null)
                {
                    facts[property.Name] = fact;
                }
            }
            catch
            {
                // A property can become unavailable during resolution. Omit it and let the
                // consumer report coverage; telemetry must never change combat execution.
            }
        }

        return facts;
    }
}
