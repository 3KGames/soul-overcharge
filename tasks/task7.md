# Task 7 — Создать `.asset` для Engine / Transmission / Drivetrain

Зависит от: task 1, 2, 3, 4, 6. **Выполнять после того, как все SO-поля окончательно определены.**

## Проблема
Новых ассетов не существует. В `Assets/Game/Data` лежат только `CarPhysicsData`,
`GearDataRpm`, `HealthData`, `NitroData`, `SoulData` и прочее.

## Что сделать
Создать три ассета в `Assets/Game/Data`:
- `EngineSO` → например `Engine.asset`
- `TransmissionSO` → например `Transmission.asset`
- `DrivetrainSO` → например `Drivetrain.asset`

Значения — отправная точка для тюнинга, не финальные значения. Заполнить так, чтобы
машина поехала и не разлетелась.

## Ориентиры для стартовых значений

### DrivetrainSO
| Параметр | Старт | Комментарий |
|---|---|---|
| `WheelRadius` | 0.35 м | типичное легковое колесо |
| `Wheelbase` | 2.6 м | |
| `TireStiffness` | 5000 | как в текущем коде. Реальные шины 10⁴–10⁵, так что это мягко — пробуксовка вероятна |
| `TireSubsteps` | 10 | даёт шаг 2 мс, втрое устойчивее порога |
| `WheelInertia` | 2.0 кг·м² | из текущего кода |
| `WheelInternalFriction` | 0.05 | при 85 рад/с даёт всего 4.25 Н·м — почти ноль |
| `MaxSteerAngleLowSpeed` | 35 | |
| `MaxSteerAngleHighSpeed` | 5 | |
| `HighSpeedThreshold` | 50 км/ч | |

### TransmissionSO
Передаточные числа — **не переносить из старого `GearDataRpm.asset`**. Там стояли
`speedToRpmFactor = 24.02` и ratios 7.75…2.08, которые работали в паре с тем множителем.
В новой схеме используется `Gears[i] * FinalGear` напрямую.

Старт: 5 передач, `FinalGear ≈ 3.9`.
`ShiftUpRPM` / `ShiftDownRPM` — с гистерезисом примерно 6000 / 2800.
`ShiftCooldownTime` ≈ 0.35 с, `ClutchEngageTime` ≈ 0.4 с.
`ClutchCoef`, `ClutchError`, `Efficiency` — откалибровать в task 12, они напрямую
определяют `ClutchMax` и проскальзывание.

### EngineSO
Значения по умолчанию из task 1 должны давать разумный `PeakTorque`.
Критично проверить: **`PeakTorque` должен быть в разы больше, чем получался раньше**
(раньше считался при нулевых оборотах и попадал в коэффициент `A` — самую слабую точку кривой).
`IdleRPM ≈ 850`, `StallRPM` заметно ниже, `IdleThrottleGain` — маленький (см. task 2).

## Критерии приёмки
- [ ] Три ассета созданы и лежат в `Assets/Game/Data`
- [ ] Значения в них сохраняются (не обнуляются) — значит `[SerializeField]` проставлен верно
- [ ] `Gears` — реальные отношения, а не числа из старого ассета
- [ ] Ассеты подставлены в `LevelLifetimeScope`
- [ ] `EngineService` на этих значениях выдаёт `PeakTorque` заметно больше, чем на старом коде
- [ ] Расчётная максималка разумна (сходится с `forceDrag` при высокой скорости)
