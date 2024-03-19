namespace PolyPlane
{
    public partial class ServerUI : Form
    {
        public string InfoText;
        public bool PauseRequested = false;
        public bool SpawnIAPlane = false;


        private System.Windows.Forms.Timer _updateTimer = new System.Windows.Forms.Timer();
        public ServerUI()
        {
            InitializeComponent();

            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Interval = 16;
            _updateTimer.Start();
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            InfoLabel.Text = InfoText;
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            PauseRequested = true;
        }

        private void SpawnAIPlaneButton_Click(object sender, EventArgs e)
        {
            SpawnIAPlane = true;
        }

        private void InterpCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            World.InterpOn = InterpCheckBox.Checked;
        }
    }
}
