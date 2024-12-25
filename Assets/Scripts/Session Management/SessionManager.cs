using UnityEngine;
using Unity.Services.Core;
using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using System.Linq.Expressions;

public class SessionManager : MonoBehaviour
{
	public bool useUnityServices = false;
	public PlayerInfoData playerInfo;

	[System.Serializable]
	public class PlayerInfoData
	{
		public DateTime? creationTime;
		public string ID;
		public string username;
	}

	public async Task Initialise()
	{
		try
		{
			await UnityServices.InitializeAsync();
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}

		AuthenticationService.Instance.SignInFailed += (err) => Debug.LogError($"Unity Relay :: {err}");
		AuthenticationService.Instance.SignedOut += () => Debug.Log($"Unity Relay :: Player signed out.");
		AuthenticationService.Instance.Expired += () => Debug.Log($"Unity Relay :: Player session could not be refreshed and expired.");
		AuthenticationService.Instance.SignedIn += () => Debug.Log($"Unity Relay :: Player Signed in. ID: {AuthenticationService.Instance.PlayerId}");

		try
		{
			await AuthenticationService.Instance.SignInAnonymouslyAsync();
			playerInfo = new PlayerInfoData();
			playerInfo.creationTime = AuthenticationService.Instance.PlayerInfo.CreatedAt;
			playerInfo.ID = AuthenticationService.Instance.PlayerInfo.Id;
			playerInfo.username = AuthenticationService.Instance.PlayerInfo.Username;
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}

	//async void Start()
	//{
	//	if (!useUnityServices)
	//		return;

	//	try
	//	{
	//		await UnityServices.InitializeAsync();
	//	}
	//	catch (Exception e)
	//	{
	//		Debug.LogException(e);
	//	}

	//	AuthenticationService.Instance.SignInFailed += (err) =>Debug.LogError($"Unity Relay :: {err}");
	//	AuthenticationService.Instance.SignedOut += () =>		 Debug.Log($"Unity Relay :: Player signed out.");
	//	AuthenticationService.Instance.Expired += () =>			 Debug.Log($"Unity Relay :: Player session could not be refreshed and expired.");
	//	AuthenticationService.Instance.SignedIn += () =>			 Debug.Log($"Unity Relay :: Player Signed in. ID: {AuthenticationService.Instance.PlayerId}");

	//	try
	//	{
	//		await AuthenticationService.Instance.SignInAnonymouslyAsync();
	//		playerInfo = new PlayerInfoData();
	//		playerInfo.creationTime = AuthenticationService.Instance.PlayerInfo.CreatedAt;
	//		playerInfo.ID = AuthenticationService.Instance.PlayerInfo.Id;
	//		playerInfo.username = AuthenticationService.Instance.PlayerInfo.Username;
	//	}
	//	catch (Exception e)
	//	{
	//		Debug.LogException(e);
	//	}
	//}
}