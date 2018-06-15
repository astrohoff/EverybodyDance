using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControls : MonoBehaviour {
    public float moveSpeed = 10;
    public float shiftMult = 2;
    public float smoothTime = 0.25f;
    private GameObject ballFollower;
    private Vector3 camVel = Vector3.zero;

    private void Start()
    {
        ballFollower = new GameObject("Ball Follower");
    }

    void LateUpdate () {
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? shiftMult : 1);
        Vector3 movement = transform.forward * Input.GetAxis("Vertical") * speed * Time.deltaTime;
        movement += transform.right * Input.GetAxis("Horizontal") * speed * Time.deltaTime;
        movement += Vector3.up * Input.GetAxis("Vertical2") * speed * Time.deltaTime;
        transform.position += movement;

        GameObject ball = GameObject.FindWithTag("Ball");
        if (ball != null)
        {
            Vector3 curPos = ballFollower.transform.position;
            Vector3 targPos = ball.transform.position;
            Vector3 newFollowPos = Vector3.SmoothDamp(curPos, targPos, ref camVel, smoothTime);
            ballFollower.transform.position = newFollowPos;
            transform.LookAt(ballFollower.transform);           
        }
	}


}
