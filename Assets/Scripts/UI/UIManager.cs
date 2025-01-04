using System;
using System.Collections.Generic;
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
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using Unity.Services.Lobbies;
using Mono.Cecil.Cil;

public class UIManager : MonoBehaviour
{
	public static UIManager instance;

	public enum TextSubmissionContext
	{
		PlayerName,
		RelayJoinCode
	}
	private enum ConnectionErrorTypes
	{
		PlayerCapacityReached,
		UsernameTaken,
		NotImplemented
	}
	[SerializeField] private TextSubmissionContext textInputContext;

	[Header("Session")]
	[SerializeField] private SessionManager sessionManager;

	[Header("Connection Handler")]
	[SerializeField] private ConnectionHandler connectionHandler;

	[Header("Lobby Data")]
	[SerializeField] UnityTransport unityTransport;
	[SerializeField] private bool isLobbyPrivate;
	[SerializeField] string hostLobbyName = "";
	public int maxPlayers = 4;
	public string relayJoinCode = string.Empty;
	const int relayCodeMaxLength = 6;
	readonly Regex relayCodeRegex = new Regex("^[A-Z0-9]*$");
	private bool networkManagerInitialised = false;
	private string playerName;
	public LobbyPlayer playerTest;

	private bool GameCodeIsValid(string gameCode) => gameCode.Length == relayCodeMaxLength && relayCodeRegex.IsMatch(gameCode);
	private int MaxInputTextLength => textInputContext == TextSubmissionContext.PlayerName ? 10 : relayCodeMaxLength;

	[Header("Player Name Input")]
	[SerializeField] private GameObject playerNameGroup;
	[SerializeField] internal TextMeshProUGUI nameDisplayText;
	[SerializeField] internal Button nameDisplayButton;

	[Header("Text Input")]
	[SerializeField] private TextMeshProUGUI inputText;
	[SerializeField] private TextMeshProUGUI inputTitle;
	[SerializeField] private Button submitButton;
	[SerializeField] private Button pasteButton;
	[SerializeField] private GameObject inputLoadingAnimationObject;

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
	[SerializeField] private Button closeLobbyButton;
	[SerializeField] private GameObject requestingLobbyGameObject;

	[Header("Join Code UI")]
	[SerializeField] private GameObject joinCodeGroup;
	[SerializeField] private Button joinCodeCopyButton;
	[SerializeField] private TextMeshProUGUI joinCodeText;

	// The callback for Update method
	private UnityAction OnUpdate;


	private void Awake()
	{
		instance = this;

		hostGameButton.onClick.AddListener(OnHostButtonPressed);
		joinPrivateGameButton.onClick.AddListener (OnJoinPrivateGameButtonPressed);
		joinPublicGameButton.onClick.AddListener(OnJoinPublicGameButtonPressed);

		privateButton.onClick.AddListener(delegate { SetHostLobbyVisiblity(false); });
		publicButton.onClick.AddListener(delegate { SetHostLobbyVisiblity(true); });
		confirmButton.onClick.AddListener(OnHostConfirmLobbyPressed);
		closeLobbyButton.onClick.AddListener(OnHostCloseLobbyPressed);

		submitButton.onClick.AddListener(SubmitTextInput);
		pasteButton.onClick.AddListener(OnPaste);

		joinCodeCopyButton.onClick.AddListener(CopyJoinCode);

		ToggleInputTextGroup(true, TextSubmissionContext.PlayerName);
	}

	private void Update()
	{
		OnUpdate?.Invoke();
	}

	public void ToggleInputTextGroup(bool state, TextSubmissionContext context = TextSubmissionContext.PlayerName)
	{
		playerNameGroup.SetActive(state);
		inaccessableBackgroundObject.SetActive(state);
		hostGameButton.interactable = !state;
		joinPrivateGameButton.interactable = !state;
		joinPublicGameButton.interactable = !state;

		if (state)
		{
			inputText.text = string.Empty;
			if (context == TextSubmissionContext.PlayerName)
			{
				inputTitle.text = "Enter a Username";
				pasteButton.gameObject.SetActive(false);
			}
			else
			{
				inputTitle.text = "Enter Join Code";
				pasteButton.gameObject.SetActive(true);
			}

			textInputContext = context;

			OnUpdate += TakeKeyboardInput;
		}
		else
		{
			OnUpdate -= TakeKeyboardInput;
		}
	}
	public void ToggleHostView(bool state)
	{
		hostLobbyPanel.SetActive(state);
	}

	private void TakeKeyboardInput()
	{
		foreach (char chr in Input.inputString)
		{
			// If character is Letter or Number and the current length is not more than 10, update text
			if((Char.IsLetter(chr) || Char.IsDigit(chr)) && inputText.text.Length + 1 <= MaxInputTextLength)
			{
				string temp = inputText.text + chr;
				inputText.text = temp;
			}
			// If Char is any return key, attempt to submit name
			else if (chr == '\r' && inputText.text.Length > 0)
			{
				SubmitTextInput();
			}
			// If char is backspace and we have minimum 1 char, remove char
			else if (chr == '\b' && inputText.text.Length > 0)
			{
				string temp = inputText.text.Remove(inputText.text.Length - 1);
				inputText.text = temp;
			}
		}
	}

	private void OnPaste()
	{
		inputText.text = GUIUtility.systemCopyBuffer;
	}

	public void SubmitTextInput()
	{
		if (inputText.text.Length == 0)
		{
			Debug.Log("Text can't be empty");
			return;
		}

		switch (textInputContext)
		{
			case TextSubmissionContext.PlayerName:
				nameDisplayText.text = inputText.text;
				nameDisplayText.gameObject.SetActive(true);
				//connectionHandler.SetConnectionData(System.Text.Encoding.ASCII.GetBytes(inputText.text));

				if (LobbyManager.lobbyPreviouslyRefusedUsername)
				{
					JoinPrivateLobbyAsClient(relayJoinCode);
				}
				break;
			case TextSubmissionContext.RelayJoinCode:
				if (GameCodeIsValid(inputText.text))
				{
					relayJoinCode = inputText.text;
					inputLoadingAnimationObject.SetActive(true);
					JoinPrivateLobbyAsClient(relayJoinCode);
				}
				break;
		}

		// Show main Buttons ToggleVEDisplay(true);

		// Sets the Name as connection data //connectionHandler.SetConnectionData(System.Text.Encoding.ASCII.GetBytes(inputText.text));

		ToggleInputTextGroup(false);
		inputText.text = string.Empty;
	}

	public void ToggleGameConnectionButtonVisiblity(bool state)
	{
		hostGameButton.gameObject.SetActive(state);
		joinPrivateGameButton.gameObject.SetActive(state);
		joinPublicGameButton.gameObject.SetActive(state);
	}

	private void CopyJoinCode()
	{

		GUIUtility.systemCopyBuffer = joinCodeText.text;
	}

	public void OnHostButtonPressed()
	{
		ToggleGameConnectionButtonVisiblity(false);
		ToggleHostLobbyPanel(true);
		publicButton.Select();
		isLobbyPrivate = false;
		gameNameText.text = $"{nameDisplayText.text}'s Lobby";
	}

	public void OnJoinPrivateGameButtonPressed()
	{
		ToggleGameConnectionButtonVisiblity(false);
		ToggleInputTextGroup(true, TextSubmissionContext.RelayJoinCode);
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
		requestingLobbyGameObject.SetActive(true);

		// Init Unity Services
		await sessionManager.InitialiseUnityServices();

		// Start Hosting using Relay
		relayJoinCode = await InitialiseHostWithRelay(maxPlayers);
		if (this == null) return;

		// Create lobby with Relay
		playerName = nameDisplayText.text;
		Lobby lobby = await LobbyManager.instance.CreateLobby(gameNameText.text, maxPlayers, playerName, isLobbyPrivate, relayJoinCode);
		if (this == null) return;

		// Enforce approval is everything is sucessfull
		//NetworkManager.Singleton.ConnectionApprovalCallback = connectionHandler.ApprovalCheck;

		requestingLobbyGameObject.SetActive(false);
		closeLobbyButton.gameObject.SetActive(true);
		closeLobbyButton.interactable = true;

		joinCodeText.text = lobby.LobbyCode;
		joinCodeGroup.SetActive(true);
	}

	private async void OnHostCloseLobbyPressed()
	{
		await LobbyManager.instance.DeleteAnyActiveLobbyWithNotify();
		closeLobbyButton.gameObject.SetActive(false);
	}

	private void ToggleHostLobbyPanel(bool state)
	{
		hostLobbyPanel.SetActive(state);
	}

	private void SetHostLobbyVisiblity(bool state)
	{
		isLobbyPrivate = !state;
	}

	public async Task<string> InitialiseHostWithRelay(int maxPlayerCount)
	{
		Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayerCount);
		var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

		NetworkEndpoint endPoint = NetworkEndpoint.Parse(allocation.RelayServer.IpV4,
			(ushort)allocation.RelayServer.Port);

		var ipAddress = endPoint.Address.Split(':')[0];

		unityTransport.SetHostRelayData(ipAddress, endPoint.Port,
			allocation.AllocationIdBytes, allocation.Key,
			allocation.ConnectionData, false);

		NetworkManager.Singleton.StartHost();

		networkManagerInitialised = true;

		return joinCode;
	}

	public async void JoinPrivateLobbyAsClient(string joinCode)
	{
		try
		{
			if (sessionManager.playerIsSetup == false)
				await sessionManager.InitialiseUnityServices();

			//LobbyManager.instance.LogLobbyPlayers();

			Lobby joinedLobby = await LobbyManager.instance.JoinPrivateLobby(joinCode, nameDisplayText.text);

			if (this == null)
			{
				Debug.Log("Null. Returning");
				return;
			}

			if (joinedLobby == null)
			{
				Debug.Log("Failed to Join Private Lobby");
				return;
			}

			inputLoadingAnimationObject.SetActive(false);
			Debug.Log($"Checking Name {nameDisplayText.text}");
			LobbyManager.instance.LogLobbyPlayers();

			if (LobbyManager.instance.activeLobby == null)
				return;

			bool nameCheckPassed = LobbyManager.instance.PlayerNameCheck(nameDisplayText.text);

			if (nameCheckPassed)
			{
				LobbyManager.lobbyPreviouslyRefusedUsername = false;
				Debug.Log("Name Check: Passed");
				LobbyManager.instance.LogLobbyPlayers();
				await OpenLobby(joinedLobby);
			}
			else
			{
				LobbyManager.lobbyPreviouslyRefusedUsername = true;
				Debug.Log("Name Check: Failed. Leaving Lobby...");
				await LobbyManager.instance.LeaveJoinedLobby();
				Debug.Log("Name Check: Left Lobby. Set a unique name and rejoin!");
				ToggleInputTextGroup(true, TextSubmissionContext.PlayerName);
				pasteButton.gameObject.SetActive(false);
			}
		}
		catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
		{
			Debug.Log("Failed to Join Private Lobby: Invalid Code");
		}
		catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyFull)
		{
			Debug.Log("Failed to Join Private Lobby: Lobby Full");
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}

	async Task OpenLobby(Lobby lobbyJoined)
	{
		Debug.Log("Lobby Data Retrieved. Initializing Client");

		try
		{
			await InitializeRelayClient(lobbyJoined);

			if (this == null)
			{
				Debug.Log("Null. Returning");
				return;
			}

			LobbyUI.instance.Show();
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}

	async Task InitializeRelayClient(Lobby lobbyJoined)
	{
		try
		{
			var relayJoinCode = lobbyJoined.Data[LobbyManager.relayJoinCodeKey].Value;
			await InitializeClient(relayJoinCode);
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}

	public async Task InitializeClient(string relayJoinCode)
	{
		var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
		var endPoint = NetworkEndpoint.Parse(joinAllocation.RelayServer.IpV4,
			(ushort)joinAllocation.RelayServer.Port);

		var ipAddress = endPoint.Address.Split(':')[0];

		unityTransport.SetClientRelayData(ipAddress, endPoint.Port,
			joinAllocation.AllocationIdBytes, joinAllocation.Key,
			joinAllocation.ConnectionData, joinAllocation.HostConnectionData, false);

		NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
		NetworkManager.Singleton.StartClient();

		Debug.Log("Relay Allocation complete. Starting as Client");

		networkManagerInitialised = true;
	}


	private void OnClientConnected(ulong obj)
	{
		LobbyManager.instance.LogLobbyPlayers();
	}

	//private void OnHostConnected(ulong obj)
	//{
	//LobbyManager.instance.LogLobbyPlayers();
	//}


}