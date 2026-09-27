# Task 6 — DI-обвязка в `LevelLifetimeScope`

Зависит от: task 1, 2, 3, 4.

## Проблема
`LevelLifetimeScope` регистрирует `TransmissionService`, но не регистрирует то, что нужно
для разрешения его конструктора. VContainer упадёт при старте сцены.

Текущий регистр `TransmissionService` (старый неймспейс `Game.Car.Controller.CarPhysics.Transmission`)
не соответствует новому классу в неймспейсе `Car.Controller.CarPhysics.Transmission`.

## Файлы
- `Assets/Game/Bootstrap/Scopes/LevelLifetimeScope.cs`

## Что сделать

1. Убрать `using Game.Car.Controller.CarPhysics.Transmission;` и поле
   `[SerializeField] private GearDataRpm gearDataRpm;` — старый класс больше не участвует.

2. Добавить `[SerializeField]` поля для новых ассетов: `EngineSO`, `TransmissionSO`, `DrivetrainSO`.
   Рядом с существующим `[Expandable] private CarPhysicsData physicsData`.

3. Зарегистрировать в правильном порядке (VContainer разрешает по графу, но порядок
   в файле стоит держать читаемым):
   - `builder.RegisterInstance(...)` для трёх SO
   - `EngineModel`, `TransmissionModel`, `DrivetrainModel` — `Singleton`
   - `EngineService` — `Singleton`
   - `DrivetrainService` — `Singleton`
   - `TransmissionService` — `Singleton` (зависит от `TransmissionModel` + `EngineService`)

4. `DriveCarState` и `DriftCarState` уже регистрируются как `Singleton` — их конструкторы
   требуют `EngineService` и `DrivetrainService`, которые до этого не были зарегистрированы.
   После шага 3 они начнут разрешаться.

5. `CarService` зависит от `TransmissionService` — проверить, что тип в using совпадает
   с новым (`Car.Controller.CarPhysics.Transmission`).

## Критерии приёмки
- [ ] В `LevelLifetimeScope` нет упоминаний `GearDataRpm` и старого неймспейса
- [ ] Все три SO зарегистрированы как `RegisterInstance`
- [ ] `EngineModel` / `TransmissionModel` / `DrivetrainModel` зарегистрированы
- [ ] `EngineService`, `DrivetrainService`, `TransmissionService` зарегистрированы
- [ ] Все конструкторы в графе разрешаются (проверяется в task 11)
