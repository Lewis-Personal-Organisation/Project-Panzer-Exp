using HelloWorld;
using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class HelloWorldManager : MonoBehaviour
{
	[SerializeField] private UIDocument uiDocument;
	VisualElement rootVisualElement;
	Button hostButton;
	Button clientButton;
	Button serverButton;
	Button moveButton;
	Label statusLabel;
	private bool uiCreated = false;

	[Header("Player Name Input")]
	[SerializeField] private GameObject playerNameGroup;
	[SerializeField] private TextMeshProUGUI inputText;
	[SerializeField] internal TextMeshProUGUI nameDisplay;
	[SerializeField] private Button submitButton;

	public HelloWorldPlayer helloWorldPlayer;
	[SerializeField] private ConnectionHandler connectionHandler;


	void Update()
	{
		TakeKeyboardInput();

		if (uiCreated)
			UpdateUI();
	}

	void OnDisable()
	{
		hostButton.clicked -= connectionHandler.OnHostButtonClicked;
		clientButton.clicked -= connectionHandler.OnClientButtonClicked;
		serverButton.clicked -= connectionHandler.OnServerButtonClicked;
		moveButton.clicked -= SubmitNewPosition;
	}

	/// <summary>
	/// Create the visual element display
	/// </summary>
	void CreateVisualDisplay()
	{
		rootVisualElement = uiDocument.rootVisualElement;

		hostButton = CreateButton("HostButton", "Host");
		clientButton = CreateButton("ClientButton", "Client");
		serverButton = CreateButton("ServerButton", "Server");
		moveButton = CreateButton("MoveButton", "Move");
		statusLabel = CreateLabel("StatusLabel", "Not Connected");

		rootVisualElement.Clear();
		rootVisualElement.Add(hostButton);
		rootVisualElement.Add(clientButton);
		rootVisualElement.Add(serverButton);
		rootVisualElement.Add(moveButton);
		rootVisualElement.Add(statusLabel);

		hostButton.clicked += connectionHandler.OnHostButtonClicked;
		clientButton.clicked += connectionHandler.OnClientButtonClicked;
		serverButton.clicked += connectionHandler.OnServerButtonClicked;
		moveButton.clicked += SubmitNewPosition;
		uiCreated = true;
	}

	/// <summary>
	/// Show or Hide the Visual Element display
	/// </summary>
	/// <param name="enable"></param>
	private void ToggleVEDisplay(bool enable)
	{
		rootVisualElement.style.display = enable == true ? DisplayStyle.Flex : DisplayStyle.None;
		rootVisualElement.SetEnabled(enable);
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
			// If char is backspace and we have minimum 1 char, remove char
			else if (chr == '\b' && inputText.text.Length > 0) 
			{
				string temp = inputText.text.Remove(inputText.text.Length - 1);
				inputText.text = temp;
			}
		}
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

		nameDisplay.text = inputText.text;
		connectionHandler.SetConnectionData(System.Text.Encoding.ASCII.GetBytes(inputText.text));
		//NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes(inputText.text);

		// If we have previously disconnected, we should auto-join as client when we submit
		if (connectionHandler.previousConnectAttemptRejected)
		{
			connectionHandler.OnClientButtonClicked();
		}
		else
		{
			playerNameGroup.SetActive(false);
			CreateVisualDisplay();
		}
	}

	// Disclaimer: This is not the recommended way to create and stylize the UI elements, it is only utilized for the sake of simplicity.
	// The recommended way is to use UXML and USS. Please see this link for more information: https://docs.unity3d.com/Manual/UIE-USS.html
	private Button CreateButton(string name, string text)
	{
		var button = new Button();
		button.name = name;
		button.text = text;
		button.style.width = 240;
		button.style.backgroundColor = Color.white;
		button.style.color = Color.black;
		button.style.unityFontStyleAndWeight = FontStyle.Bold;
		return button;
	}

	private Label CreateLabel(string name, string content)
	{
		var label = new Label();
		label.name = name;
		label.text = content;
		label.style.color = Color.black;
		label.style.fontSize = 18;
		return label;
	}

	void UpdateUI()
	{
		if (NetworkManager.Singleton == null)
		{
			SetStartButtons(false);
			SetMoveButton(false);
			SetStatusText("NetworkManager not found");
			return;
		}

		if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
		{
			SetStartButtons(true);
			SetMoveButton(false);
			SetStatusText("Not connected");
		}
		else
		{
			SetStartButtons(false);
			SetMoveButton(true);
			UpdateStatusLabels();
		}
	}

	void SetStartButtons(bool state)
	{
		hostButton.style.display = state ? DisplayStyle.Flex : DisplayStyle.None;
		clientButton.style.display = state ? DisplayStyle.Flex : DisplayStyle.None;
		serverButton.style.display = state ? DisplayStyle.Flex : DisplayStyle.None;
	}

	void SetMoveButton(bool state)
	{
		moveButton.style.display = state ? DisplayStyle.Flex : DisplayStyle.None;
		if (state)
		{
			moveButton.text = NetworkManager.Singleton.IsServer ? "Move" : "Request Position Change";
		}
	}

	public void TogglePlayerNameGroup(bool state)
	{
		playerNameGroup.SetActive(state);
	}

	void SetStatusText(string text) => statusLabel.text = text;

	void UpdateStatusLabels()
	{
		var mode = NetworkManager.Singleton.IsHost ? "Host" : NetworkManager.Singleton.IsServer ? "Server" : "Client";
		string transport = "Transport: " + NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetType().Name;
		string modeText = "Mode: " + mode;
		SetStatusText($"{transport}\n{modeText}");
	}

	void SubmitNewPosition()
	{
		if (NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
		{
			foreach (ulong uid in NetworkManager.Singleton.ConnectedClientsIds)
			{
				var playerObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(uid);
				var player = playerObject.GetComponent<HelloWorld.HelloWorldPlayer>();
				player.Move();
			}
		}
		else if (NetworkManager.Singleton.IsClient)
		{
			Debug.Log("Client should print this");
			var playerObject = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
			var player = playerObject.GetComponent<HelloWorld.HelloWorldPlayer>();
			player.Move();
		}
	}

	public void UpdateUIOnDisconnect()
	{
		ToggleVEDisplay(false);
		playerNameGroup.SetActive(true);
		nameDisplay.text = string.Empty;
		inputText.text = string.Empty;
	}
}