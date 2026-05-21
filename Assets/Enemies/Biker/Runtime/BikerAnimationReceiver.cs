using UnityEngine;

namespace Enemies.Biker.Runtime
{
	public class BikerAnimationReceiver : MonoBehaviour
	{
		private BikerContext _context;

		private void Awake()
		{
			_context = GetComponentInParent<BikerContext>();
		}

		public void OpenAttackZone()
		{
			if (_context != null)
			{
				_context.OpenAttackZone();
			}
		}

		public void CloseAttackZone()
		{
			if (_context != null)
			{
				_context.CloseAttackZone();
			}
		}
	}
}