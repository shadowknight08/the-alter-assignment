using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace CardGame.Networking
{

    public class LobbyManager : MonoBehaviour
    {
        private static LobbyManager instance;
        public static LobbyManager Instance => instance;

        [SerializeField] private string gameSceneName = "Game";

        private bool isHost;
        private string joinCode;
        private bool isAuthenticated = false;
        private bool gameSceneLoaded = false;

        public event Action OnBothPlayersConnected;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeServices();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private async void InitializeServices()
        {
            try
            {
                await UnityServices.InitializeAsync();
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                isAuthenticated = true;
                Debug.Log("[LobbyManager] Unity Services initialized and authenticated");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyManager] Failed to initialize Unity Services: {e.Message}");
            }
        }

        public async void StartHost()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[LobbyManager] Already hosting");
                return;
            }

            if (!await EnsureAuthenticated())
            {
                Debug.LogError("[LobbyManager] Authentication failed");
                return;
            }

            try
            {
                Debug.Log("[LobbyManager] Creating Relay allocation...");

                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections: 1);
                joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    Debug.LogError("[LobbyManager] UnityTransport component not found on NetworkManager");
                    return;
                }

                transport.SetRelayServerData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );

                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                bool started = NetworkManager.Singleton.StartHost();
                if (started)
                {
                    isHost = true;
                    gameSceneLoaded = false;
                    Debug.Log($"[LobbyManager] Host started successfully! Join Code: {joinCode}");
                    Debug.Log($"[LobbyManager] Share this join code with another player to connect.");
                }
                else
                {
                    Debug.LogError("[LobbyManager] Failed to start host");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyManager] Failed to start host: {e.Message}");
            }
        }

        public async void StartClient(string joinCodeInput)
        {
            if (NetworkManager.Singleton.IsClient)
            {
                Debug.LogWarning("[LobbyManager] Already connected as client");
                return;
            }

            if (string.IsNullOrEmpty(joinCodeInput))
            {
                Debug.LogError("[LobbyManager] Join code is required");
                return;
            }

            if (!await EnsureAuthenticated())
            {
                Debug.LogError("[LobbyManager] Authentication failed");
                return;
            }

            try
            {
                Debug.Log($"[LobbyManager] Joining match with code: {joinCodeInput}...");

                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCodeInput);

                UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    Debug.LogError("[LobbyManager] UnityTransport component not found on NetworkManager");
                    return;
                }

                transport.SetRelayServerData(
                    joinAllocation.RelayServer.IpV4,
                    (ushort)joinAllocation.RelayServer.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData
                );

                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                bool started = NetworkManager.Singleton.StartClient();
                if (started)
                {
                    isHost = false;
                    joinCode = joinCodeInput;
                    gameSceneLoaded = false;
                    Debug.Log($"[LobbyManager] Client connecting with join code: {joinCodeInput}");
                }
                else
                {
                    Debug.LogError("[LobbyManager] Failed to start client");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyManager] Failed to join: {e.Message}");
            }
        }

        private void LoadGameScene()
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            if (gameSceneLoaded)
            {
                Debug.LogWarning("[LobbyManager] Game scene already loaded");
                return;
            }

            Debug.Log($"[LobbyManager] Loading game scene: {gameSceneName}");

            try
            {
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
                gameSceneLoaded = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyManager] Failed to load game scene: {e.Message}");
            }
        }

        private async Task<bool> EnsureAuthenticated()
        {
            if (isAuthenticated) return true;

            int attempts = 0;
            while (!isAuthenticated && attempts < 50)
            {
                await Task.Delay(100);
                attempts++;
            }

            return isAuthenticated;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (isHost)
            {
                int connectedClients = NetworkManager.Singleton.ConnectedClients.Count;
                Debug.Log($"[LobbyManager] Client {clientId} connected. Total players: {connectedClients}");

                if (connectedClients == 2)
                {
                    Debug.Log("[LobbyManager] Both players connected! Loading game scene...");
                    OnBothPlayersConnected?.Invoke();
                    LoadGameScene();
                }
            }
            else
            {
                Debug.Log("[LobbyManager] Connected to host!");
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            gameSceneLoaded = false;
            Debug.Log($"[LobbyManager] Client {clientId} disconnected");
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton != null &&
                (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
                gameSceneLoaded = false;
                Debug.Log("[LobbyManager] Disconnected");
            }
        }

        public bool IsConnected() => NetworkManager.Singleton != null &&
                                     (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer);

        public string GetJoinCode() => joinCode;

        public bool IsHost() => isHost;
    }
}
