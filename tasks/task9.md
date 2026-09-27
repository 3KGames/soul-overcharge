# Task 9 — Переписать `AccelerationGraphWindow` под новую модель

Зависит от: task 2, 3, 4.

## Проблема
`Assets/Game/Car/Controller/CarPhysics/Editor/AccelerationGraphWindow.cs` (420 строк) целиком
завязан на старый API:
```csharp
var svc = new TransmissionService(_carPhysicsData, _gearData);
svc.ShiftUpSafe();
svc.GetAcceleration(speed, inputMock);   // в новом классе такого метода нет
_gearData.GearsCount, _gearData.RpmRedline, _gearData.SpeedToRpmFactor  // полей нет
```
Пространство имён `Game.Car.Controller.CarPhysics.Editor`, класс зарегистрирован в меню
`Tools/ArcadeCar/Acceleration Graph`. Пользователь решил переписать, а не удалять.

## Файлы
- `Assets/Game/Car/Controller/CarPhysics/Editor/AccelerationGraphWindow.cs`

## Что сделать

1. Сменить `using` на новые неймспейсы (`Car.Controller.CarPhysics.Transmission` и т.д.).

2. Вместо `GearDataRpm` — объектные поля для `EngineSO`, `TransmissionSO`, `DrivetrainSO`,
   плюс `CarPhysicsData`.

3. Построение кривой ускорения: для каждой передачи собирать реальный пайплайн
   ```
   EngineService → TransmissionService → DrivetrainService
   ```
   и на каждой точке по скорости считать ускорение тем же способом, что и `DriveCarState`:
   зафиксировать `omega` так, чтобы соответствовать скорости на выбранной передаче, посчитать
   момент, сцепление, `tireForce`, деление на `effMass`.

4. Зависимость от task 2: перегрузка `CalculateTorqueAt(throttle, angularSpeed)` позволяет
   считать момент при произвольных оборотах без изменения состояния. Без неё инструмент
   собрать нельзя.

5. Пересчитать вспомогательные функции под новые поля:
   - `ComputeMaxSpeedForGear` — вместо `RpmRedline / (GearRatio * SpeedToRpmFactor)` считать
     через `maxOmega` и отношение передачи
   - `GetRpmForSpeed` — `v / R * Gears[i] * FinalGear`, в рад/с → об/мин

6. Сохранить весь существующий UI: режимы `SelectedGear` / `AllGears`, `FitX`, `FitY`,
   оси, легенда, `CursorReadout`, диапазоны скорости, число сэмплов.

## Критерии приёмки
- [ ] Окно открывается через `Tools/ArcadeCar/Acceleration Graph`
- [ ] Показывает кривые ускорения по скорости для каждой передачи
- [ ] Кривые построены через реальные сервисы, а не по формуле из старой системы
- [ ] `CursorReadout` показывает ускорение и RPM выбранной передачи
- [ ] `FitX` подгоняет диапазон под максималку передачи
- [ ] Ни одного обращения к `GearDataRpm` / старому `TransmissionService`
- [ ] Сохраняет весь прежний функционал окна
