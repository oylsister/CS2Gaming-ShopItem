namespace ShopItem
{
    public class PlayerData 
    {
        public PlayerData(bool bought, string achieve, string reset)
        {
            _bought = bought;
            _timeAcheived = achieve;
            _timeReset = reset;
        }

        private bool _bought;
        private string _timeAcheived;
        private string _timeReset;

        public bool Bought
        {
            get { return _bought; }
            set { _bought = value; }
        }

        public string TimeAcheived
        {
            get { return _timeAcheived; }
            set { _timeAcheived = value; }
        }

        public string TimeReset
        {
            get { return _timeReset; }
            set { _timeReset = value; }
        }
    }
}
