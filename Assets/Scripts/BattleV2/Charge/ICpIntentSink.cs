namespace BattleV2.Charge
{
    public interface ICpIntentSink
    {
        void BeginTurn(int maxCp);
        void EndTurn(string reason = null);
        void Set(int value, string reason = null);
        void Add(int delta, string reason = null);
        int ConsumeOnce(int selectionId, string reason = null);
        void Cancel(string reason = null);
    }
}
