using System;
using Common.Runtime.StateMachine;
using VContainer;

namespace Car.Controller.CarPhysics.States
{    
    public class CarStateFactory : IStateFactory<CarState>
    {
        private readonly IObjectResolver _resolver;

        public CarStateFactory(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public IState<CarState> Create(CarState state)
        {
            return state switch
            {
                CarState.Drive => _resolver.Resolve<DriveCarState>(),
                CarState.Drift => _resolver.Resolve<DriftCarState>(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}