using System.Collections.Generic;
using UnityEngine;

public class Medipack : Projectile
{
	public new const string name = "MEDIPACK";

	private const float RESUPPLY_RATE = 3f;

	private const float RESUPPLY_RANGE = 4f;

	private void Awake()
	{
		Rigidbody component = GetComponent<Rigidbody>();
		component.velocity = base.transform.forward * configuration.speed;
		InvokeRepeating("Resupply", 3f, 3f);
	}

	private void Resupply()
	{
		List<Actor> list = ActorManager.AliveActorsInRange(base.transform.position, 4f);
		foreach (Actor item in list)
		{
			item.ResupplyHealth();
		}
	}

	protected override void Update()
	{
		if (Time.time > expireTime)
		{
			Object.Destroy(base.gameObject);
		}
	}
}
