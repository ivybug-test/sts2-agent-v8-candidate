namespace STS2AIAgent.Tests;

internal static class V14RunEntryContractTests
{
    public static void SetSeedRemainsAvailableForFixedSuiteRuns()
    {
        var actions = AgentSourceFixture.ReadActionService();
        var state = AgentSourceFixture.ReadStateService();
        var executor = AgentSourceFixture.WithoutWhitespace(
            AgentSourceFixture.MethodBody(actions, "ExecuteSetSeedAsync"));

        Assert.Contains("\"set_seed\"=>ExecuteSetSeedAsync(request)",
            AgentSourceFixture.WithoutWhitespace(actions));
        Assert.Contains("publicstring?seed{get;init;}",
            AgentSourceFixture.WithoutWhitespace(actions));
        Assert.Contains("if(!GameStateService.CanSetSeed(currentScreen))", executor);
        Assert.Contains("game.DebugSeedOverride=canonical", executor);
        Assert.Contains("status=applied?\"completed\":\"pending\"", executor);
        Assert.Contains("name=\"set_seed\"", AgentSourceFixture.WithoutWhitespace(state));
        Assert.Contains("currentScreenisNCharacterSelectScreen",
            AgentSourceFixture.WithoutWhitespace(
                AgentSourceFixture.MethodBody(state, "CanSetSeed")));
    }

    public static void ChooseCardWaitsForOverlayInputGate()
    {
        var body = AgentSourceFixture.WithoutWhitespace(
            AgentSourceFixture.MethodBody(AgentSourceFixture.ReadActionService(),
                "ExecuteSelectDeckCardAsync"));
        var gate = body.IndexOf("if(currentScreenisNChooseACardSelectionScreen)",
            StringComparison.Ordinal);
        var wait = body.IndexOf("awaitTask.Delay(500)", StringComparison.Ordinal);
        var press = body.IndexOf("selected.EmitSignal(NCardHolder.SignalName.Pressed,selected)",
            StringComparison.Ordinal);
        Assert.True(gate >= 0 && gate < wait && wait < press,
            "The choose-card overlay input gate must open before Pressed is emitted.");
    }
}
