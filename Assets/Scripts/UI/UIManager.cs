using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using Unity.Netcode;
using Unity.Services.Relay.Models;
using Unity.Services.Relay;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
	[Header("Lobby Data")]
	[SerializeField] UnityTransport unityTransport;
	[SerializeField] private bool isLobbyPrivate;
	string lobbyDisplayName = "";
	string hostLobbyName;
	public int maxPlayers = 4;
	public string relayJoinCode = string.Empty;
	public bool networkInitialised = false;
	public string playerName;

	[Header("Player Name Input")]
	[SerializeField] private GameObject playerNameGroup;
	[SerializeField] private TextMeshProUGUI inputText;
	[SerializeField] internal TextMeshProUGUI nameDisplayText;
	[SerializeField] internal UnityEngine.UI.Button nameDisplayButton;
	[SerializeField] private UnityEngine.UI.Button submitButton;

	[Header("Game Connection")]
	[SerializeField] private GameObject inaccessableBackgroundObject;
	[SerializeField] private Button hostGameButton;
	[SerializeField] private Button joinPrivateGameButton;
	[SerializeField] private Button joinPublicGameButton;

	[Header("Host Lobby")]
	[SerializeField] private GameObject hostLobbyPanel;
	[SerializeField] private TextMeshProUGUI gameNameText;
	[SerializeField] private Button privateButton;
	[SerializeField] private Button publicButton;
	[SerializeField] private Button confirmButton;

	// The callback for Update method
	private UnityAction OnUpdate;


	private void Awake()
	{
		hostGameButton.onClick.AddListener(OnHostButtonPressed);
		joinPrivateGameButton.onClick.AddListener (OnJoinPrivateGameButtonPressed);
		joinPublicGameButton.onClick.AddListener(OnJoinPublicGameButtonPressed);

		privateButton.onClick.AddListener(delegate { SetHostLobbyVisiblity(false); });
		publicButton.onClick.AddListener(delegate { SetHostLobbyVisiblity(true); });
		confirmButton.onClick.AddListener(OnHostConfirmLobbyPressed);

		TogglePlayerNameGroup(true);
	}

	private void Update()
	{
		OnUpdate?.Invoke();
	}

	public void OnHostButtonPressed()
	{
		ToggleGameConnectionButtonVisiblity(false);
		ToggleHostLobbyPanel(true);
		publicButton.Select();
		isLobbyPrivate = false;
		lobbyDisplayName = $"{nameDisplayText.text}'s Test Lobby";
		gameNameText.text = lobbyDisplayName;
	}

	public void OnJoinPrivateGameButtonPressed()
	{
		ToggleGameConnectionButtonVisiblity(false);
	}

	public void OnJoinPublicGameButtonPressed()
	{
		ToggleGameConnectionButtonVisiblity(false);
	}

	private async void OnHostConfirmLobbyPressed()
	{
		privateButton.interactable = false;
		publicButton.interactable = false;
		confirmButton.interactable = false;

		relayJoinCode = await InitializeHost(maxPlayers);
		if (this == null) return;

		Lobby lobby = await LobbyManager.instance.CreateLobby(hostLobbyName, maxPlayers, playerName, isLobbyPrivate, relayJoinCode);
		if (this == null ) return;


	}

	/// <summary>
	/// Submit name entered by the user. If its empty, return. Else, set as our displayed name and Connection Data for joining a server
	/// If we're submitting after a disconnect, auto-rejoin as client with the new name. Else, hide the name input and show Connection buttons
	/// </summary>
	public void SubmitName()
	{
		if (inputText.text.Length == 0)
		{
			Debug.Log("Text can't be empty");
			return;
		}

		// Show main Buttons ToggleVEDisplay(true);
		nameDisplayText.text = inputText.text;
		nameDisplayText.gameObject.SetActive(true);
		// Sets the Name as connection data //connectionHandler.SetConnectionData(System.Text.Encoding.ASCII.GetBytes(inputText.text));

		TogglePlayerNameGroup(false);
	}

	public void TogglePlayerNameGroup(bool state)
	{
		playerNameGroup.SetActive(state);
		inaccessableBackgroundObject.SetActive(state);
		hostGameButton.interactable = !state;
		joinPrivateGameButton.interactable = !state;
		joinPublicGameButton.interactable = !state;

		if (state)
		{
			OnUpdate += TakeKeyboardInput;
		}
		else
		{
			OnUpdate -= TakeKeyboardInput;
		}
	}

	private void TakeKeyboardInput()
	{
		foreach (char chr in Input.inputString)
		{
			// If character is Letter or Number and the current length is not more than 10, update text
			if ((Char.IsLetter(chr) || Char.IsDigit(chr)) && inputText.text.Length + 1 <= 10)
			{
				string temp = inputText.text + chr;
				inputText.text = temp;
			}
			// If Char is any return key, attempt to submit name
			else if (chr == '\r' && inputText.text.Length > 0)
			{
				SubmitName();
			}
			// If char is backspace and we have minimum 1 char, remove char
			else if (chr == '\b' && inputText.text.Length > 0)
			{
				string temp = inputText.text.Remove(inputText.text.Length - 1);
				inputText.text = temp;
			}
		}
	}

	public void ToggleGameConnectionButtonVisiblity(bool state)
	{
		hostGameButton.gameObject.SetActive(state);
		joinPrivateGameButton.gameObject.SetActive(state);
		joinPublicGameButton.gameObject.SetActive(state);
	}

	private void ToggleHostLobbyPanel(bool state)
	{
		hostLobbyPanel.SetActive(state);
	}

	private void SetHostLobbyVisiblity(bool state)
	{
		isLobbyPrivate = !state;
	}

	public async Task<string> InitializeHost(int maxPlayerCount)
	{
		Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayerCount);
		var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

		NetworkEndpoint endPoint = NetworkEndpoint.Parse(allocation.RelayServer.IpV4,
			(ushort)allocation.RelayServer.Port);

		var ipAddress = endPoint.Address.Split(':')[0];

		unityTransport.SetHostRelayData(ipAddress, endPoint.Port,
			allocation.AllocationIdBytes, allocation.Key,
			allocation.ConnectionData, false);

		Debug.Log($"Initialized Relay Host and received join code: {joinCode}");

		NetworkManager.Singleton.StartHost();

		networkInitialised = true;

		return joinCode;
	}
}