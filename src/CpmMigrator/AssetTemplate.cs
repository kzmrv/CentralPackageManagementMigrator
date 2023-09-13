namespace CpmMigrator
{
    internal class AssetTemplate
    {
        private Dictionary<string, string> _data = new Dictionary<string, string>();

        public AssetTemplate(Dictionary<string, string> data)
        {
            _data = data;
        }

        public Dictionary<string, string> Data
        {
            get
            {
                return _data;
            }
        }
    }
}
