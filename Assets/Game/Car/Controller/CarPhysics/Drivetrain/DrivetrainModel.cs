using UnityEngine;

namespace Car.Controller.CarPhysics.Drivetrain
{
	[System.Serializable]
	public class DrivetrainModel
	{
		private readonly DrivetrainSO baseDrivetrain;

		public float WheelRadius             { get; private set; }
		public float MaxSteerAngleLowSpeed  { get; private set; }
		public float MaxSteerAngleHighSpeed { get; private set; }
		public float HighSpeedThreshold     { get; private set; }
		public AnimationCurve SteerAngleCurve { get; private set; }
		public float Wheelbase               { get; private set; }

		public float TireStiffness         { get; private set; }
		public float TireSubsteps          { get; private set; }
		public float WheelInertia          { get; private set; }
		public float WheelInternalFriction { get; private set; }
		public float MaxLongitudinalGrip   { get; private set; }

		public DrivetrainModel(DrivetrainSO baseDrivetrain)
		{
			this.baseDrivetrain = baseDrivetrain;

			WheelRadius = baseDrivetrain.WheelRadius;
			MaxSteerAngleLowSpeed = baseDrivetrain.MaxSteerAngleLowSpeed;
			MaxSteerAngleHighSpeed = baseDrivetrain.MaxSteerAngleHighSpeed;
			HighSpeedThreshold = baseDrivetrain.HighSpeedThreshold;
			SteerAngleCurve = baseDrivetrain.SteerAngleCurve;
			Wheelbase = baseDrivetrain.Wheelbase;

			TireStiffness = baseDrivetrain.TireStiffness;
			TireSubsteps = baseDrivetrain.TireSubsteps;
			WheelInertia = baseDrivetrain.WheelInertia;
			WheelInternalFriction = baseDrivetrain.WheelInternalFriction;
			MaxLongitudinalGrip = baseDrivetrain.MaxLongitudinalGrip;
		}
	}
}
