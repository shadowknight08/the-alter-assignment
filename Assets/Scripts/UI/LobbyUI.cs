using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardGame.Networking;

namespace CardGame.UI
{
    // Lobby UI - handles host/join buttons and status display
    public class LobbyUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI joinCodeText;

        private void Start()
        {
            if (hostButton != null)
                hostButton.onClick.AddListener(OnHostClicked);

            if (joinButton != null)
                joinButton.onClick.AddListener(OnJoinClicked);

            if (disconnectButton != null)
            {
                disconnectButton.onClick.AddListener(OnDisconnectClicked);
                disconnectButton.interactable = false;
            }

            if (joinCodeInput != null)
            {
                var placeholder = joinCodeInput.placeholder.GetComponent<TextMeshProUGUI>();
                if (placeholder != null)
                {
                    placeholder.text = "Enter join code...";
                }
            }

            if (joinCodeText != null)
                joinCodeText.gameObject.SetActive(false);

            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnBothPlayersConnected += OnBothPlayersConnected;
            }

            UpdateStatus("Ready to connect");
        }

        private void OnDestroy()
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnBothPlayersConnected -= OnBothPlayersConnected;
            }
        }

        private void Update()
        {
            if (LobbyManager.Instance != null)
            {
                UpdateConnectionStatus();
            }
        }

        private void OnHostClicked()
        {
            if (LobbyManager.Instance == null)
            {
                UpdateStatus("Error: LobbyManager not found!");
                return;
            }

            UpdateStatus("Creating match...");
            SetButtonsEnabled(false);
            LobbyManager.Instance.StartHost();
        }

        private void OnJoinClicked()
        {
            if (LobbyManager.Instance == null)
            {
                UpdateStatus("Error: LobbyManager not found!");
                return;
            }

            string joinCode = joinCodeInput != null ? joinCodeInput.text : "";

            if (string.IsNullOrEmpty(joinCode))
            {
                UpdateStatus("Please enter a join code");
                return;
            }

            UpdateStatus("Joining match...");
            SetButtonsEnabled(false);
            LobbyManager.Instance.StartClient(joinCode.Trim().ToUpper());
        }

        private void OnDisconnectClicked()
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.Disconnect();
                UpdateStatus("Disconnected");
            }
        }

        private void OnBothPlayersConnected()
        {
            UpdateStatus("Both players connected! Loading game...");
        }

        private void UpdateConnectionStatus()
        {
            bool connected = LobbyManager.Instance.IsConnected();
            bool isHost = LobbyManager.Instance.IsHost();

            if (hostButton != null)
                hostButton.interactable = !connected;

            if (joinButton != null)
                joinButton.interactable = !connected && joinCodeInput != null;

            if (disconnectButton != null)
                disconnectButton.interactable = connected;

            if (joinCodeInput != null)
                joinCodeInput.interactable = !connected;

            if (connected && isHost)
            {
                string code = LobbyManager.Instance.GetJoinCode();
                if (joinCodeText != null && !string.IsNullOrEmpty(code))
                {
                    joinCodeText.gameObject.SetActive(true);
                    joinCodeText.text = $"Join Code: {code}\n(Share this with your opponent)";
                }

                int connectedCount = 0;
                if (Unity.Netcode.NetworkManager.Singleton != null)
                {
                    connectedCount = Unity.Netcode.NetworkManager.Singleton.ConnectedClients.Count;
                }

                if (connectedCount >= 2)
                {
                    UpdateStatus("Both players connected! Loading game...");
                }
                else
                {
                    UpdateStatus("Waiting for opponent to join...");
                }
            }
            else if (connected && !isHost)
            {
                if (joinCodeText != null)
                    joinCodeText.gameObject.SetActive(false);

                UpdateStatus("Connected! Waiting for game to start...");
            }
            else if (!connected)
            {
                if (joinCodeText != null)
                    joinCodeText.gameObject.SetActive(false);
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            if (hostButton != null)
                hostButton.interactable = enabled;

            if (joinButton != null)
                joinButton.interactable = enabled;
        }

        private void UpdateStatus(string status)
        {
            if (statusText != null)
                statusText.text = status;
        }
    }
}
