using System;
using System.Text.Json.Serialization;

namespace ArtaleProBuff
{
    public class BuffCardViewModel : ViewModelBase
    {
        private string _key = "f5";
        private string _intervalText = "175";
        private string _fluctuationText = "10";
        private bool _isActive = true;
        private bool _isLongPress = false;
        private string _holdTimeText = "5.0";
        private bool _isExclusive = false;

        // Runtime UI states (not serialized)
        private string _status = "就绪";
        private double _progressMax = 100;
        private double _progressValue = 0;
        private string _variationText = "等待运行...";
        private string _variationColor = "#9ca3af";

        public string Key
        {
            get => _key;
            set => SetField(ref _key, value);
        }

        public string IntervalText
        {
            get => _intervalText;
            set => SetField(ref _intervalText, value);
        }

        public string FluctuationText
        {
            get => _fluctuationText;
            set => SetField(ref _fluctuationText, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        public bool IsLongPress
        {
            get => _isLongPress;
            set => SetField(ref _isLongPress, value);
        }

        public string HoldTimeText
        {
            get => _holdTimeText;
            set => SetField(ref _holdTimeText, value);
        }

        public bool IsExclusive
        {
            get => _isExclusive;
            set => SetField(ref _isExclusive, value);
        }

        [JsonIgnore]
        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        [JsonIgnore]
        public double ProgressMax
        {
            get => _progressMax;
            set => SetField(ref _progressMax, value);
        }

        [JsonIgnore]
        public double ProgressValue
        {
            get => _progressValue;
            set => SetField(ref _progressValue, value);
        }

        [JsonIgnore]
        public string VariationText
        {
            get => _variationText;
            set => SetField(ref _variationText, value);
        }

        [JsonIgnore]
        public string VariationColor
        {
            get => _variationColor;
            set => SetField(ref _variationColor, value);
        }

        // Thread cancellation token source helper
        [JsonIgnore]
        public System.Threading.CancellationTokenSource Cts { get; set; }
    }
}
