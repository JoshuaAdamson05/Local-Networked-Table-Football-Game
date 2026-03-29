[System.Serializable]
public class BallMetricData : MetricData
{
    public int ExtrapolationCount;
    public int TotalTickSamples;

    public void RecordTickSample(bool isExtrapolating)
    {
        TotalTickSamples++;

        if (isExtrapolating)
            ExtrapolationCount++;
    }

    public float GetExtrapolationPercent()
    {
        return TotalTickSamples > 0
            ? (float)ExtrapolationCount / TotalTickSamples * 100f
            : 0f;
    }

    public override void Reset()
    {
        base.Reset();
        ExtrapolationCount = 0;
        TotalTickSamples = 0;
    }
}
