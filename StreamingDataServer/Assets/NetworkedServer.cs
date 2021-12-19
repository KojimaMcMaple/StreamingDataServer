using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;
    LinkedList<SharingRoom> room_list_;

    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);
        room_list_ = new LinkedList<SharingRoom>();
    }

    // Update is called once per frame
    void Update()
    {
        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);
        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessReceivedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                PlayerDisconnected(recConnectionID);
                break;
        }
    }
  
    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }

    private void ProcessReceivedMsg(string msg, int id)
    {
        Debug.Log("msg received = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);
        if (signifier == ClientToServerSignifiers.kJoinSharingRoom)
        {
            string name = csv[1];
            bool does_room_exist = false;
            foreach (SharingRoom item in room_list_)
            {
                if (item.name == name)
                {
                    does_room_exist = true;
                    if (!item.player_id_list.Contains(id))
                    {
                        item.player_id_list.AddLast(id);
                        Debug.Log(">>> Added player ID#" + id + " to room!");
                    }
                    else
                    {
                        Debug.Log(">>> Prevented dupe ID#" + id + " to join room!");
                    }
                    break;
                }
            }
            if (!does_room_exist)
            {
                SharingRoom sr = new SharingRoom();
                sr.name = name;
                sr.player_id_list.AddLast(id);
                room_list_.AddLast(sr);
                Debug.Log(">>> Created new SharingRoom, added player ID#" + id + " to room!");
            }
        }
        else if (signifier == ClientToServerSignifiers.kPartyTransferDataStart)
        {
            SharingRoom sr = GetSharingRoomWithPlayerId(id);
            sr.sharing_party_data = new LinkedList<string>();
        }
        else if (signifier == ClientToServerSignifiers.kPartyTransferData)
        {
            SharingRoom sr = GetSharingRoomWithPlayerId(id);
            sr.sharing_party_data.AddLast(msg);
        }
        else if (signifier == ClientToServerSignifiers.kPartyTransferDataEnd)
        {
            SharingRoom sr = GetSharingRoomWithPlayerId(id);
            foreach (int player_id in sr.player_id_list)
            {
                if (player_id == id)
                {
                    continue;
                }
                SendMessageToClient(ServerToClientSignifiers.kPartyTransferDataStart + "", player_id);
                foreach (string item in sr.sharing_party_data)
                {
                    SendMessageToClient(item, player_id);
                }
                SendMessageToClient(ServerToClientSignifiers.kPartyTransferDataEnd + "", player_id);
            }
        }
    }

    private void PlayerDisconnected(int id)
    {
        SharingRoom room_matched_player_id = GetSharingRoomWithPlayerId(id);
        if (room_matched_player_id != null)
        {
            room_matched_player_id.player_id_list.Remove(id);
            if (room_matched_player_id.player_id_list.Count == 0)
            {
                room_list_.Remove(room_matched_player_id);
            }
        }
    }

    private SharingRoom GetSharingRoomWithPlayerId(int id)
    {
        foreach (SharingRoom room in room_list_)
        {
            foreach (int player_id in room.player_id_list)
            {
                if (player_id == id)
                {
                    return room;
                }
            }
        }
        return null;
    }
}

public class SharingRoom
{
    public LinkedList<int> player_id_list;
    public string name;
    public LinkedList<string> sharing_party_data;

    public SharingRoom()
    {
        player_id_list = new LinkedList<int>();
        sharing_party_data = new LinkedList<string>();
    }
}

static public class ClientToServerSignifiers
{
    public const int kJoinSharingRoom = 1;
    public const int kPartyTransferDataStart = 100;
    public const int kPartyTransferData = 101;
    public const int kPartyTransferDataEnd = 102;
}

static public class ServerToClientSignifiers
{
    public const int kPartyTransferDataStart = 100;
    public const int kPartyTransferData = 101;
    public const int kPartyTransferDataEnd = 102;
}