using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ForceFieldContainer
{
    public Vector3 center;
    public Vector3 size;
    public Vector3 force;
	public int use;

	public static int GetSize => sizeof(float) * 9 + sizeof(int);
}


[ExecuteInEditMode]
public class ForceField : MonoBehaviour
{
    public ForceFieldContainer container;
    public bool shoudDraw = true;
	public bool alwasOn = true;

	private void Update()
	{
		if (transform.hasChanged)
		{
			container.center = transform.position;
			container.size = transform.lossyScale;
		}
	}

	private void OnDrawGizmos()
	{
		if (shoudDraw)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireCube(container.center, container.size);
		}
	}
}
