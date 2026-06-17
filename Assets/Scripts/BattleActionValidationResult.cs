public class BattleActionValidationResult
{
    public bool isValid;
    public string reason;

    public static BattleActionValidationResult Valid()
    {
        return new BattleActionValidationResult
        {
            isValid = true,
            reason = ""
        };
    }

    public static BattleActionValidationResult Invalid(string reason)
    {
        return new BattleActionValidationResult
        {
            isValid = false,
            reason = string.IsNullOrWhiteSpace(reason) ? "Invalid BattleAction" : reason
        };
    }
}
