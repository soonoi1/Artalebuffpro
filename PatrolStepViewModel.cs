using System;

namespace ArtaleProBuff
{
    public class PatrolStepViewModel : ViewModelBase
    {
        private string _direction = "右"; // "右", "左", "停留"
        private string _durationText = "2.0";

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
    }
}
