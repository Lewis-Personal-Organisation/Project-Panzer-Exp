using Unity.Netcode;
using UnityEngine;

namespace HelloWorld
{
	public class Player : NetworkBehaviour
	{
		public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();
		//[SerializeField] private PlayerUI playerUI;
		[SerializeField] private UIManager helloWorldManager;
		[SerializeField] private NetworkData networkData;
		public NetworkData NetworkData => networkData;

		/// <summary>
		/// When a Player Object spawns, register event for Network Variable state change
		/// </summary>
		public override void OnNetworkSpawn()
		{
			Position.OnValueChanged += OnStateChanged;

			if (IsOwner)
			{
				helloWorldManager = GameObject.Find("UI Manager").GetComponent<UIManager>();
				//helloWorldManager.player = this;

				if (IsServer)
				{
					networkData = this.gameObject.AddComponent<NetworkData>();
					networkData.TrySubmitNewPlayerName(helloWorldManager.nameDisplayText.text);
				}

				Move();
			}
		}

		/// <summary>
		/// When we leave the network, unasign our event
		/// </summary>
		public override void OnNetworkDespawn()
		{
			Position.OnValueChanged -= OnStateChanged;
		}

		/// <summary>
		/// When Position Network Variable changes, update our local position with its value
		/// </summary>
		public void OnStateChanged(Vector3 previous, Vector3 current)
		{
			// note: `Position.Value` will be equal to `current` here
			if (Position.Value != previous)
			{
				transform.position = Position.Value;
			}
		}

		/// <summary>
		/// Ask the server to change position of our transform.
		/// </summary>
		public void Move()
		{
			if (IsHost)
			{
				var randomPosition = GetRandomPositionOnPlane();
				transform.position = randomPosition;
				Position.Value = randomPosition;
			}
			else if (IsClient)
			{
				SubmitPositionRequestServerRpc();
			}
		}

		/// <summary>
		/// Ask the server to change position of our transform.
		/// </summary>
		public void MoveAsServer()
		{
			var randomPosition = GetRandomPositionOnPlane();
			transform.position = randomPosition;
			Position.Value = randomPosition;
		}

		/// <summary>
		/// The method called from clients to server, requesting a position change.
		/// Creates a new position,  apply's it locally to the Servers Player (because it is the server), updates the Position Net Var so all players can update their position
		/// </summary>
		[Rpc(SendTo.Server)]
		void SubmitPositionRequestServerRpc(RpcParams rpcParams = default)
		{
			var randomPosition = GetRandomPositionOnPlane();
			transform.position = randomPosition;
			Position.Value = randomPosition;
		}

		/// <summary>
		/// Returns a random position on the X and Y
		/// </summary>
		/// <returns></returns>
		static Vector3 GetRandomPositionOnPlane()
		{
			return new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
		}

		[Rpc(SendTo.Server)]
		public void RequestPlayerNamesServerRpc(RpcParams rpcParams = default)
		{
			// Get the server Net Obj => HelloWorldPlayer. GetPlayerNetworkObject() method is only valid when called on the Server
			var serverPlayer = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(0).GetComponent<HelloWorld.Player>();

			// Using our (the servers) Network Data component, retrieve the fresh list of Player Names
			SendPlayerNamesClientRpc(serverPlayer.NetworkData.GetPlayerNames());
		}

		[Rpc(SendTo.ClientsAndHost)]
		void SendPlayerNamesClientRpc(string playerNames)
		{
			Debug.Log($"Client Recieved names! -> {playerNames}");
		}
	}
}