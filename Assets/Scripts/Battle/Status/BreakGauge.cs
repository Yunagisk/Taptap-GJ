using System;

// 敌人的破防条。进度达到阈值就清空、跳过下一次还没执行的行动，并进入保护。
// 保护覆盖破防当轮和下一轮，再下一轮才能重新累计。保护期内不能刷新跳过。
public sealed class BreakGauge
{
    public int Threshold { get; private set; }
    public int Progress { get; private set; }
    public int ProtectedThroughRound { get; private set; }
    public bool SkipPending { get; private set; }

    public event Action Changed;

    public BreakGauge(int threshold)
    {
        Threshold = threshold < 0 ? 0 : threshold;
    }

    public bool IsProtected(int round)
    {
        return ProtectedThroughRound > 0 && round <= ProtectedThroughRound;
    }

    public bool TryAdd(int amount, int round)
    {
        if (amount <= 0 || Threshold <= 0 || IsProtected(round))
            return false;

        Progress += amount;
        bool broke = Progress >= Threshold;
        if (broke)
        {
            Progress = 0;
            ProtectedThroughRound = round + 1;
            SkipPending = true;
        }

        Changed?.Invoke();
        return broke;
    }

    public bool ConsumeSkip()
    {
        if (!SkipPending)
            return false;
        SkipPending = false;
        Changed?.Invoke();
        return true;
    }

    public string Format(int round)
    {
        if (Threshold <= 0)
            return "";

        string text = "破防 " + Progress + "/" + Threshold;
        if (IsProtected(round))
            text = "破防保护中";
        if (SkipPending)
            text += " · 跳过下次行动";
        return text;
    }
}
