using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ExplosionFieldContainer
{
    public Vector3 center;
    public float radius;
    public float force;
	public int use;

    public static int GetSize => sizeof(float) * 5 + sizeof(int);
}


[ExecuteInEditMode]
public class ExplosionField : MonoBehaviour
{
    public ExplosionFieldContainer container;
    public bool shouldDraw = true;
	public bool alwasOn = true;

	private void Update()
	{
		if (transform.hasChanged)
		{
			container.center = transform.position;
		}
	}

	private void OnDrawGizmos()
	{
		if (shouldDraw)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireSphere(container.center, container.radius);
		}
	}
}
