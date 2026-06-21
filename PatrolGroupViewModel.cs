using System;
using System.Text.Json.Serialization;

namespace ArtaleProBuff
{
    public class PatrolGroupViewModel : ViewModelBase
    {
        private bool _isActive = true;
        private string _rightTimeText = "2.0";
        private string _leftTimeText = "2.0";
        private string _midPauseTimeText = "0.0";
        private string _intervalAfterText = "5.0";
        
        private bool _isTimedLoop = false;
        private string _loopIntervalText = "30";
        private string _loopCountText = "1";
        
        // Runtime UI state
        private string _status = "等待运行";
        private DateTime _nextRunTime = DateTime.MinValue;

        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        public string RightTimeText
        {
            get => _rightTimeText;
            set => SetField(ref _rightTimeText, value);
        }

        public string LeftTimeText
        {
            get => _leftTimeText;
            set => SetField(ref _leftTimeText, value);
        }

        public string MidPauseTimeText
        {
            get => _midPauseTimeText;
            set => SetField(ref _midPauseTimeText, value);
        }

        public string IntervalAfterText
        {
            get => _intervalAfterText;
            set => SetField(ref _intervalAfterText, value);
        }

        public bool IsTimedLoop
        {
            get => _isTimedLoop;
            set => SetField(ref _isTimedLoop, value);
        }

        public string LoopIntervalText
        {
            get => _loopIntervalText;
            set => SetField(ref _loopIntervalText, value);
        }

        public string LoopCountText
        {
            get => _loopCountText;
            set => SetField(ref _loopCountText, value);
        }

        [JsonIgnore]
        public DateTime NextRunTime
        {
            get => _nextRunTime;
            set => _nextRunTime = value;
        }

        [JsonIgnore]
        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }
    }
}
