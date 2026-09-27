# Task 1 — Сериализация SO + новые поля + дефолты

## Проблема
`EngineSO`, `TransmissionSO`, `DrivetrainSO` объявляют поля как `public float X { get; private set; }`
**без `[SerializeField]`**. Unity такие свойства не сериализует вообще — в любом созданном ассете
все значения равны нулю. Дальше по цепочке: `Volume = 0` → `Inertia = 0` → деление на ноль в
`UpdateAngularSpeed`; `Gears = null` → `Gears[SelectedGear]` → NRE; `WheelRadius = 0` → деление на ноль.
Пока это не исправлено, запустить и потестить невозможно.

## Файлы
- `Assets/Game/Car/Controller/CarPhysics/Engine/EngineSO.cs`
- `Assets/Game/Car/Controller/CarPhysics/Transmission/TransmissionSO.cs`
- `Assets/Game/Car/Controller/CarPhysics/Drivetrain/DrivetrainSO.cs`

## Что сделать
1. Добавить `[SerializeField]` ко всем полям во всех трёх SO. Публичные геттеры оставить.
2. Добавить `[Range]` / `[Min]` / `[Tooltip]` — чтобы параметры нельзя было выставить в 0 или отрицательные.
3. Проставить осмысленные значения по умолчанию в инициализаторах (сейчас почти всё 0).
4. `DrivetrainSO.SteerAngleCurve` — дефолт `AnimationCurve.Linear(0f, 0f, 1f, 1f)`.
   Сейчас он `null` → NRE в `GetSteeringAngle` на первом шаге физики.
5. `TransmissionSO.Gears` — дефолтный массив реальных передаточных отношений, 5 штук.
   Старый ассет (`speedToRpmFactor = 24.02`, ratios 7.75…2.08) к новой схеме
   `Gears[i] * FinalGear` отношения не имеет — числа оттуда не переносить.

## Новые поля

### `EngineSO`
| Поле | Назначение |
|---|---|
| `IdleRPM` | Цель холостых для П-регулятора |
| `IdleThrottleGain` | Жёсткость П-регулятора, подбирается в тюнинге (задача 12) |
| `StallRPM` | Аварийный пол. **Строго ниже `IdleRPM`**, иначе П-регулятор выродится в ноль |

### `TransmissionSO`
| Поле | Назначение |
|---|---|
| `ShiftUpRPM` | Порог повышения передачи |
| `ShiftDownRPM` | Порог понижения (гистерезис: заметно ниже `ShiftUpRPM`) |
| `ShiftCooldownTime` | Пауза между переключениями, сек |
| `ClutchEngageTime` | Время нарастания сцепления при трогании, сек |

### `DrivetrainSO`
| Поле | Назначение |
|---|---|
| `TireStiffness` | Жёсткость шины, Н на м/с проскальзывания. Используется **без домножений** |
| `TireSubsteps` | Внутренних шагов шины за кадр. По умолчанию 10 |
| `WheelInertia` | Инерция колеса, кг·м² |
| `WheelInternalFriction` | Вязкое трение в колесе |

## Критерии приёмки
- [ ] Создан ассет каждого SO — все значения сохраняются, не обнуляются после перезагрузки редактора
- [ ] Ни одно поле не может быть 0 там, где это ломает физику (ограничено `Min`/`Range`)
- [ ] `SteerAngleCurve` не `null` на свежем ассете
- [ ] `Gears` не `null`, длина ≥ 2
- [ ] `StallRPM` по умолчанию меньше `IdleRPM` (в инспекторе это должно быть видно)
- [ ] `TransmissionModel` — поле `private readonly TransmissionSO engine;` переименовать в `_baseTransmission` (копипаста)
