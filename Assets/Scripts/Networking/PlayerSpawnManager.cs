using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GameScene'e boş bir GameObject ekle, bu script'i bağla.
/// SpawnManager GameObject'ine NetworkObject component'i de ekle.
/// </summary>
public class PlayerSpawnManager : NetworkBehaviour
{
    [SerializeField] private GameObject teamAPrefab;
    [SerializeField] private GameObject teamBPrefab;
    [SerializeField] private Transform teamASpawnParent; // child: 1, 2
    [SerializeField] private Transform teamBSpawnParent; // child: 1, 2

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        SpawnAllPlayers();
    }

    private void SpawnAllPlayers()
    {
        var teamASpawns = GetChildren(teamASpawnParent);
        var teamBSpawns = GetChildren(teamBSpawnParent);
        Shuffle(teamASpawns);
        Shuffle(teamBSpawns);

        int aIdx = 0, bIdx = 0;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            int team = GetTeamForClient(client.ClientId);
            GameObject prefab;
            Transform spawn;

            if (team == 2)
            {
                prefab = teamBPrefab;
                spawn = teamBSpawns[bIdx % teamBSpawns.Count];
                bIdx++;
            }
            else
            {
                prefab = teamAPrefab;
                spawn = teamASpawns[aIdx % teamASpawns.Count];
                aIdx++;
            }

            var go = Instantiate(prefab, spawn.position, spawn.rotation);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(client.ClientId);
        }
    }

    private static int GetTeamForClient(ulong clientId)
    {
        if (NetworkLobbyManager.Instance == null) return 1;

        foreach (var player in NetworkLobbyManager.Instance.LobbyPlayers)
        {
            if (player.ClientId == clientId)
                return player.TeamId;
        }
        return 1;
    }

    private static List<Transform> GetChildren(Transform parent)
    {
        var list = new List<Transform>();
        foreach (Transform child in parent)
            list.Add(child);
        return list;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
