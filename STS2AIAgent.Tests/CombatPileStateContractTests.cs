namespace STS2AIAgent.Tests;

internal static class CombatPileStateContractTests
{
    public static void RawCombatIncludesStructuredPileCards()
    {
        var source = AgentSourceFixture.ReadStateService();
        var combat = AgentSourceFixture.WithoutWhitespace(
            AgentSourceFixture.MethodBody(source, "BuildCombatPayload"));
        var pile = AgentSourceFixture.WithoutWhitespace(
            AgentSourceFixture.MethodBody(source, "BuildCombatPilePayload"));

        Assert.Contains("draw_pile=BuildCombatPilePayload(combatState,me.PlayerCombatState.DrawPile.Cards)", combat);
        Assert.Contains("discard_pile=BuildCombatPilePayload(combatState,me.PlayerCombatState.DiscardPile.Cards)", combat);
        Assert.Contains("exhaust_pile=BuildCombatPilePayload(combatState,me.PlayerCombatState.ExhaustPile.Cards)", combat);
        Assert.Contains("card_id=card.Id.Entry", pile);
        Assert.Contains("dynamic_values=BuildCardDynamicValuePayloads(card)", pile);
        Assert.Contains("OrderBy(card=>card.card_id", pile);
    }
}
