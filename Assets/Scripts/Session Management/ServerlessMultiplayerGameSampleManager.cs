using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    [DisallowMultipleComponent]
    public class ServerlessMultiplayerGameSampleManager : MonoBehaviour
    {
        // Profile name prefix used for unique profiles to permit multiplayer testing using one anonymous account.
        const string k_ProfileNamePrefix = "Profile-";

        public static ServerlessMultiplayerGameSampleManager instance { get; private set; }

        public bool isInitialized { get; private set; }

        // Save off dropdown index so we can restore it on each of the subsequent scenes.
        public int profileDropdownIndex { get; private set; }

        // Reason we returned to the main menu so we can show popup to client if host leaves or player is kicked.
        public enum ReturnToMenuReason
        {
            None,
            PlayerKicked,
            LobbyClosed,
            HostLeftGame
        }

        // Save last reason for returning to the menu. Note that it's stored here because this is one of the few classes
        // that is always present and is responsible for coordinating the user flow for the entire sample.
        public ReturnToMenuReason returnToMenuReason { get; private set; }

        // At the end of the game, we store results here so we can show them when we return to the main menu.
        public bool arePreviousGameResultsSet { get; private set; }

        public DataStructs.GameResultsData previousGameResults { get; private set; }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                instance = this;
            }
        }

        async void Start()
        {
            try
            {
                DontDestroyOnLoad(gameObject);

                //ProfanityManager.Initialize();

                await SignInAndInitialize();

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                isInitialized = true;

                //menuSceneManager.ShowMainMenu();

                Debug.Log("Initialization and signin complete.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task SignInAndInitialize()
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
				if (this == null) return;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void SetReturnToMenuReason(ReturnToMenuReason returnToMenuReason)
        {
            this.returnToMenuReason = returnToMenuReason;
        }

        public void ClearReturnToMenuReason()
        {
            this.returnToMenuReason = ReturnToMenuReason.None;
        }

        public void SetPreviousGameResults(DataStructs.GameResultsData results)
        {
            previousGameResults = results;
            arePreviousGameResultsSet = true;
        }

        public void ClearPreviousGameResults()
        {
            arePreviousGameResultsSet = false;
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
