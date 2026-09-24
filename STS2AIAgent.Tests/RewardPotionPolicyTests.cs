using STS2AIAgent.Game;

namespace STS2AIAgent.Tests;

internal static class RewardPotionPolicyTests
{
    public static void FullSlotsRequireCapacityBeforeClaim()
    {
        Assert.False(RewardPotionPolicy.CanClaim(true, true, false));
        Assert.True(RewardPotionPolicy.CanClaim(true, true, true));
        Assert.True(RewardPotionPolicy.CanClaim(true, false, false));
        Assert.False(RewardPotionPolicy.CanClaim(false, true, true));
    }
}
