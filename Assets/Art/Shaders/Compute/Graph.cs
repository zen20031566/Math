using System;
using UnityEngine;

public class Graph : MonoBehaviour
{
	[SerializeField] Transform pointPrefab;
	[SerializeField, Range(10, 100)]
	int resolution = 10;

	private Transform[] points;
	
	[SerializeField]
	FunctionLibrary.FunctionName function;
	
	private void Start()
	{
		points = new Transform[resolution * resolution];
		float step = 2f / resolution;
		var scale = Vector3.one * step;
		
		for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++)
		{
			Transform point = Instantiate(pointPrefab);
			points[i] = point;
			Vector3 position = point.localPosition;
			
			if (x == resolution) {
				x = 0;
				z++;
			}
			
			position.x = (x + 0.5f) * step - 1f;
			position.z = (z + 0.5f) * step - 1f;
			
			//f(x)
			position.y = x * x;

			position.y += 2;
			point.localPosition = position;
			point.localScale = scale;
			point.SetParent(transform, false);
		}
	}

	private void Update()
	{
		for (int i = 0; i < points.Length; i++)
		{
			Transform point = points[i];
			Vector3 position = point.localPosition;
			float time = Time.time;
			
			FunctionLibrary.Function f = FunctionLibrary.GetFunction(function);
			position.y = f(position.x, position.z,time);
			
			position.y += 2;
			point.localPosition = position;
		}
	}
}
