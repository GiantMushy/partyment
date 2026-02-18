using System.Collections.Generic;
using UnityEngine;

public class ObjectiveAssigner : MonoBehaviour
{
    public ObjectiveDatabase database;

    //playerObjectives[playerIndex] = objective
    public List<SecretObjectives> playerObjectives = new();

    public void Assign(int playerCount)
    {
        playerObjectives.Clear();

        var pool = new List<SecretObjectives>(database.allObjectives);

        if (pool.Count < playerCount)
            Debug.LogWarning("Not enough objectives for all players; some players may not get one.");

        for (int i = 0; i < playerCount; i++)
        {
            if (pool.Count == 0) break;
            int r = Random.Range(0, pool.Count);
            playerObjectives.Add(pool[r]);
            pool.RemoveAt(r); //no dupes
        }
    }

    public SecretObjectives GetForPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerObjectives.Count) return null;
        return playerObjectives[playerIndex];
    }
}
