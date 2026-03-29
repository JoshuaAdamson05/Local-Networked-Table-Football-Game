[System.Serializable]
public class MetricData
{
    public float TotalError;
    public int ErrorSamples;
    public float MaxError;
    public int SnapCount;
    public uint MaxSnapshotGap;

    public void RecordError(float error)
    {
        TotalError += error;
        ErrorSamples++;

        if (error > MaxError)
            MaxError = error;
    }

    public void RecordSnap()
    {
        SnapCount++;
    }

    public void RecordSnapshotGap(uint gap)
    {
        if (gap > MaxSnapshotGap)
            MaxSnapshotGap = gap;
    }

    public float GetAverageError()
    {
        return ErrorSamples > 0 ? TotalError / ErrorSamples : 0f;
    }

    public virtual void Reset()
    {
        TotalError = 0f;
        ErrorSamples = 0;
        MaxError = 0f;
        SnapCount = 0;
        MaxSnapshotGap = 0;
    }
}
