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

    private static void Postfix(object __instance, MethodBase __originalMethod, object[] __args)
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
                ReadFacts(__instance, __originalMethod, __args));
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
    private static Dictionary<string, object?> ReadFacts(
        object entry, MethodBase constructor, object[] arguments)
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
                object? fact = ReadFact(value);
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

        // Some history entries expose their resolved quantities only as fields. Keep their
        // primitive values with explicit field_ provenance instead of parsing display text.
        for (Type? type = entry.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            foreach (var field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.Public |
                         BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                try
                {
                    var fact = ReadFact(field.GetValue(entry));
                    if (fact != null)
                    {
                        facts["field_" + field.Name] = fact;
                    }
                }
                catch
                {
                    // A field may be unavailable during construction; omit it.
                }
            }
        }

        var parameters = constructor.GetParameters();
        for (var index = 0; index < Math.Min(parameters.Length, arguments.Length); index++)
        {
            var name = parameters[index].Name ?? index.ToString();
            try
            {
                var fact = ReadFact(arguments[index]);
                if (fact != null)
                {
                    facts["arg_" + name] = fact;
                }
                else if (arguments[index] != null)
                {
                    facts["arg_type_" + name] = arguments[index].GetType().Name;
                }
            }
            catch
            {
                // Preserve the action even when a constructor argument cannot be inspected.
            }
        }

        return facts;
    }

    private static object? ReadFact(object? value) => value switch
    {
        null => null,
        string or bool or byte or short or ushort or int or uint or long or ulong or
            float or double or decimal => value,
        Enum e => e.ToString(),
        CardModel card => new
        {
            card_id = card.Id.Entry,
            instance_ref = RuntimeHelpers.GetHashCode(card)
        },
        Creature creature => new
        {
            model_id = creature.ModelId.Entry,
            instance_ref = RuntimeHelpers.GetHashCode(creature),
            current_hp = creature.CurrentHp,
            max_hp = creature.MaxHp,
            block = creature.Block
        },
        Player player => new
        {
            player_id = player.NetId.ToString(),
            character_id = player.Character?.Id.Entry
        },
        AbstractModel model => new { model_id = model.Id.Entry },
        _ => ReadKnownRecord(value)
    };

    private static object? ReadKnownRecord(object value)
    {
        // These are resolution records, not live game objects. Capture only their directly
        // typed values so damage and card attribution do not depend on localized descriptions.
        if (value.GetType().Name is not ("DamageResult" or "CardPlay"))
        {
            return null;
        }

        Dictionary<string, object?> values = new(StringComparer.Ordinal);
        foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }
            try
            {
                var fact = ReadDirectValue(property.GetValue(value));
                if (fact != null)
                {
                    values[property.Name] = fact;
                }
            }
            catch
            {
                // An individual resolution member may not be readable yet.
            }
        }
        foreach (var field in value.GetType().GetFields(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            try
            {
                var fact = ReadDirectValue(field.GetValue(value));
                if (fact != null)
                {
                    values["field_" + field.Name] = fact;
                }
            }
            catch
            {
                // Keep the rest of the record if one field cannot be read.
            }
        }
        return values.Count > 0 ? values : null;
    }

    private static object? ReadDirectValue(object? value) => value switch
    {
        null => null,
        string or bool or byte or short or ushort or int or uint or long or ulong or
            float or double or decimal => value,
        Enum e => e.ToString(),
        CardModel card => new { card_id = card.Id.Entry, instance_ref = RuntimeHelpers.GetHashCode(card) },
        Creature creature => new { model_id = creature.ModelId.Entry, instance_ref = RuntimeHelpers.GetHashCode(creature) },
        _ => null
    };
}
