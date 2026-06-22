using System;
using System.Collections.ObjectModel;
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
        private ObservableCollection<PatrolStepViewModel> _steps = new ObservableCollection<PatrolStepViewModel>();

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

        public ObservableCollection<PatrolStepViewModel> Steps
        {
            get => _steps;
            set => SetField(ref _steps, value);
        }

        public void InitializeStepsFromLegacy()
        {
            if (Steps == null)
            {
                Steps = new ObservableCollection<PatrolStepViewModel>();
            }

            if (Steps.Count == 0)
            {
                bool hasLegacy = !string.IsNullOrEmpty(RightTimeText) || !string.IsNullOrEmpty(LeftTimeText) || !string.IsNullOrEmpty(MidPauseTimeText);
                if (hasLegacy)
                {
                    double r = 0, l = 0, m = 0;
                    double.TryParse(RightTimeText, out r);
                    double.TryParse(LeftTimeText, out l);
                    double.TryParse(MidPauseTimeText, out m);

                    if (r > 0)
                    {
                        Steps.Add(new PatrolStepViewModel { Direction = "右", DurationText = RightTimeText, PauseAfterText = MidPauseTimeText });
                    }
                    if (l > 0)
                    {
                        Steps.Add(new PatrolStepViewModel { Direction = "左", DurationText = LeftTimeText, PauseAfterText = "0.0" });
                    }
                }

                // If still empty (e.g. new group or empty config), add default steps
                if (Steps.Count == 0)
                {
                    Steps.Add(new PatrolStepViewModel { Direction = "右", DurationText = "2.0", PauseAfterText = "0.0" });
                    Steps.Add(new PatrolStepViewModel { Direction = "左", DurationText = "2.0", PauseAfterText = "0.0" });
                }
            }
        }

        [JsonIgnore]
        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }
    }
}
