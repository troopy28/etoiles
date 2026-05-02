using UnityEngine;

public class MenuCameraFly : MonoBehaviour
{
	[Header("Targeting")]
	public Transform target;
	public Vector3 offset = new Vector3(0, 3, -15);
		
	[Header("Movement")]
	public float movementSpeed = 0.5f;
	public float movementAmount = 1.2f;
	public float rotationSpeed = 0.3f;
	public float rotationAmount = 2.0f;

	private Vector3 initialPos;
	private Quaternion initialRot;

	void Start()
	{
		initialPos = transform.position;
		initialRot = transform.rotation;
		
		if (target == null)
		{
			GameObject ship = GameObject.Find("Menu_Ship");
			if (ship != null) target = ship.transform;
		}
	}

	void Update()
	{
		float xShift = Mathf.Sin(Time.time * movementSpeed) * movementAmount;
		float yShift = Mathf.Cos(Time.time * movementSpeed * 0.7f) * (movementAmount * 0.5f);
		
		Vector3 targetPos = initialPos + new Vector3(xShift, yShift, 0);
		transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 2.0f);

		float xRot = Mathf.Sin(Time.time * rotationSpeed) * rotationAmount;
		float yRot = Mathf.Cos(Time.time * rotationSpeed * 0.8f) * rotationAmount;
		
		transform.rotation = initialRot * Quaternion.Euler(xRot, yRot, 0);

		if (target != null)
		{
			Quaternion lookRot = Quaternion.LookRotation(target.position - transform.position);
			transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 0.5f);
		}
	}
}
