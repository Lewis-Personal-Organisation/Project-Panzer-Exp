using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Unity.Services.Samples.ServerlessMultiplayerGame;
using System.Linq;

[DisallowMultipleComponent]
public class LobbyManager : MonoBehaviour
{
	public static LobbyManager instance { get; private set; }
	public Lobby activeLobby { get; private set; }
	public static string playerId => AuthenticationService.Instance.PlayerId;
	public List<Player> players { get; private set; }
	public int numPlayers => players.Count;
	public bool isHost { get; private set; }
	public const string playerNameKey = "playerName";
	public const string isReadyKey = "isReady";
	public const string hostNameKey = "hostName";
	public const string relayJoinCodeKey = "relayJoinCode";
	public static event Action<Lobby, bool> OnLobbyChanged;
	public static event Action OnPlayerNotInLobbyEvent;

	float nextHostHeartbeatTime;
	const float hostHeartbeatFrequency = 15;
	float nextUpdatePlayersTime;
	const float updatePlayersFrequency = 1.5F;
	string playerName;
	bool isPlayerReady = false;
	bool wasGameStarted = false;


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

    async void  Update()
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

			UpdateLobby(updatedLobby);
		}

		// Handle lobby no longer exists (host canceled game and returned to main menu).
		catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
		{
			if (this == null) return;

			// Lobby has closed
			ServerlessMultiplayerGameSampleManager.instance.SetReturnToMenuReason(
				ServerlessMultiplayerGameSampleManager.ReturnToMenuReason.LobbyClosed);

			OnPlayerNotInLobby();
		}

		// Handle player no longer allowed to view lobby (host booted player so player is no longer in the lobby).
		catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.Forbidden)
		{
			if (this == null) return;

			ServerlessMultiplayerGameSampleManager.instance.SetReturnToMenuReason(
				ServerlessMultiplayerGameSampleManager.ReturnToMenuReason.PlayerKicked);

			OnPlayerNotInLobby();
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}

	void UpdateLobby(Lobby updatedLobby)
	{
		// Since this is called after an await, ensure that the Lobby wasn't closed while waiting.
		if (activeLobby == null || updatedLobby == null) return;

		if (DidPlayersChange(activeLobby.Players, updatedLobby.Players))
		{
			activeLobby = updatedLobby;
			players = activeLobby?.Players;

			if (updatedLobby.Players.Exists(player => player.Id == playerId))
			{
				var isGameReady = IsGameReady(updatedLobby);

				OnLobbyChanged?.Invoke(updatedLobby, isGameReady);
			}
			else
			{
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
			return true;
		}

		for (int i = 0; i < newPlayers.Count; i++)
		{
			if (oldPlayers[i].Id != newPlayers[i].Id ||
				oldPlayers[i].Data[isReadyKey].Value != newPlayers[i].Data[isReadyKey].Value)
			{
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
			var isReady = bool.Parse(player.Data[isReadyKey].Value);
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
			isHost = true;
			playerName = hostName;
			wasGameStarted = false;
			isPlayerReady = false;

			await DeleteAnyActiveLobbyWithNotify();
			if (this == null) return default;

			var options = new CreateLobbyOptions();
			options.IsPrivate = isPrivate;
			options.Data = new Dictionary<string, DataObject>
				{
					{ hostNameKey, new DataObject(DataObject.VisibilityOptions.Public, hostName) },
					{ relayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode) },
				};

			options.Player = CreatePlayerData();

			activeLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
			if (this == null) return default;

			players = activeLobby?.Players;

			Log(activeLobby);
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}

		return activeLobby;
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
				{ playerNameKey,  new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) },
				{ isReadyKey,  new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, isPlayerReady.ToString()) },
			};

		return playerDictionary;
	}

	public static void Log(Lobby lobby)
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
			$"Lobby.Data:{lobbyDataStr}");
	}
}
