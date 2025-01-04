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
	public bool playerIsSetup => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;

	[System.Serializable]
	public class PlayerInfoData
	{
		public DateTime? creationTime;
		public string ID;
		public string username;
	}

	public async Task InitialiseUnityServices()
	{
		try
		{
			await UnityServices.InitializeAsync();
			Debug.Log("Session Manager :: Unity Services initialised Successfully");
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}

		AuthenticationService.Instance.SignInFailed += (err) =>
		{
			playerInfo = null;
			Debug.LogError($"Unity Relay :: {err}");
		};

		AuthenticationService.Instance.SignedOut += () =>
		{
			playerInfo = null;
			Debug.Log($"Unity Relay :: Player signed out.");
		};

		AuthenticationService.Instance.Expired += () =>
		{
			playerInfo = null;
			Debug.Log($"Unity Relay :: Player session could not be refreshed and expired.");
		};

		AuthenticationService.Instance.SignedIn += () => Debug.Log($"Unity Relay :: Player Signed in. ID: {AuthenticationService.Instance.PlayerId}");

		try
		{
			await AuthenticationService.Instance.SignInAnonymouslyAsync();
			playerInfo = new PlayerInfoData();
			playerInfo.creationTime = AuthenticationService.Instance.PlayerInfo.CreatedAt;
			playerInfo.ID = AuthenticationService.Instance.PlayerInfo.Id;
			playerInfo.username = AuthenticationService.Instance.PlayerInfo.Username;

			Debug.Log("Session Manager :: Signed in Anonymously");
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
	}
}