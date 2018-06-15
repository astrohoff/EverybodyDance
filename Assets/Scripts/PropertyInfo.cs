using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PropertyInfo : MonoBehaviour {
    public List<MyProperty> props = new List<MyProperty>();

    public void ProcessProperty(RocketLeagueReplayParser.NetworkStream.ActorStateProperty asp)
    {
        for(int i = 0; i < props.Count; i++)
        {
            if (props[i].name == asp.PropertyName)
            {
                props[i].value = asp.Data.ToString();
                return;
            }
        }
        if(props.Count < 50)
            props.Add(new MyProperty(asp));
    }

    [System.Serializable]
    public class MyProperty
    {
        public string name = "";
        public string value = "";

        public MyProperty(string name, string value)
        {
            this.name = name;
            this.value = value;
        }

        public MyProperty(RocketLeagueReplayParser.NetworkStream.ActorStateProperty asp)
        {
            name = asp.PropertyName;
            value = asp.Data.ToString();
        }
    }
}
