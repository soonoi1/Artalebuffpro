using System;

namespace ArtaleProBuff
{
    public class PatrolStepViewModel : ViewModelBase
    {
        private string _direction = "右"; // "右", "左"
        private string _durationText = "2.0";
        private string _pauseAfterText = "0.0";

        public string Direction
        {
            get => _direction;
            set => SetField(ref _direction, value);
        }

        public string DurationText
        {
            get => _durationText;
            set => SetField(ref _durationText, value);
        }

        public string PauseAfterText
        {
            get => _pauseAfterText;
            set => SetField(ref _pauseAfterText, value);
        }
    }
}
