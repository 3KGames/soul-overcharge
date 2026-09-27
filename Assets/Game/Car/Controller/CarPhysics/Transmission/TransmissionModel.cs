namespace Car.Controller.CarPhysics.Transmission
{
	public class TransmissionModel
	{
		private readonly TransmissionSO baseTransmission;

		public float[] Gears     { get; private set; }
		public float   FinalGear { get; private set; }

		public float Efficiency   { get; private set; }
		public float ClutchCoef   { get; private set; }
		public float ClutchError  { get; private set; }

		public float ShiftUpRPM        { get; private set; }
		public float ShiftDownRPM      { get; private set; }
		public float ShiftCooldownTime { get; private set; }

		public float ClutchEngageTime { get; private set; }
		public float LaunchSpeed      { get; private set; }

		public TransmissionModel(TransmissionSO baseTransmission)
		{
			this.baseTransmission = baseTransmission;

			Gears     = baseTransmission.Gears;
			FinalGear = baseTransmission.FinalGear;
			Efficiency = baseTransmission.Efficiency;
			ClutchCoef = baseTransmission.ClutchCoef;
			ClutchError = baseTransmission.ClutchError;

			ShiftUpRPM = baseTransmission.ShiftUpRPM;
			ShiftDownRPM = baseTransmission.ShiftDownRPM;
			ShiftCooldownTime = baseTransmission.ShiftCooldownTime;

			ClutchEngageTime = baseTransmission.ClutchEngageTime;
			LaunchSpeed = baseTransmission.LaunchSpeed;
		}
	}
}
