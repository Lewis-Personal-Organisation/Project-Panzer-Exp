using UnityEngine;
using Unity.Netcode;

public class LobbyPlayer : NetworkBehaviour
{
	[SerializeField] private NetworkData networkData;
	public NetworkData NetworkData => networkData;

	//public override void OnNetworkSpawn()
	//{
	//	if (IsOwner)
	//	{
	//		if (IsServer)
	//		{
	//			networkData.TrySubmitNewPlayerName(UIManager.instance.nameDisplayText.text);
	//		}
	//	}
	//}
}