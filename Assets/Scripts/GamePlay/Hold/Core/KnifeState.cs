namespace GamePlay.HoldSystem
{
    public enum KnifeState
    {
        None,
        Preparing,
        Ready,
        LightAttack,
        HeavyAttack
    }

    public enum KnifeAttackKind : byte
    {
        Light,
        Heavy
    }
}
