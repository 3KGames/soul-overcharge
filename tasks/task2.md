# Task 2 — `EngineService`: холостые, `PeakTorque`, `RPM`

Зависит от: task 1.

## Проблемы
1. **Мотор глохнет.** `CalculateTorque(0f)` = `idealTorque * Mathf.Pow(0, ThrottlePow)` = **0**.
   При отпущенном газе остаётся только трение (всегда ≥ 0), поэтому единственное равновесие
   это `angularSpeed = 0`. Холостых нет.
2. **`PeakTorque` занижен в ~2.5 раза.** Считается в конструкторе, когда `angularSpeed == 0`
   → `rpmNorm = 0` → формула Лейдермана даёт только коэффициент `A`, т.е. момент на холостых,
   самая слабая точка кривой. Дальше `ClutchMax = PeakTorque * ClutchCoef` → сцепление зажато →
   `torqueDrive` ограничен → машина недотягивает и RPM не привязан к скорости.
3. **`angularSpeed` не клампится.** Нет ограничителя (отложен), нет `Max(0f, …)` —
   с новым `WheelInternalFriction` на высокой скорости на низкой передаче мотор уходит в минус.
4. **Нет `RPM`.** Тахометру и FMOD нечего отдавать.

## Файлы
- `Assets/Game/Car/Controller/CarPhysics/Engine/EngineService.cs`

## Что сделать

### 1. Вынести расчёт в перегрузку
`CalculateTorque` сейчас читает внутреннее поле `angularSpeed`, поэтому «посчитать при других
оборотах» невозможно. Разделить на:
- `CalculateTorqueAt(float throttle, float angularSpeed)` — чистый расчёт, без состояния
- `CalculateTorque(float throttle)` — вызывает её с текущим `angularSpeed`

Перегрузка нужна и для задачи 9 (график в редакторе).

### 2. `PeakTorque` при максимальных оборотах
```csharp
float maxOmega = 30f * _engineModel.PistonMaxSpeed / _engineModel.PistonStroke;
PeakTorque = CalculateTorqueAt(1f, maxOmega);
```
Это ровно формула `N_rpmMax = 30 · S_ход / S_ход*PistonMax · 1000`, уже применённая в коде
как `maxRPM`, только в рад/с. Считать в конструкторе **нельзя** — раньше он брал `angularSpeed = 0`.

Известное допущение: `A + B·r + C·r²` может максимизироваться не при `r = 1`, а при
`r = −B/(2C)`. По решению пользователя считаем при максимальных оборотах. Если later окажется,
что машина недотягивает — вернуться и перебрать.

### 3. П-регулятор холостых
В начале расчёта:
```
idleThrottle   = Clamp01( Max(0, (IdleOmega - omega)) * IdleThrottleGain )
effectiveThrot = Clamp01( Max(playerThrottle, idleThrottle) )
```
и дальше в формулу идёт `effectiveThrottle` вместо `ThrottleEff` из `playerThrottle`.

Про `IdleThrottleGain`: **первоначальное значение 0.05 из спецификации пользователя корректно,
моя более ранняя рекомендация снизить его была ошибкой.** Решающий критерий — не насыщение,
а просадка оборотов под нагрузкой. Равновесие наступает там, где
`CalculateTorque(idleThrottle) == CalculateFriction()`, при этом
`idleThrottle = (IdleOmega - omega) * gain`, то есть чем больше gain, тем ближе к `IdleOmega`
реальные обороты. При gain = 0.05 просадка единиц процентов.

Насыщение `Clamp01` при `IdleOmega * gain > 1` — не дефект: оно срабатывает только далеко
от равновесия, то есть именно тогда, когда мотор заглох и нужно подхватить газ. В линейном
режиме вокруг `IdleOmega` выход регулятора мал, и это как раз П-регулятор, а не реле.

### 4. Пол на `StallOmega`
В `UpdateAngularSpeed` после интегрирования:
```csharp
angularSpeed += (effectiveTorque / Inertia) * dt;
angularSpeed = Mathf.Max(StallOmega, angularSpeed);
```
**Критично:** пол ставится на `StallOmega` (ниже цели), а **не** на `IdleOmega`.
При поле на `IdleOmega` условие `omega >= IdleOmega` выполняется всегда, значит
`idleThrottle` тождественно ноль, П-регулятор — мёртвый код, мотор залипает ровно на холостых
с нулевым моментом, и тахометр всегда показывает холостые.

Аварийная роль пола — патологические случаи: все нули в SO (`CalculateTorque` всегда 0),
лавина момента от сцепления за один кадр.

### 5. `RPM`
```csharp
public float RPM => angularSpeed * 30f / Mathf.PI;
```

## Критерии приёмки
- [ ] Газ отпущен, машина стоит — обороты держатся около `IdleRPM`, мотор не глохнет
- [ ] Обороты никогда не опускаются ниже `StallRPM`
- [ ] Реальные обороты на холостых отличаются от `IdleRPM` не более чем на единицы процентов
      (это критерий качества П-регулятора; насыщение внизу — норма)
- [ ] `PeakTorque` считается при `maxOmega`, а не при нулевых оборотах
- [ ] `PeakTorque` > 0 на ассете с непустыми значениями
- [ ] `CalculateTorqueAt` даёт тот же результат, что и `CalculateTorque` при текущих оборотах
- [ ] Публичный `RPM` совпадает с `angularSpeed * 30 / PI`
