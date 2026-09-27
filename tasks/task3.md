# Task 3 — `TransmissionService`: авто + ручная, события, engagement

Зависит от: task 1, task 2.

## Проблемы
1. **Автомата нет.** `UpdateAutoShift` из старой системы не перенесён. `SelectedGear` всегда
   сидит на 0, меняется только руками из инпута.
2. **`RpmChanged` отсутствует** — тахометр и звук мотора не получают обороты.
   В старом классе было `public event Action<float> RpmChanged;`
3. **`IsAutoTransmission` отсутствует** — `SettingsUI` к нему обращается, сломан.
4. **`engagement = 1f` захардкожен** в обоих стейтах. Сцепление нет — трогание с места идёт
   через пробуксовку вместо плавного подхвата.
5. `PeakTorque` из task 2 не берётся корректно (был посчитан при нулевых оборотах) —
   `ClutchMax` занижен.

## Файлы
- `Assets/Game/Car/Controller/CarPhysics/Transmission/TransmissionService.cs`

## Что сделать

### 1. События
```csharp
public event Action        GearChanged;    // уже есть, оставить
public event Action<float> RpmChanged;     // добавить, значение = RPM из EngineService
public bool IsAutoTransmission { get; set; } = true;
```
`RpmChanged` вызывается каждый тик коробки, значением — фактические обороты мотора
(не вычисленные из скорости, как в старой системе через `SpeedToRpmFactor`).

### 2. Скорость вращения колёс
Сейчас в двух местах дублируется:
```csharp
float angularSpeed = angWheelSpeed * _transmissionModel.Gears[SelectedGear] * _transmissionModel.FinalGear;
```
и то же в `CalculateDriveTorque` и в `CurGearRatio`. Свести к одному выражению над `CurGearRatio`.

### 3. Engagement
Убрать `engagement = 1f` из стейтов, считать внутри коробки.
Модель: при малой скорости (трогание) сцепление нарастает от 0 к 1 за `ClutchEngageTime`.
При `Brake` — сброс, чтобы можно было трогаться и тормозить на первой.

### 4. Автомат
`UpdateAuto(float dt, float throttle, float brake)`:
- повышение при `RPM >= ShiftUpRPM` и `CanShiftUp()`
- понижение при `RPM <= ShiftDownRPM` и `CanShiftDown()`
- пауза `ShiftCooldownTime` между переключениями, чтобы не дёргалось
- при нулевом газе и нулевом тормозе — не понижать передачу (иначе будет «скатываться» вниз)

### 5. Ручной режим
При `IsAutoTransmission == false` `UpdateAuto` не вызывается, работают только
`ShiftUpSafe` / `ShiftDownSafe` из `CarService.HandleGearShift`. Это уже прокинуто в инпут,
менять не нужно.

## Критерии приёмки
- [ ] `RpmChanged` срабатывает каждый тик, значение равно `EngineService.RPM`
- [ ] `GearChanged` срабатывает при смене передачи
- [ ] Автомат: разгон с нуля последовательно повышает передачи, торможение — понижает
- [ ] Гистерезис работает: после повышения передача не понижается обратно мгновенно
- [ ] Автомат не дёргается между двумя передачами на одной скорости
- [ ] `IsAutoTransmission = false` → передачи меняются только по инпуту
- [ ] `engagement` нарастает при трогании, сцепление не рвётся
- [ ] `ClutchMax` берётся из исправленного `PeakTorque`
- [ ] Дублирующееся выражение `Gears[i] * FinalGear` осталось в одном месте
