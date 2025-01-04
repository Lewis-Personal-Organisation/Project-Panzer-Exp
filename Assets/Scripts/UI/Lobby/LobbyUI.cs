using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Matchmaker.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
	public static LobbyUI instance;

	[SerializeField] private GameObject parent;

	[SerializeField] private TextMeshProUGUI title;

	LobbyManager lobbyManager => LobbyManager.instance;
	bool isHost => lobbyManager.isHost;

	[SerializeField] private PlayerSlot[] playerSlots = new PlayerSlot[4];

	[SerializeField]
	[System.Serializable]
	private class PlayerSlot
	{
		[SerializeField] private GameObject gameObject;
		[SerializeField] private Image backImage;
		[SerializeField] private TextMeshProUGUI playerNameTitle;

		public bool IsFree() => !gameObject.activeInHierarchy;

		public void Show(string playerName)
		{
			backImage.color = UnityEngine.Random.ColorHSV();
			playerNameTitle.text = playerName;
			gameObject.SetActive(true);
		}

		public void Hide()
		{
			backImage.color = Color.white;
			playerNameTitle.text = string.Empty;
			gameObject.SetActive(false);
		}
	}


	private void Awake()
	{
		instance = this;
	}

	void OnLobbyChanged(Lobby updatedLobby, bool isGameReady)
	{
		if (isHost)
		{
			Debug.Log("OnLobbyChanged :: Host");
			OnHostLobbyChanged(updatedLobby, isGameReady);
		}
		else
		{
			Debug.Log("OnLobbyChanged :: Client");
			OnJoinLobbyChanged(updatedLobby, isGameReady);
		}
	}

	public void OnHostLobbyChanged(Lobby updatedLobby, bool isGameReady)
	{
		//sceneView.SetHostLobbyPlayers(updatedLobby.Players);

		if (isGameReady)
		{
			NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
		}

		//LobbyManager.instance.LogLobbyPlayers();
	}

	public void OnJoinLobbyChanged(Lobby updatedLobby, bool isGameReady)
	{
		//sceneView.SetJoinLobbyPlayers(updatedLobby.Players);
	}

	public void OnPlayerNotInLobby()
	{
		Debug.Log($"This player is no longer in the lobby so returning to main menu.");

		ReturnToMainMenu();
	}

	void ReturnToMainMenu()
	{
		//SceneManager.LoadScene("ServerlessMultiplayerGameSample");
	}

	public void Show()
	{
		//LobbyManager.OnLobbyChanged += OnLobbyChanged;
		//LobbyManager.OnPlayerNotInLobbyEvent += OnPlayerNotInLobby;

		parent.SetActive(true);
		SetLobbyTitle();
		AdjustPlayerSlots();
	}

	private void SetLobbyTitle()
	{
		title.text = isHost ? "Host Game Lobby" : "Game Lobby";
	}

	public void OnPlayerJoined(List<LobbyPlayerJoined> newPlayers)
	{
		AdjustPlayerSlots();
		//if (isHost)
		//{
		//	List<Unity.Services.Lobbies.Models.Player> activePlayers = lobbyManager.GetLobbyPlayers();

		//	for (int i = 0; i < playerSlots.Length; i++)
		//	{
		//		if (i < activePlayers.Count)
		//			playerSlots[i].Show(activePlayers[i].Data["playerName"].Value);
		//		else
		//			playerSlots[i].Hide();
		//	}
		//}
	}

	public void AdjustPlayerSlots()
	{
		List<Unity.Services.Lobbies.Models.Player> activePlayers = lobbyManager.GetLobbyPlayers();

		for (int i = 0; i < playerSlots.Length; i++)
		{
			if (i < activePlayers.Count)
				playerSlots[i].Show(activePlayers[i].Data["playerName"].Value);
			else
				playerSlots[i].Hide();
		}
	}

	public void OnPlayerLeft(List<int> removedPlayerIndexes)
	{
		AdjustPlayerSlots();
		//if (isHost)
		//{
		//	for (int i = 0; i < removedPlayerIndexes.Count; i++)
		//	{
		//		Debug.Log($"Player '{removedPlayerIndexes[i]}' Left!");
		//	}
		//}
	}
}
