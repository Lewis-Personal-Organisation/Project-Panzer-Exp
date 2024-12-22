using UnityEngine;
using Unity.Netcode;
using System;

public class ConnectionAwareActioner : NetworkBehaviour
{
	public Action OnIsServer;
	public Action OnIsHost;
	public Action OnIsClient;

	public override void OnNetworkSpawn()
	{

	}

	public void Action(string text)
	{
		Debug.Log("Hello!");
	}
}
