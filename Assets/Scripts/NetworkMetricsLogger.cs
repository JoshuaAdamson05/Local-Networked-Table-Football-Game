using System;
using System.Globalization;
using System.IO;
using Unity.Netcode;
using UnityEngine;

public class NetworkMetricsLogger : MonoBehaviour
{
    private bool _isLogging = false;
    public bool IsLogging => _isLogging;
    [SerializeField] private string _folderName = "NetworkLogs";
    [SerializeField] private string _fileName = "network_metrics.csv";
    private float _startTime;
    private string _fullFilePath;
    private bool _isPathInitialised = false;
    public static NetworkMetricsLogger Instance;
    public BallMetricData BallMetricData = new BallMetricData();
    public MetricData ClientRodMetricData = new MetricData();

    private void Awake()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            Destroy(this.gameObject);
            return;
        }

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Network metrics logger already exists!");
            Destroy(this.gameObject);
            return;
        }

        KickOffEventBus.OnKickOff += OnKickOff;

        string folderPath = Path.Combine(Application.dataPath, _folderName);
        Directory.CreateDirectory(folderPath);
        _fullFilePath = Path.Combine(folderPath, _fileName);
        CreaterHeader();
        _isPathInitialised = true;
    }

    private void OnDestroy()
    {
        KickOffEventBus.OnKickOff -= OnKickOff;
        StopLogging();
    }

    public void StartLogging()
    {
        if (!_isPathInitialised || _isLogging) return;
        BallMetricData.Reset();
        ClientRodMetricData.Reset();
        _startTime = Time.time;
        _isLogging = true;
        Debug.Log("Network metrics logger has started logging!");
    }

    private void StopLogging()
    {
        if (!_isPathInitialised || !_isLogging) return;
        _isLogging = false;
        WriteSummaryRow();
    }

    private void CreaterHeader()
    {
        if (File.Exists(_fullFilePath)) return;

        string header = "Timestamp,DurationSeconds,BallAverageError,BallMaxError,BallSnapCount,BallExtrapolationPercent,BallMaxSnapshotGap," +
            "RodAverageError,RodMaxError,RodSnapCount,RodMaxSnapshotGap" + Environment.NewLine;

        File.WriteAllText(_fullFilePath, header);
    }

    private void WriteSummaryRow()
    {
        float duration = Time.time - _startTime;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string row = string.Join(",",
            Escape(timestamp),
            duration.ToString("F2", CultureInfo.InvariantCulture),

            BallMetricData.GetAverageError().ToString("F4", CultureInfo.InvariantCulture),
            BallMetricData.MaxError.ToString("F4", CultureInfo.InvariantCulture),
            BallMetricData.SnapCount.ToString(),
            BallMetricData.GetExtrapolationPercent().ToString("F2", CultureInfo.InvariantCulture),
            BallMetricData.MaxSnapshotGap.ToString(),

            ClientRodMetricData.GetAverageError().ToString("F4", CultureInfo.InvariantCulture),
            ClientRodMetricData.MaxError.ToString("F4", CultureInfo.InvariantCulture),
            ClientRodMetricData.SnapCount.ToString(),
            ClientRodMetricData.MaxSnapshotGap.ToString()
        );

        File.AppendAllText(_fullFilePath, row + Environment.NewLine);
        Debug.Log($"Network metrics written to CSV: {_fullFilePath}");
    }

    private string Escape(string value)
    {
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }

        return value;
    }

    private void OnKickOff(KickOffEvent kickOffEvent)
    {
        StartLogging();
    }
}
