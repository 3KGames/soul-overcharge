# Task 4 — `DrivetrainService`: шинная физика с внутренними шагами

Зависит от: task 1.

## Проблема (главный блокер — без этого машина разносит)

Сейчас шинная петля размазана по двум стейтам:
```csharp
float tirePatchSpeed = omegaWheels * _drivetrain.WheelR;
float slipSpeed      = tirePatchSpeed - forwardSpeed;
float tireForce      = slipSpeed * tireStiffness;               // k = 5000
rb.AddForce(forward * (tireForce - forceDrag) / BaseMass, Acceleration);
_drivetrain.UpdateWheelOmega(dt, torqueDrive - tireForce * WheelR);
```
Вычитая уравнения, `w` сокращается и остаётся чистое проскальзывание:
```
slip' = −( k·R²/I + k/M )·slip − (c·R/I)·w + const
```
Коэффициент: `k·R²/I = 5000·0.1225/2 = 306.25`, плюс `k/M = 5000/1391 = 3.59` → **−309.84 /с**.

Явная интеграция устойчива при `dt < 2/309.84` = **6.45 мс**. В `Time.fixedDeltaTime` (1/50 с)
шаг 20 мс — **втрое больше порога**. Множитель усиления за один кадр:
```
1 + (−309.84)·0.02 = −5.197
```
По модулю 5.2. Проскальзывание не колеблется, а **растёт в 5.2 раза за кадр**, меняя знак.
Раскадка от стартовых 0.1 м/с: 0.1 → −0.5 → 2.7 → −14 → 73 → −380 → 1970 → −10200 → **+53000**
за 9 кадров (0.18 с). Дальше `tireForce` = 2.6·10⁸ Н, машину срывает.

Почему не видно на глаз: у тикающего `W = 0.5` множитель `0.99` — визуально неотличим от единицы.

**Решение:** 10 внутренних шагов по ~2 мс. Тогда множитель `0.38` — энергия убывает, как надо.

Дополнительно: `WheelInertia = 2.0f` и `WheelInternalFriction = 0.05f` — публичные изменяемые
поля прямо в сервисе, не в SO. Перенести в SO (task 1 уже добавляет поля).

## Файлы
- `Assets/Game/Car/Controller/CarPhysics/Drivetrain/DrivetrainService.cs`
- `Assets/Game/Car/Controller/CarPhysics/Drivetrain/DrivetrainModel.cs` (пробросить новые поля)

## Что сделать

1. Забрать `TireStiffness`, `TireSubsteps`, `WheelInertia`, `WheelInternalFriction` из `DrivetrainModel`
   вместо локальных констант. Сделать `readonly`-свойствами, не изменяемыми полями.

2. **Вынести шинную петлю в сервис.** Сейчас она продублирована в `DriveCarState` и `DriftCarState`
   (в дрифте — ещё и в голом блоке `{ }` без условия, строки 96–120). Один источник истины.
   Метод принимает `dt`, суммарный момент на колесе, скорость кузова и массу;
   внутри — цикл из `TireSubsteps` шагов, возвращает `tireForce`.
   Возвращать именно силу, а не ускорение — делить на массу вызывает стейт, у него своя `effMass`.

3. **`null`-guard на `SteerAngleCurve`** в `GetSteeringAngle` — на ассете без кривой падает.

4. Проверить, что `tireStiffness` из инспектора попадает в формулу **без домножений**.
   `rb.mass` в петле не участвует — это осознанное решение, см. `tasks.md`.

## Критерии приёмки
- [ ] Множитель усиления на шаге `dt/TireSubsteps` по модулю меньше 1
- [ ] При `TireStiffness = 5000`, `TireSubsteps = 10`, `dt = 0.02` петля не расходится
- [ ] `DriveCarState` и `DriftCarState` не содержат собственной копии шинных расчётов
- [ ] `WheelInertia` / `WheelInternalFriction` приходят из `DrivetrainSO`
- [ ] Нулевое или отрицательное `TireSubsteps` не приводит к делению на ноль
- [ ] `GetSteeringAngle` не падает при `SteerAngleCurve == null`
- [ ] `tireForce` — в ньютонах, стейт сам делит на свою `effMass`
