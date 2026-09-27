namespace Car.Controller.CarPhysics.Engine
{
	[System.Serializable]
	public class EngineModel
	{
		private readonly EngineSO baseEngine;

		public float BlockMass { get; private set; }
		public float Volume { get; private set; }
		public float CylinderNum { get; private set; }
		public float CylinderMass { get; private set; }
		public float PistonStroke { get; private set; }
		public float PistonMaxSpeed { get; private set; }
		public float ThrottlePow { get; private set; }
		public float InertiaCoef { get; private set; }

		public float TurboCoef { get; private set; }
		public float VolumetricEfficiency { get; private set; }
		public float AirToFuelRatio { get; private set; }
		public float ThermalEfficiency { get; private set; }
		public float FuelEnergy { get; private set; }

		public float A { get; private set; }
		public float B { get; private set; }
		public float C { get; private set; }

		public float FrictionC0 { get; private set; }
		public float FrictionC1 { get; private set; }
		public float FrictionC2 { get; private set; }

		public float IdleRPM { get; private set; }
		public float IdleThrottleGain { get; private set; }
		public float StallRPM { get; private set; }
		public float RevLimiterCutTime { get; private set; }

		public EngineModel(EngineSO baseEngine)
		{
			this.baseEngine = baseEngine;

			BlockMass = baseEngine.BlockMass;
			Volume = baseEngine.Volume;
			CylinderNum = baseEngine.CylinderNum;
			CylinderMass = baseEngine.CylinderMass;
			PistonStroke = baseEngine.PistonStroke;
			PistonMaxSpeed = baseEngine.PistonMaxSpeed;
			ThrottlePow = baseEngine.ThrottlePow;
			InertiaCoef = baseEngine.InertiaCoef;

			TurboCoef = baseEngine.TurboCoef;
			VolumetricEfficiency = baseEngine.VolumetricEfficiency;
			AirToFuelRatio = baseEngine.AirToFuelRatio;
			ThermalEfficiency = baseEngine.ThermalEfficiency;
			FuelEnergy = baseEngine.FuelEnergy;

			A = baseEngine.A;
			B = baseEngine.B;
			C = baseEngine.C;

			FrictionC0 = baseEngine.FrictionC0;
			FrictionC1 = baseEngine.FrictionC1;
			FrictionC2 = baseEngine.FrictionC2;

			IdleRPM = baseEngine.IdleRPM;
			IdleThrottleGain = baseEngine.IdleThrottleGain;
			StallRPM = baseEngine.StallRPM;
			RevLimiterCutTime = baseEngine.RevLimiterCutTime;
		}
	}
}
