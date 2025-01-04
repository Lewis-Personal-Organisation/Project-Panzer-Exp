using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Unity.Services.Samples.ServerlessMultiplayerGame;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;

[DisallowMultipleComponent]
public class LobbyManager : MonoBehaviour
{
	public static LobbyManager instance { get; private set; }
	public Lobby activeLobby { get; private set; }
	public static string playerId => AuthenticationService.Instance.PlayerId;
	public List<Player> players { get; private set; }
	public int numPlayers => players.Count;
	public bool isHost { get; private set; }
	public const string hostNameKey = "hostName";
	public const string relayJoinCodeKey = "relayJoinCode";
	public static event Action<Lobby, bool> OnLobbyChanged;
	public static event Action OnPlayerNotInLobbyEvent;
	public static bool lobbyPreviouslyRefusedUsername = false;
	float nextHostHeartbeatTime;
	const float hostHeartbeatFrequency = 15;
	float nextUpdatePlayersTime;
	const float updatePlayersFrequency = 1.5F;
	bool wasGameStarted = false;

	private PlayerDictionaryData playerDictionaryData;
	private class PlayerDictionaryData
	{
		public PlayerDictionaryData(string playerName, bool isPlayerReady)
		{
			this.playerName = playerName;
			this.isPlayerReady = isPlayerReady;
		}

		public const string playerNameKey = "playerName";
		public string playerName;
		public const string isReadyKey = "isReady";
		public bool isPlayerReady = false;
	}

	ILobbyEvents activeLobbyEvents;

	private void Awake()
	{
		if (instance != null && instance != this)
		{
			Destroy(this);
		}
		else
		{
			instance = this;
		}
	}

    async void Update()
    {
		try
		{
			if (activeLobby != null && !wasGameStarted)
			{
				if (isHost && Time.realtimeSinceStartup >= nextHostHeartbeatTime)
				{
					await PeriodicHostHeartbeat();

					// Exit this update now so we'll only ever update 1 item (heartbeat or lobby changes) in 1 Update().
					return;
				}

				if (Time.realtimeSinceStartup >= nextUpdatePlayersTime)
				{
					await PeriodicUpdateLobby();
				}
			}
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}


	async Task PeriodicHostHeartbeat()
	{
		try
		{
			// Set next heartbeat time before calling Lobby Service since next update could also trigger a
			// heartbeat which could cause throttling issues.
			nextHostHeartbeatTime = Time.realtimeSinceStartup + hostHeartbeatFrequency;

			await LobbyService.Instance.SendHeartbeatPingAsync(activeLobby.Id);
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}

	async Task PeriodicUpdateLobby()
	{
		try
		{
			// Set next update time before calling Lobby Service since next update could also trigger an
			// update which could cause throttling issues.
			nextUpdatePlayersTime = Time.realtimeSinceStartup + updatePlayersFrequency;

			var updatedLobby = await LobbyService.Instance.GetLobbyAsync(activeLobby.Id);
			if (this == null) return;

			Debug.Log($"Old Players:{activeLobby.Players?.Count}, New Players:{updatedLobby.Players?.Count}");
			UpdateLobby(updatedLobby);
		}

		// Handle lobby no longer exists (host canceled game and returned to main menu).
		catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
		{
			if (this == null) return;

			// Lobby has closed
			//ServerlessMultiplayerGameSampleManager.instance.SetReturnToMenuReason(
			//	ServerlessMultiplayerGameSampleManager.ReturnToMenuReason.LobbyClosed);

			OnPlayerNotInLobby();
		}

		// Handle player no longer allowed to view lobby (host booted player so player is no longer in the lobby).
		catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.Forbidden)
		{
			if (this == null) return;

			//ServerlessMultiplayerGameSampleManager.instance.SetReturnToMenuReason(
			//	ServerlessMultiplayerGameSampleManager.ReturnToMenuReason.PlayerKicked);

			OnPlayerNotInLobby();
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}

	void UpdateLobby(Lobby updatedLobby)
	{
		//Debug.Log($"Update Lobby :: Old Players:{activeLobby.Players?.Count}, New Players:{updatedLobby.Players?.Count}");

		// Since this is called after an await, ensure that the Lobby wasn't closed while waiting.
		if (activeLobby == null || updatedLobby == null) return;

		//Debug.Log("Update Lobby :: Lobbies Valid");
		if (DidPlayersChange(activeLobby.Players, updatedLobby.Players))
		{
			Debug.Log("Update Lobby :: Players Changed - Lobby Updated!");
			activeLobby = updatedLobby;
			players = activeLobby?.Players;

			if (updatedLobby.Players.Exists(player => player.Id == playerId))
			{
				var isGameReady = IsGameReady(updatedLobby);

				OnLobbyChanged?.Invoke(updatedLobby, isGameReady);
				Debug.Log("Update Lobby :OnLobbyChanged Invoked");
			}
			else
			{
				Debug.Log("Update Lobby : Player Kicked");
				ServerlessMultiplayerGameSampleManager.instance.SetReturnToMenuReason(
					ServerlessMultiplayerGameSampleManager.ReturnToMenuReason.PlayerKicked);

				OnPlayerNotInLobby();
			}
		}
	}

	public void OnPlayerNotInLobby()
	{
		if (activeLobby != null)
		{
			activeLobby = null;

			OnPlayerNotInLobbyEvent?.Invoke();
		}
	}

	static bool DidPlayersChange(List<Player> oldPlayers, List<Player> newPlayers)
	{
		if (oldPlayers.Count != newPlayers.Count)
		{
			Debug.Log("Update Lobby :: DidPlayersChange :: Player Count Changed");
			return true;
		}

		for (int i = 0; i < newPlayers.Count; i++)
		{
			if (oldPlayers[i].Id != newPlayers[i].Id ||
				oldPlayers[i].Data[PlayerDictionaryData.isReadyKey].Value != newPlayers[i].Data[PlayerDictionaryData.isReadyKey].Value)
			{
				Debug.Log("Update Lobby :: DidPlayersChange :: Player ID/Ready State Changed");
				return true;
			}
		}

		return false;
	}

	static bool IsGameReady(Lobby lobby)
	{
		if (lobby.Players.Count <= 1)
		{
			return false;
		}

		foreach (var player in lobby.Players)
		{
			var isReady = bool.Parse(player.Data[PlayerDictionaryData.isReadyKey].Value);
			if (!isReady)
			{
				return false;
			}
		}

		return true;
	}

	public async Task<Lobby> CreateLobby(string lobbyName, int maxPlayers, string hostName,
			bool isPrivate, string relayJoinCode)
	{
		try
		{
			playerDictionaryData = new(hostName, false);
			isHost = true;
			wasGameStarted = false;

			await DeleteAnyActiveLobbyWithNotify();
			if (this == null) return default;

			var options = new CreateLobbyOptions
			{
				IsPrivate = isPrivate,
				Data = new Dictionary<string, DataObject>
				{
					{ hostNameKey, new DataObject(DataObject.VisibilityOptions.Public, hostName) },
					{ relayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode) },
				},

				Player = CreatePlayerData()
			};

			activeLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

			// Callbacks
			//LobbyEventCallbacks callbacks = new LobbyEventCallbacks();
			//callbacks.PlayerJoined += LobbyUI.instance.OnPlayerJoined;
			//callbacks.PlayerLeft += LobbyUI.instance.OnPlayerLeft;
			//try
			//{
			//	activeLobbyEvents = await LobbyService.Instance.SubscribeToLobbyEventsAsync(activeLobby.Id, callbacks);
			//}
			//catch (LobbyServiceException ex)
			//{
			//	switch (ex.Reason)
			//	{
			//		case LobbyExceptionReason.AlreadySubscribedToLobby: Debug.LogWarning($"Already subscribed to lobby[{activeLobby.Id}]. We did not need to try and subscribe again. Exception Message: {ex.Message}"); break;
			//		case LobbyExceptionReason.SubscriptionToLobbyLostWhileBusy: Debug.LogError($"Subscription to lobby events was lost while it was busy trying to subscribe. Exception Message: {ex.Message}"); throw;
			//		case LobbyExceptionReason.LobbyEventServiceConnectionError: Debug.LogError($"Failed to connect to lobby events. Exception Message: {ex.Message}"); throw;
			//		default: throw;
			//	}
			//}

			if (this == null) return default;

			players = activeLobby?.Players;

			UIManager.instance.ToggleHostView(false);
			LobbyUI.instance.Show();

			LogLobbyCreation(activeLobby);
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}

		return activeLobby;
	}

	public async Task<Lobby> JoinPrivateLobby(string lobbyJoinCode, string playerName)
	{
		try
		{
			await PrepareToJoinLobby(playerName);
			if (this == null) return default;

			var options = new JoinLobbyByCodeOptions();
			options.Player = CreatePlayerData();

			Debug.Log($"Joining lobby with Code {lobbyJoinCode}");
			activeLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyJoinCode, options);
			if (this == null) return default;

			players = activeLobby?.Players;
		}
		catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
		{
			if (this == null) return null;

			activeLobby = null;

			throw;
		}
		catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyFull)
		{
			if (this == null) return null;

			activeLobby = null;

			throw;
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}

		return activeLobby;
	}

	async Task PrepareToJoinLobby(string playerName)
	{
		isHost = false;
		this.wasGameStarted = false;
		this.playerDictionaryData = new(playerName, false);

		if (activeLobby != null)
		{
			Debug.Log("Already in a lobby when attempting to join so leaving old lobby.");
			await LeaveJoinedLobby();
		}
	}

	public async Task LeaveJoinedLobby()
	{
		try
		{
			await RemovePlayer(playerId);
			if (this == null) return;

			OnPlayerNotInLobby();
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}

	public async Task RemovePlayer(string playerId)
	{
		try
		{
			if (activeLobby != null)
			{
				await LobbyService.Instance.RemovePlayerAsync(activeLobby.Id, playerId);
				Debug.Log("Removed Player");
			}
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}

	public List<Player> GetLobbyPlayers()
	{
		if (activeLobby != null)
		{
			return activeLobby.Players;
		}
		else
		{
			return null;
		}
	}

	public void LogLobbyPlayers()
	{
		List<Player> players = GetLobbyPlayers();

		if (players == null)
		{
			Debug.Log("Players are null. Returning");
			return;
		}

		string lobbyPlayerNames = "Player(s) ";
		for (int i = 0; i < players.Count; i++)
		{
			lobbyPlayerNames += $"'{players[i].Data["playerName"].Value}'";

			if (i == players.Count - 1)
				lobbyPlayerNames += " are present";
			else
				lobbyPlayerNames += ", ";
		}

		Debug.Log(lobbyPlayerNames);
	}

	public async Task DeleteAnyActiveLobbyWithNotify()
	{
		try
		{
			if (activeLobby != null && isHost)
			{
				await LobbyService.Instance.DeleteLobbyAsync(activeLobby.Id);
				if (this == null) return;

				OnPlayerNotInLobby();
			}
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}

	Player CreatePlayerData()
	{
		var player = new Player();
		player.Data = CreatePlayerDictionary();

		return player;
	}

	Dictionary<string, PlayerDataObject> CreatePlayerDictionary()
	{
		var playerDictionary = new Dictionary<string, PlayerDataObject>
			{
				{ PlayerDictionaryData.playerNameKey,  new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerDictionaryData.playerName) },
				{ PlayerDictionaryData.isReadyKey,  new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerDictionaryData.isPlayerReady.ToString()) },
			};

		return playerDictionary;
	}

	public static void LogLobbyCreation(Lobby lobby)
	{
		if (lobby is null)
		{
			Debug.Log("No active lobby.");
			return;
		}

		var lobbyData = lobby.Data.Select(kvp => $"{kvp.Key} is {kvp.Value.Value}");
		var lobbyDataStr = string.Join(", ", lobbyData);

		Debug.Log($"Lobby Named:{lobby.Name}, " +
			$"Players:{lobby.Players.Count}/{lobby.MaxPlayers}, " +
			$"IsPrivate:{lobby.IsPrivate}, " +
			$"IsLocked:{lobby.IsLocked}, " +
			$"LobbyCode:{lobby.LobbyCode}, " +
			$"Id:{lobby.Id}, " +
			$"Created:{lobby.Created}, " +
			$"HostId:{lobby.HostId}, " +
			$"EnvironmentId:{lobby.EnvironmentId}, " +
			$"Upid:{lobby.Upid}, " +
			$"Lobby.Data: {lobbyDataStr}");

		instance.LogLobbyPlayers();
	}

	// Check if players connected already use our name
	// Since we are already connected to a lobby at this point, we must check for 2 matches instead of 1
	public bool PlayerNameCheck(string ownerName)
	{
		int matches = 0;
		List<Player> players = instance.GetLobbyPlayers();

		for (int i = 0; i < players.Count; i++)
		{
			Debug.Log($"Name Check: Found player '{players[i].Data["playerName"].Value}'");
			if (ownerName == players[i].Data["playerName"].Value)
			{
				matches++;
				Debug.Log($"Name Check: Name Matched! {players[i].Data["playerName"].Value} + {ownerName}, Count {matches}");

				if (matches == 2)
					return false;
			}
		}
		return true;
	}

	public void OnPlayersJoinedLobby(List<LobbyPlayerJoined> newPlayers)
	{
		if (isHost)
		{
			for (int i = 0; i < newPlayers.Count; i++)
			{
				Debug.Log($"Player '{newPlayers[i].Player.Data["playerName"].Value}' joined!");
			}
		}
	}
	public void OnPlayersLeftLobby(List<int> leftPlayers)
	{
		if (isHost)
		{
			for (int i = 0; i < leftPlayers.Count; i++)
			{
				Debug.Log($"Player '{leftPlayers[i]}' Left!");
			}
		}
	}
}
