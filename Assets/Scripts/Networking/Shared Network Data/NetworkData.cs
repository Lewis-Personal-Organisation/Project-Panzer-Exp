using System;
using UnityEngine;
using System.Collections.Generic;
using System.Text;

public class NetworkData : MonoBehaviour
{
	public const int MaxPlayerCount = 4;
	[SerializeField] private List<string> playerNames = new List<string>();

	/// <summary>
	/// Is this session at maximum player capacity?
	/// </summary>
	/// <returns></returns>
	public bool ServerAtPlayerCapacity()
	{
		return playerNames.Count == MaxPlayerCount;
	}

	public string GetCurrentCapacity => $"{playerNames.Count}/{MaxPlayerCount}";
	public string GetPlayerNames()
	{
		StringBuilder stringBuilder = new StringBuilder();

		for (int i = 0; i < playerNames.Count; i++)
		{
			stringBuilder.Append($"{playerNames[i]}{(i != playerNames.Count - 1 ? ", " : "")}");
		}

		return stringBuilder.ToString();
	}

	/// <summary>
	/// Returns weather a player name can be accepted
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public bool TrySubmitNewPlayerName(string name)
	{
		if (playerNames.Contains(name))
		{
			Debug.Log($"'{name}' already exists. Choose a different name!");
			return false;
		}

		Debug.Log($"Added '{name}' to list of used names");
		playerNames.Add(name);
		return true;
	}
}
