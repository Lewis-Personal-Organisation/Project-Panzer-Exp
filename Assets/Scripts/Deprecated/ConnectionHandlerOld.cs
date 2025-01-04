using HelloWorld;
using Unity.Netcode;
using UnityEngine;

public class ConnectionHandlerOld : MonoBehaviour
{
	[SerializeField] private UIManagerOld UIManager;
	[SerializeField] private byte[] cachedConnectionData;
	public bool previousConnectAttemptRejected = false;

	private enum ConnectionErrorTypes
	{
		PlayerCapacityReached,
		UsernameTaken,
		NotImplemented
	}


	public void SetConnectionData(byte[] connectionData)
	{
		this.cachedConnectionData = connectionData;
		NetworkManager.Singleton.NetworkConfig.ConnectionData = this.cachedConnectionData;
	}

	public void OnHostButtonClicked()
	{
		NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
		NetworkManager.Singleton.StartHost();
		Debug.Log($"Successfully joined as Host");

		UIManager.nameDisplayButton.enabled = false;
	}
	public void OnServerButtonClicked() => NetworkManager.Singleton.StartServer();

	public void OnClientButtonClicked()
	{
		NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
		NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
		NetworkManager.Singleton.StartClient();
	}

	private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
	{
		// Additional connection data defined by user code
		var newPlayerData = System.Text.Encoding.ASCII.GetString(request.Payload);

		bool initialClient = request.ClientNetworkId == 0;
		Debug.Log($"Request as {(initialClient ? "Server" : "Client")} with ID {request.ClientNetworkId}. Data: {newPlayerData}");

		bool joinApproved = false;

		// If we are the initial player or combined server and client, auto approve since no names can possibly be taken
		// If we are not the first player, check the username is not taken. Refuse entry if so.
		if (initialClient)
		{
			joinApproved = true;
		}
		else
		{
			Player serverPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject().GetComponent<Player>();

			// If response.Approved is false, you can provide a message that explains the reason why via ConnectionApprovalResponse.Reason
			// On the client-side, NetworkManager.DisconnectReason will be populated with this message via DisconnectReasonMessage
			if (serverPlayer.NetworkData.ServerAtPlayerCapacity())
				response.Reason = $"(E{(int)ConnectionErrorTypes.PlayerCapacityReached}): Session Access Denied - Max Players Reached ({serverPlayer.NetworkData.GetCurrentCapacity}) ";
			else if (!serverPlayer.NetworkData.TrySubmitNewPlayerName(newPlayerData))
				response.Reason = $"(E{(int)ConnectionErrorTypes.UsernameTaken}) Session Access Denied - Player Name already taken ";
			else
				joinApproved = true;
		}

		// Approval Logic. If Approve is true, player is accepted, else rejected
		response.Approved = joinApproved;
		response.CreatePlayerObject = true;

		// The Prefab hash value of the NetworkPrefab, if null the default NetworkManager player Prefab is used
		response.PlayerPrefabHash = null;

		// Position to spawn the player object (if null it uses default of Vector3.zero).  Rotation to spawn the player object (if null it uses the default of Quaternion.identity)
		response.Position = Vector3.zero;
		response.Rotation = Quaternion.identity;

		// If additional approval steps are needed, set this to true until the additional steps are complete
		// once it transitions from true to false the connection approval response will be processed.
		response.Pending = false;
	}

	/// <summary>
	/// Callback for when a client connects
	/// </summary>
	private void OnClientConnectedCallback(ulong obj)
	{
		if (NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
		{
		}
		else if (NetworkManager.Singleton.IsClient)
		{
			UIManager.nameDisplayButton.enabled = false;

			if (UIManager.player)
			{
				Debug.Log($"Our Player is assigned. Calling Player Method for Names");
				UIManager.player.RequestPlayerNamesServerRpc();
			}
		}

		previousConnectAttemptRejected = false;
		UIManager.TogglePlayerNameGroup(false);
		UIManager.ToggleVEDisplay(true);
	}

	private void OnClientDisconnectCallback(ulong obj)
	{
		NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
		NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;

		if (!NetworkManager.Singleton.IsServer && NetworkManager.Singleton.DisconnectReason != string.Empty)
		{
			Debug.Log($"Join Failed: {NetworkManager.Singleton.DisconnectReason}");

			UIManager.UpdateUIOnDisconnect(FilterConnectionError(NetworkManager.Singleton.DisconnectReason) == ConnectionErrorTypes.UsernameTaken);
			previousConnectAttemptRejected = true;
		}
	}

	private ConnectionErrorTypes FilterConnectionError(string error)
	{
		return error switch
		{
			string s when s.StartsWith("(E0)") => ConnectionErrorTypes.PlayerCapacityReached,
			string s when s.StartsWith("(E1)") => ConnectionErrorTypes.UsernameTaken,
			_ => ConnectionErrorTypes.NotImplemented,
		};
	}
}