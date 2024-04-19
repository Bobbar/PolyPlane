namespace PolyPlane.Net
{
    public class ChatInterface
    {
        public string CurrentText => _currentText;
        public bool ChatIsActive = false;

        public const int MAX_CHARS = 50;
        private string _currentText = string.Empty;
        private readonly string _playerName;
        private NetEventManager _netMan = null;

        public ChatInterface(NetEventManager netMan, string playerName)
        {
            _netMan = netMan;
            _playerName = playerName;
        }

        public void NewKeyPress(char key)
        {
            if (!HandleActionKeys(key))
            {
                if (!ChatIsActive)
                    return;

                if (_currentText.Length < MAX_CHARS)
                    _currentText += key;
            }
        }

        private bool HandleActionKeys(char key)
        {
            switch (key)
            {
                case 'y' or 'Y':
                    if (!ChatIsActive)
                    {
                        ChatIsActive = true;
                        return true;
                    }
                    else
                        return false;

                case (char)13:
                    SendCurrentMessage();
                    return true;

                case (char)8:
                    DoBackspace();
                    return true;

                case (char)27:
                    DoEscape();
                    return true;
            }

            return false;
        }

        private void DoBackspace()
        {
            if (_currentText.Length > 0)
                _currentText = _currentText.Remove(_currentText.Length - 1, 1);
        }

        private void DoEscape()
        {
            _currentText = string.Empty;
            ChatIsActive = false;
        }

        private void SendCurrentMessage()
        {
            _currentText = _currentText.Trim();

            if (!string.IsNullOrEmpty(_currentText))
                _netMan.Host.SendNewChatPacket(_currentText, _playerName);

            ChatIsActive = false;
            _currentText = string.Empty;
        }

        public void SendMessage(string message)
        {
            message = message.Trim();

            if (!string.IsNullOrEmpty(message))
                _netMan.Host.SendNewChatPacket(message, _playerName);

            ChatIsActive = false;
            _currentText = string.Empty;
        }
    }
}
