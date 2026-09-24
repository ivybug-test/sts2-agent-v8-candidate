namespace STS2AIAgent.Game;

/// <summary>Decides whether a visible reward can be claimed with the current potion capacity.</summary>
internal static class RewardPotionPolicy
{
    public static bool CanClaim(bool buttonEnabled, bool isPotion, bool hasEmptyPotionSlot)
    {
        return buttonEnabled && (!isPotion || hasEmptyPotionSlot);
    }
}
