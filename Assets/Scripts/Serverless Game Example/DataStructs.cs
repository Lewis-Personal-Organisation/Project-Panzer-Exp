using System;
using System.Collections.Generic;
using UnityEngine;

public class DataStructs : MonoBehaviour
{
	public struct GameResultsData
	{
		public string winnerPlayerName;

		public string winnerPlayerId;

		public int winnerScore;

		public List<PlayerScoreData> playerScoreData;

		public override string ToString()
		{
			return $"Results: Winner:{winnerPlayerName} with {winnerScore} points, total players: {playerScoreData.Count}.";
		}
	}

	[Serializable]
	public struct PlayerScoreData
	{
		public string playerId;

		public string playerName;

		public int score;

		//public PlayerScoreData(PlayerAvatar playerAvatar)
		//{
		//	playerId = playerAvatar.playerId;
		//	playerName = playerAvatar.playerName;
		//	score = playerAvatar.score;
		//}
	}
}
