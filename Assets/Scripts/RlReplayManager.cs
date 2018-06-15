using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RocketLeagueReplayParser;
using RocketLeagueReplayParser.NetworkStream;

public class RlReplayManager : MonoBehaviour {
    // Replay files can be found on Windows in "Documents\My Games\Rocket League\TAGame\Demos".
    public string replayPath;
    // Objects used to represent Rocket League objects.
    public GameObject playerPrefab, ballPrefab, otherPrefab;
    // Raw positions are huge, going into 1000s.
    // Use this to scale down to a more managable range.
    public float posScale = 0.01f;
    // Rotation values seem to be between roughly -1.0 and 1.0, though there are some slighly outside
    // that range.
    // The resulting rotation angles are not completely wrong, but definately not correct.
    public float rotScale = 180f;
    // Pause and resume playback in the inspector.
    public bool play;
    // Elapsed replay time in seconds.
    // If you want to start the replay at a certain time, you can set this before playing.
    public float time = 0;
    // This is the paresed replay object.
    private Replay replay;
    // Each Rocket League object has a unique Id, this maps the ID to the Unity GameObject.
    private Dictionary<uint, GameObject> rlObjects = new Dictionary<uint, GameObject>();
    // Current replay data frame number.
    private int rlFrameNum = 0;
    // Rocket League objects' type is identified by a class ID, which is tied to a class name.
    // This dictionary is used to determine the type from the ID more quickly / easily.
    private Dictionary<int, string> classNameLookup = new Dictionary<int, string>();
    // Lots of "other" objects, so let's put them under a single parent to reduce hierarchy clutter.
    private GameObject otherObjsParent;
    // More useful organization of used object types.
    private enum MyRlObjTypes { Car, Ball, Other }

    private void Start()
    {
        // Parse replay.
        replay = Replay.Deserialize(replayPath);

        // Build class name dictionary.
        for(int i = 0; i < replay.ClassIndexLength; i++)
            classNameLookup.Add(replay.ClassIndexes[i].Index, replay.ClassIndexes[i].Class);

        otherObjsParent = new GameObject("Other Objects");
    }

    private void Update()
    {
        if(play)
        {
            // Itterate through replay data frames until there's no more frames or the data frame's time
            // is past our time value.
            while(rlFrameNum < replay.Frames.Count && time >= replay.Frames[rlFrameNum].Time)
            {
                Frame rlFrame = replay.Frames[rlFrameNum];
                // Itterate through Rocket League objects (ActorStates).
                for (int i = 0; i < rlFrame.ActorStates.Count; i++)
                {
                    ActorState actorState = rlFrame.ActorStates[i];
                    GameObject rlObj = null;
                    // If RL object has just been created, spawn a new GameObject for it.
                    if (actorState.State == ActorStateState.New)
                    {
                        // Make sure there is not an existing object with the same ID.
                        // Not sure what would would cause this.
                        if (rlObjects.ContainsKey(actorState.Id))
                            Debug.Log("Duplicate object ID " + actorState.Id);                            
                        else
                        {
                            rlObj = GetNewRlObject(actorState);
                            rlObjects.Add(actorState.Id, rlObj);
                        }
                    }
                    // Otherwise we should be able to get the object from the dictionary.
                    else 
                    {
                        // Make sure the dictionary actually has the object.
                        // I think ActorStates only have a valid class type on the frame they are created,
                        // so I don't think we can just spawn a missing object here.
                        if (!rlObjects.ContainsKey(actorState.Id))
                            Debug.Log("Missing object ID " + actorState.Id);
                        else
                            rlObj = rlObjects[actorState.Id];
                    }
                    
                    // If everthing went correctly getting the object...
                    if (rlObj != null)
                    {
                        // If the object is being deleted, destroy it.
                        if (actorState.State == ActorStateState.Deleted)
                        {
                            rlObjects.Remove(actorState.Id);
                            Destroy(rlObj);
                        }
                        // Otherwise update the object's state.
                        else
                            UpdateObjectState(actorState);
                    }
                }
                rlFrameNum++;
            }           
            time += Time.deltaTime;
        }       
    }

    private void UpdateObjectState(ActorState actorState)
    {
        // Apply position and rotation if they are defined.
        // These are not the main position / rotation values, I think these are
        // just for initialization.
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
        // Itterate through updated object properties and apply them.
        foreach (uint propKey in actorState.Properties.Keys)
            ProcessProperty(actorState.Id, actorState.Properties[propKey]);
    }

    // Apply updated properties.
    private void ProcessProperty(uint actorId, ActorStateProperty asp)
    {
        // Only properties with defined cases are actually used.
        switch(asp.PropertyName)
        {
            // Rigidbody state.
            // Includes position, rotation, and velocity info (see RigidBodyState for all fields).
            case "TAGame.RBActor_TA:ReplicatedRBState":
                RigidBodyState rbState = (RigidBodyState)(asp.Data);
                Vector3 pos = new Vector3(rbState.Position.X, rbState.Position.Z, rbState.Position.Y) * posScale;
                Vector3 rot = new Vector3(rbState.Rotation.X, rbState.Rotation.Y, rbState.Rotation.Z) * rotScale;
                rlObjects[actorId].transform.position = pos;
                rlObjects[actorId].transform.eulerAngles = rot;
                break;
            // Player name.
            case "Engine.PlayerReplicationInfo:PlayerName":
                rlObjects[actorId].name += " (" +(string)asp.Data + ")";
                break;
            // Team / color info.
            case "TAGame.Car_TA:TeamPaint":
                // Make object orange if team 0, blue if team 1.
                MeshRenderer meshRend = rlObjects[actorId].GetComponent<MeshRenderer>();
                TeamPaint tp = (TeamPaint)asp.Data;
                if (tp.TeamNumber == 0)
                    meshRend.material.color = new Color(1, 0.5f, 0);
                else
                    meshRend.material.color = Color.blue;
                break;               
        }
        // Add property to PropertyInfo component for debuging. 
        rlObjects[actorId].GetComponent<PropertyInfo>().ProcessProperty(asp);
    }

    // Get a new GameObject for the given object type.
    private GameObject GetNewRlObject(ActorState actorState)
    {
        // Determine object type.
        MyRlObjTypes rlObjType = GetRlObjType(actorState);
        GameObject newObj;
        // Instantiate corresponding GameObject.
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


    // Convert class ID to custom type enum.
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

    // Safely get a class name from class ID.
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
