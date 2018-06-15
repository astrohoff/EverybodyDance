using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RocketLeagueReplayParser;
using RocketLeagueReplayParser.NetworkStream;
public class RlReplayManager : MonoBehaviour {
    public string replayPath;
    public GameObject playerPrefab, ballPrefab, otherPrefab;
    public float posScale = 0.01f;
    public float rotScale = 180f;
    public bool play;
    private Replay replay;
    //private List<GameObject> players = new List<GameObject>();
    private Dictionary<uint, GameObject> rlObjects = new Dictionary<uint, GameObject>();
    public float time = 0;
    public int rlFrameNum = 0;
    private Dictionary<int, string> classNameLookup = new Dictionary<int, string>();
    private GameObject otherObjsParent;

    private void Start()
    {
        replay = Replay.Deserialize(replayPath);

        for(int i = 0; i < replay.ClassIndexLength; i++)
        {
            classNameLookup.Add(replay.ClassIndexes[i].Index, replay.ClassIndexes[i].Class);
        }

        /*int playerCount = (int)(replay.Properties["TeamSize"].Value) * 2;
        for(int i = 0; i < playerCount; i++)
        {
            GameObject newPlayer = Instantiate(playerPrefab);
            newPlayer.name = "Player " + i;
            players.Add(newPlayer);
        }*/
        otherObjsParent = new GameObject("Other Objects");
    }

    private void Update()
    {
        if(play)
        {
            Vector3 updatePos = Vector3.zero;
            Vector3 updateRot = Vector3.zero;
            while(rlFrameNum < replay.Frames.Count && time >= replay.Frames[rlFrameNum].Time)
            {
                Frame rlFrame = replay.Frames[rlFrameNum];
                for (int i = 0; i < rlFrame.ActorStates.Count; i++)
                {
                    ActorState actorState = rlFrame.ActorStates[i];
                    GameObject rlObj = null;
                    if (actorState.State == ActorStateState.New)
                    {
                        if (!rlObjects.ContainsKey(actorState.Id))
                        {
                            rlObj = GetNewRlObject(actorState);
                            rlObjects.Add(actorState.Id, rlObj);
                        }
                    }
                    else 
                    {
                        if(rlObjects.ContainsKey(actorState.Id))
                            rlObj = rlObjects[actorState.Id];
                    }
                    if (rlObj != null)
                    {
                        if (actorState.State == ActorStateState.Deleted)
                        {
                            rlObjects.Remove(actorState.Id);
                            Destroy(rlObj);
                        }
                        else
                        {
                            /*if (TryGetPosition(actorState, ref updatePos))
                                rlObj.transform.position = updatePos;
                            if (TryGetRotation(actorState, ref updateRot))
                                rlObj.transform.eulerAngles = updateRot;
                            */
                            UpdateObjectState(actorState);
                        }
                    }
                }
                rlFrameNum++;
            }           
            time += Time.deltaTime;
        }       
    }

    private void UpdateObjectState(ActorState actorState)
    {
        if(actorState.Position != null)
        {
            Vector3D rlPos = actorState.Position;
            Vector3 pos = new Vector3(rlPos.X, rlPos.Z, rlPos.Y) * posScale;
            rlObjects[actorState.Id].transform.position = pos;
        }
        if(actorState.Rotation != null)
        {
            Rotator rlRot = actorState.Rotation;
            Vector3 rot = new Vector3(rlRot.Pitch, rlRot.Yaw, rlRot.Roll) * rotScale;
            rlObjects[actorState.Id].transform.eulerAngles = rot;
        }
        foreach (uint propKey in actorState.Properties.Keys)
            ProcessProperty(actorState.Id, actorState.Properties[propKey]);
    }

    private void ProcessProperty(uint actorId, ActorStateProperty asp)
    {
        switch(asp.PropertyName)
        {
            case "TAGame.RBActor_TA:ReplicatedRBState":
                RigidBodyState rbState = (RigidBodyState)(asp.Data);
                Vector3 pos = new Vector3(rbState.Position.X, rbState.Position.Z, rbState.Position.Y) * posScale;
                Vector3 rot = new Vector3(rbState.Rotation.X, rbState.Rotation.Y, rbState.Rotation.Z) * rotScale;
                rlObjects[actorId].transform.position = pos;
                rlObjects[actorId].transform.eulerAngles = rot;
                break;
            case "Engine.PlayerReplicationInfo:PlayerName":
                if(rlObjects[actorId].name.Length < 128)
                    rlObjects[actorId].name += " (" +(string)asp.Data + ")";
                break;
            case "TAGame.Car_TA:TeamPaint":
                MeshRenderer meshRend = rlObjects[actorId].GetComponent<MeshRenderer>();
                if(meshRend != null)
                {
                    TeamPaint tp = (TeamPaint)asp.Data;
                    if (tp.TeamNumber == 0)
                        meshRend.material.color = new Color(1, 0.5f, 0);
                    else
                        meshRend.material.color = Color.blue;
                }
                break;               
        }
        rlObjects[actorId].GetComponent<PropertyInfo>().ProcessProperty(asp);
    }

    private bool TryGetPosition(ActorState actorState, ref Vector3 pos)
    {
        foreach(uint propKey in actorState.Properties.Keys)
        {
            ActorStateProperty asp = actorState.Properties[propKey];
            if (asp.PropertyName == "TAGame.RBActor_TA:ReplicatedRBState")
            {
                RigidBodyState rbState = (RigidBodyState)(asp.Data);
                pos.x = rbState.Position.X;
                pos.y = rbState.Position.Z;
                pos.z = rbState.Position.Y;
                pos *= posScale;
                return true;
            }
        }
        if (actorState.Position != null)
        {
            pos.x = actorState.Position.X;
            pos.y = actorState.Position.Z;
            pos.z = actorState.Position.Y;
            pos *= posScale;
            return true;
        }
        return false;
    }

    private bool TryGetRotation(ActorState actorState, ref Vector3 rot)
    {
        foreach (uint propKey in actorState.Properties.Keys)
        {
            ActorStateProperty asp = actorState.Properties[propKey];
            if (asp.PropertyName == "TAGame.RBActor_TA:ReplicatedRBState")
            {
                RigidBodyState rbState = (RigidBodyState)(asp.Data);
                rot.x = 0; //rbState.Rotation.X;
                rot.y = rbState.Rotation.Y;
                rot.z = 0;// rbState.Rotation.Z;
                rot *= rotScale;
                return true;
            }
        }
        if (actorState.Rotation != null)
        {
            rot.x = 0;// actorState.Rotation.Pitch;
            rot.y = actorState.Rotation.Yaw;
            rot.z = 0;// actorState.Rotation.Roll;
            rot *= rotScale;
            return true;
        }
        return false;
    }

    private GameObject GetNewRlObject(ActorState actorState)
    {
        MyRlObjTypes rlObjType = GetRlObjType(actorState);
        GameObject newObj;
        switch(rlObjType)
        {
            case MyRlObjTypes.Car:
                newObj = Instantiate(playerPrefab);
                newObj.name = "player (" + actorState.Id + ")";
                break;
            case MyRlObjTypes.Ball:
                newObj = Instantiate(ballPrefab);
                newObj.name = "Ball (" + actorState.Id + ")";
                newObj.tag = "Ball";
                break;
            default:
                newObj = Instantiate(otherPrefab);
                newObj.name = GetClassName((int)actorState.ClassId) + "(" + actorState.Id + ")";
                newObj.transform.parent = otherObjsParent.transform;
                break;
        }
        return newObj;
    }

    private enum MyRlObjTypes { Car, Ball, Other }

    private MyRlObjTypes GetRlObjType(ActorState actorState)
    {
        string className = GetClassName((int)(actorState.ClassId));
        switch (className)
        {
            case "TAGame.Car_TA":
                return MyRlObjTypes.Car;
            case "TAGame.Ball_TA":
                return MyRlObjTypes.Ball;
            default:
                return MyRlObjTypes.Other;
        }
    }
    private string GetClassName(int id)
    {
        string className;
        if(classNameLookup.TryGetValue(id, out className) == true)
        {
            return className;
        }
        return "class not found";
    }
}
