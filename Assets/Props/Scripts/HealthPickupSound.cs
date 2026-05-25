using System;
using Car.Health.Pickups;
using FMODUnity;
using Pickups;
using UnityEngine;

namespace Props.Scripts
{
	public class HealthPickupSound : MonoBehaviour
	{
		[SerializeField]
		private EventReference healthPickupEvent;
		
		[SerializeField]
		private HealthPickup _context;

		private void Start()
		{
			_context.OnCollected += PlaySound_Repair;
		}

		public void PlaySound_Repair()
		{
			RuntimeManager.PlayOneShot(healthPickupEvent, transform.position);
		}
	}
}