using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct PlaneContainer
{
    public Vector3 position;
    public Vector3 normal;
    public Vector3 size;

    public Vector3 x;
    public Vector3 y;

    public static int GetSize => sizeof(float) * 15;
}

[ExecuteInEditMode]
public class Plane : MonoBehaviour
{
    public PlaneContainer container;

	private void Update()
	{
		if (transform.hasChanged)
        {
            container.position = transform.position;
            container.normal = transform.up;

            container.size = 10 * new Vector3(
                transform.lossyScale.z, transform.lossyScale.x, transform.lossyScale.y / 5
                );

            container.x = transform.forward;
            container.y = transform.right;
		}
	}
}
