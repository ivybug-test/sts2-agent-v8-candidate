using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using STS2AIAgent.Server;

namespace STS2AIAgent.Game;

internal static partial class GameActionService
{
    private static Task<ActionResponsePayload> ExecuteSetSeedAsync(ActionRequest request)
    {
        var currentScreen = ActiveScreenContext.Instance.GetCurrentScreen();
        var screen = GameStateService.ResolveScreen(currentScreen);
        if (!GameStateService.CanSetSeed(currentScreen))
        {
            throw new ApiException(409, "invalid_action",
                "set_seed is only available after entering character selection and before embark.",
                new { action = "set_seed", screen });
        }

        var game = NGame.Instance ?? throw new ApiException(503, "state_unavailable",
            "NGame.Instance is not available yet.", new { action = "set_seed", screen },
            retryable: true);
        // Entering character selection clears DebugSeedOverride. StartRunLobby reads it
        // at embark, while calling lobby.SetSeed here would invoke an unsupported UI callback.
        // The game canonicalizes lobby seeds but consumes DebugSeedOverride verbatim.
        var canonical = string.IsNullOrWhiteSpace(request.seed)
            ? null : SeedHelper.CanonicalizeSeed(request.seed);
        game.DebugSeedOverride = canonical;
        var applied = game.DebugSeedOverride == canonical;
        return Task.FromResult(new ActionResponsePayload
        {
            action = "set_seed",
            status = applied ? "completed" : "pending",
            stable = applied,
            message = applied ? $"Seed override set to {canonical ?? "<random>"}."
                : "Seed override readback did not match.",
            state = GameStateService.BuildStatePayload()
        });
    }
}
